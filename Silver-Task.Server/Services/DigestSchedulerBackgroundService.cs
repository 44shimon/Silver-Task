using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Phase 46 — replaces the old (Phase 36) NotificationDigestBackgroundService, which sent a
    /// fixed-shape digest directly via IEmailService (no retry/tracking) to every user with a
    /// single global DigestFrequency == "Daily". This version drives the new per-notification-type
    /// EmailDeliveryMode (spec's own per-category Immediately/DailyDigest/WeeklyDigest/Off), builds
    /// real content via IDigestGenerationService, and enqueues through the same EmailDelivery
    /// queue/retry pipeline Phase 45 built (spec §47 "do not create a second retry implementation").
    ///
    /// Same PeriodicTimer + per-tick DI-scope pattern as every other background service in this
    /// app. 10-minute interval (tighter than the old hourly sweep, matching spec §42's "every
    /// 5-15 minutes") — deliberately checks "is local time past the configured send time and has
    /// today's/this week's digest not already been generated" with **no upper bound window**, so
    /// if the app was offline at the scheduled time it still catches up the same day/week once it
    /// resumes (spec §44), rather than waiting for the next cycle. Batches users via
    /// SystemSettingKeys.MaxNotificationBatchSize (spec §68/§69 — never loads the whole user base).
    ///
    /// No distributed lock: this app has no multi-instance/distributed job coordination anywhere
    /// (every BackgroundService here assumes a single running instance) — see the Phase 46 plan's
    /// own "Non-goals" section for why this is a documented limitation, not an oversight.
    /// </summary>
    public class DigestSchedulerBackgroundService(
        IServiceScopeFactory scopeFactory,
        IWorkerHeartbeatRegistry heartbeats,
        ILogger<DigestSchedulerBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
        private const string WorkerName = "digest-scheduler";

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IWorkerHeartbeatRegistry _heartbeats = heartbeats;
        private readonly ILogger<DigestSchedulerBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            await RunTickAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunTickAsync(stoppingToken);
            }
        }

        private async Task RunTickAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var digestService = scope.ServiceProvider.GetRequiredService<IDigestGenerationService>();
                var systemSettings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

                var maxBatch = await systemSettings.GetIntAsync(SystemSettingKeys.MaxNotificationBatchSize);
                var utcNow = DateTime.UtcNow;

                await ProcessAsync(db, digestService, NotificationDeliveryModes.DailyDigest, maxBatch, utcNow,
                    isDue: p => IsDueDaily(p, utcNow),
                    generate: (svc, userId, ct) => svc.TryGenerateDailyDigestAsync(userId, ct),
                    stoppingToken);

                await ProcessAsync(db, digestService, NotificationDeliveryModes.WeeklyDigest, maxBatch, utcNow,
                    isDue: p => IsDueWeekly(p, utcNow),
                    generate: (svc, userId, ct) => svc.TryGenerateWeeklyDigestAsync(userId, ct),
                    stoppingToken);

                _heartbeats.ReportSuccess(WorkerName, Interval);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Digest scheduler sweep failed.");
            }
        }

        private static async Task ProcessAsync(
            AppDbContext db, IDigestGenerationService digestService, string mode, int maxBatch, DateTime utcNow,
            Func<UserPreference, bool> isDue, Func<IDigestGenerationService, Guid, CancellationToken, Task<bool>> generate,
            CancellationToken stoppingToken)
        {
            // Only users who have at least one notification type actually set to this mode are
            // candidates — everyone else (the common case, since the default per-type mode is
            // Immediately) is never even loaded.
            var candidateUserIds = await db.UserNotificationSettings
                .Where(s => s.EmailDeliveryMode == mode)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(stoppingToken);
            if (candidateUserIds.Count == 0)
            {
                return;
            }

            var duePreferences = await db.UserPreferences
                .Include(p => p.User)
                .Where(p => candidateUserIds.Contains(p.UserId) && p.User!.IsActive)
                .Take(maxBatch * 4) // over-fetch before the per-user timezone check below, same pattern the old digest sweep used
                .ToListAsync(stoppingToken);

            var due = duePreferences.Where(isDue).Take(maxBatch).ToList();
            foreach (var preference in due)
            {
                await generate(digestService, preference.UserId, stoppingToken);
            }
        }

        private static bool IsDueDaily(UserPreference preference, DateTime utcNow)
        {
            var timeZone = ResolveTimeZone(preference.TimeZone);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            if (TimeOnly.FromDateTime(localNow) < preference.DailyDigestTime)
            {
                return false;
            }
            if (preference.LastDailyDigestAt is not DateTime lastSent)
            {
                return true;
            }
            var localLastSent = TimeZoneInfo.ConvertTimeFromUtc(lastSent, timeZone);
            return DateOnly.FromDateTime(localLastSent) < DateOnly.FromDateTime(localNow);
        }

        private static bool IsDueWeekly(UserPreference preference, DateTime utcNow)
        {
            var timeZone = ResolveTimeZone(preference.TimeZone);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            if (!Enum.TryParse<DayOfWeek>(preference.WeeklyDigestDay, ignoreCase: true, out var configuredDay) ||
                localNow.DayOfWeek != configuredDay)
            {
                return false;
            }
            if (TimeOnly.FromDateTime(localNow) < preference.WeeklyDigestTime)
            {
                return false;
            }
            if (preference.LastWeeklyDigestAt is not DateTime lastSent)
            {
                return true;
            }
            var localLastSent = TimeZoneInfo.ConvertTimeFromUtc(lastSent, timeZone);
            // ISO week comparison (not just "was it >6 days ago") so a catch-up run that fires a
            // few hours late on the correct day still only sends once for that calendar week.
            return ISOWeek.GetYear(localLastSent) != ISOWeek.GetYear(localNow) ||
                ISOWeek.GetWeekOfYear(localLastSent) != ISOWeek.GetWeekOfYear(localNow);
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Phase 36 — sends the "Silver Task — Daily Summary" email to every user whose
    /// UserPreference.DigestFrequency is "Daily". Runs hourly (same PeriodicTimer + per-tick DI
    /// scope pattern as every other background service here); each tick only picks up users whose
    /// *local* time (per their own UserPreference.TimeZone) currently falls in a fixed morning
    /// window and who haven't already been sent one so far today, so a user is never double-
    /// digested no matter how often the sweep ticks, and each user's "day" is their own, not UTC's.
    /// </summary>
    public class NotificationDigestBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDigestBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private static readonly TimeOnly DigestWindowStart = new(8, 0);
        private static readonly TimeOnly DigestWindowEnd = new(9, 0);

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<NotificationDigestBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            await RunSweepAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunSweepAsync(stoppingToken);
            }
        }

        private async Task RunSweepAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var systemSettings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                if (!emailService.IsConfigured)
                {
                    return;
                }
                if (!await systemSettings.GetBoolAsync(SystemSettingKeys.DailyDigestEnabled) ||
                    !await systemSettings.GetBoolAsync(SystemSettingKeys.EmailNotificationsEnabled))
                {
                    return;
                }

                var maxBatch = await systemSettings.GetIntAsync(SystemSettingKeys.MaxNotificationBatchSize);
                var utcNow = DateTime.UtcNow;

                var candidates = await db.UserPreferences
                    .Include(p => p.User)
                    .Where(p => p.DigestFrequency == "Daily" && p.User!.IsActive)
                    .Take(maxBatch * 4) // over-fetch before the per-user timezone filter below, which can't run in SQL
                    .ToListAsync(stoppingToken);

                var dueToSend = candidates
                    .Where(p => IsDueForDigest(p.TimeZone, p.LastDigestSentAt, utcNow))
                    .Take(maxBatch)
                    .ToList();

                var appName = await systemSettings.GetStringAsync(SystemSettingKeys.ApplicationName);
                var appBaseUrl = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault() ?? "";

                foreach (var preference in dueToSend)
                {
                    var since = preference.LastDigestSentAt ?? utcNow.AddDays(-1);

                    var assignedCount = await db.Tasks.CountAsync(t =>
                        t.AssignedToUserId == preference.UserId &&
                        t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled, stoppingToken);
                    var today = DateOnly.FromDateTime(utcNow);
                    var dueTodayCount = await db.Tasks.CountAsync(t =>
                        t.AssignedToUserId == preference.UserId && t.DueDate == today &&
                        t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled, stoppingToken);
                    var overdueCount = await db.Tasks.CountAsync(t =>
                        t.AssignedToUserId == preference.UserId && t.DueDate < today &&
                        t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled, stoppingToken);
                    var newMentionsCount = await db.Notifications.CountAsync(n =>
                        n.UserId == preference.UserId && n.Type == NotificationTypes.MentionedInComment && n.CreatedAt >= since, stoppingToken);
                    var newCommentsCount = await db.Notifications.CountAsync(n =>
                        n.UserId == preference.UserId && n.Type == NotificationTypes.CommentAdded && n.CreatedAt >= since, stoppingToken);

                    var (subject, html) = NotificationTemplates.ForDigest(
                        appName, appBaseUrl, assignedCount, dueTodayCount, overdueCount, newMentionsCount, newCommentsCount);
                    await emailService.SendAsync(preference.User!.Email, preference.User.Name, subject, html);

                    preference.LastDigestSentAt = utcNow;
                }

                if (dueToSend.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Notification digest sweep failed.");
            }
        }

        private static bool IsDueForDigest(string timeZoneId, DateTime? lastSentAtUtc, DateTime utcNow)
        {
            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                timeZone = TimeZoneInfo.Utc;
            }

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            if (TimeOnly.FromDateTime(localNow) < DigestWindowStart || TimeOnly.FromDateTime(localNow) >= DigestWindowEnd)
            {
                return false;
            }

            if (lastSentAtUtc is not DateTime lastSent)
            {
                return true;
            }

            var localLastSent = TimeZoneInfo.ConvertTimeFromUtc(lastSent, timeZone);
            return DateOnly.FromDateTime(localLastSent) < DateOnly.FromDateTime(localNow);
        }
    }
}

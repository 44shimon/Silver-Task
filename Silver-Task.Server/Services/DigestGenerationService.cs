using System.Net;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IDigestGenerationService
    {
        /// <summary>Builds (if there's anything to say — spec's own "do not send an empty
        /// digest" rule) and enqueues one Daily Digest EmailDelivery for this user, and advances
        /// UserPreference.LastDailyDigestAt in the same SaveChanges either way (see the class doc
        /// comment on why "generated" and "sent" are deliberately different moments). Returns
        /// whether a digest actually had content and was enqueued — purely informational for the
        /// caller (DigestSchedulerBackgroundService)/tests, not something callers need to branch
        /// on.</summary>
        Task<bool> TryGenerateDailyDigestAsync(Guid userId, CancellationToken cancellationToken);

        Task<bool> TryGenerateWeeklyDigestAsync(Guid userId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Phase 46 — builds Daily/Weekly digest content entirely from data that already exists
    /// (Notification rows for the "what happened" sections, live Tasks/Projects queries for the
    /// "what's on your plate" sections — spec's own "reuse existing notification records, do not
    /// create a duplicate event history" requirement) and enqueues it through the *same*
    /// EmailDelivery queue/retry pipeline Phase 45 built for immediate email, via
    /// EmailDelivery.RenderedSubject/RenderedHtmlBody (see that entity's own doc comment).
    ///
    /// Content is rendered once, here, not re-computed on every retry attempt — this is
    /// deliberate: UserPreference.LastDailyDigestAt/LastWeeklyDigestAt advance in the very same
    /// SaveChanges call that enqueues the EmailDelivery row, so "has today's digest already been
    /// generated" and "what window does it cover" are both decided atomically, once, regardless
    /// of how many times EmailDeliveryBackgroundService subsequently retries sending it.
    /// </summary>
    public class DigestGenerationService(
        AppDbContext db,
        IEmailService emailService,
        IEmailTemplateService templateService,
        ISystemSettingsService systemSettings,
        IConfiguration configuration) : IDigestGenerationService
    {
        // Ordered exactly per spec §31 (Overdue/Due Today first, then the notification-derived
        // categories); "Other Updates" is the catch-all bucket for any NotificationType not
        // explicitly mapped, so no event type is ever silently dropped from a digest.
        private static readonly (string Type, string Section)[] SectionMap =
        [
            (NotificationTypes.TaskAssigned, "ASSIGNMENTS"),
            (NotificationTypes.TaskReassigned, "ASSIGNMENTS"),
            (NotificationTypes.TaskUnassigned, "ASSIGNMENTS"),
            (NotificationTypes.MentionedInComment, "MENTIONS"),
            (NotificationTypes.CommentAdded, "COMMENTS"),
            (NotificationTypes.TaskStatusChanged, "STATUS CHANGES"),
            (NotificationTypes.TaskPriorityChanged, "PRIORITY CHANGES"),
            (NotificationTypes.TaskDueDateChanged, "DUE DATE CHANGES"),
            (NotificationTypes.TaskDueSoon, "DUE SOON"),
            (NotificationTypes.TaskOverdue, "OVERDUE"),
            (NotificationTypes.TaskCompleted, "COMPLETED"),
            (NotificationTypes.ProjectTaskCompleted, "COMPLETED"),
            (NotificationTypes.TaskReopened, "COMPLETED"),
            (NotificationTypes.UserAddedToProject, "PROJECT CHANGES"),
            (NotificationTypes.UserRemovedFromProject, "PROJECT CHANGES"),
            (NotificationTypes.ProjectStatusChanged, "PROJECT CHANGES"),
            (NotificationTypes.ProjectRoleChanged, "PROJECT CHANGES"),
        ];
        private static readonly IReadOnlyDictionary<string, string> SectionByType =
            SectionMap.ToDictionary(x => x.Type, x => x.Section, StringComparer.OrdinalIgnoreCase);
        private static readonly string[] SectionOrder =
        [
            "ASSIGNMENTS", "MENTIONS", "COMMENTS", "STATUS CHANGES", "PRIORITY CHANGES",
            "DUE DATE CHANGES", "DUE SOON", "OVERDUE", "COMPLETED", "PROJECT CHANGES", "OTHER UPDATES"
        ];
        private const int MaxItemsPerSection = 10;
        private const int MaxItemsPerLiveList = 10;

        private readonly AppDbContext _db = db;
        private readonly IEmailService _emailService = emailService;
        private readonly IEmailTemplateService _templateService = templateService;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly IConfiguration _configuration = configuration;

        public Task<bool> TryGenerateDailyDigestAsync(Guid userId, CancellationToken cancellationToken) =>
            TryGenerateAsync(userId, isWeekly: false, cancellationToken);

        public Task<bool> TryGenerateWeeklyDigestAsync(Guid userId, CancellationToken cancellationToken) =>
            TryGenerateAsync(userId, isWeekly: true, cancellationToken);

        private async Task<bool> TryGenerateAsync(Guid userId, bool isWeekly, CancellationToken cancellationToken)
        {
            // Spec §50 — the full pre-generation gate: SMTP configured, both global email
            // switches, and (defensively — the scheduler already filters to active users) the
            // recipient still exists/is active. Nothing here advances Last*DigestAt: if email is
            // simply unavailable, there is nothing to "have processed" yet, so a later-enabled
            // deployment still catches up on the true backlog rather than silently losing it.
            if (!_emailService.IsConfigured)
            {
                return false;
            }
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return false;
            }
            if (!await _systemSettings.GetBoolAsync(SystemSettingKeys.EmailNotificationsEnabled) ||
                !await _systemSettings.GetBoolAsync(SystemSettingKeys.DailyDigestEnabled))
            {
                return false;
            }

            var preference = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (preference is null || !preference.EmailNotificationsEnabled)
            {
                return false;
            }

            var mode = isWeekly ? NotificationDeliveryModes.WeeklyDigest : NotificationDeliveryModes.DailyDigest;
            var eligibleTypes = await _db.UserNotificationSettings
                .Where(s => s.UserId == userId && s.EmailDeliveryMode == mode)
                .Select(s => s.NotificationType)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var lastSent = isWeekly ? preference.LastWeeklyDigestAt : preference.LastDailyDigestAt;
            var windowStart = lastSent ?? now.AddDays(isWeekly ? -7 : -1);

            var notifications = eligibleTypes.Count == 0
                ? []
                : await _db.Notifications
                    .Where(n => n.UserId == userId && eligibleTypes.Contains(n.Type) && n.CreatedAt > windowStart)
                    .Include(n => n.ActorUser)
                    .OrderBy(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);

            // One batched query for every distinct project referenced across notifications and
            // the live task queries below (spec §67's N+1 avoidance) — also the current-access
            // re-check (spec §51/§57): a project the user has since lost access to (and isn't an
            // Administrator for) is simply excluded, not just filtered by a possibly-stale
            // AssignedToUserId/Notification.ProjectId.
            var candidateProjectIds = notifications.Where(n => n.ProjectId is not null).Select(n => n.ProjectId!.Value)
                .Concat(await _db.Tasks.Where(t => t.AssignedToUserId == userId).Select(t => t.ProjectId).Distinct().ToListAsync(cancellationToken))
                .Distinct()
                .ToList();
            var accessibleProjects = candidateProjectIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _db.Projects
                    .Where(p => candidateProjectIds.Contains(p.Id) &&
                        (user.Role == UserRole.Administrator || p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)))
                    .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

            var sections = BuildNotificationSections(notifications, accessibleProjects);

            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, ResolveTimeZone(preference.TimeZone)));
            var (overdueItems, overdueCount) = await BuildOverdueAsync(userId, accessibleProjects, today, cancellationToken);
            var (dueTodayItems, dueTodayCount) = await BuildDueTodayAsync(userId, accessibleProjects, today, cancellationToken);
            var (upcomingItems, _) = await BuildUpcomingAsync(userId, accessibleProjects, today, isWeekly ? 14 : 7, cancellationToken);
            var dueThisWeekCount = await _db.Tasks.CountAsync(t =>
                t.AssignedToUserId == userId && t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled &&
                t.DueDate != null && t.DueDate >= today && t.DueDate <= today.AddDays(7) &&
                accessibleProjects.Keys.Contains(t.ProjectId), cancellationToken);

            IReadOnlyList<(string Text, string? Url)> completedThisWeek = [];
            if (isWeekly)
            {
                var completedTasks = await _db.Tasks
                    .Where(t => t.AssignedToUserId == userId && t.CompletedAt != null && t.CompletedAt > windowStart &&
                        accessibleProjects.Keys.Contains(t.ProjectId))
                    .OrderByDescending(t => t.CompletedAt)
                    .Take(MaxItemsPerLiveList)
                    .Select(t => new { t.Id, t.Title, t.ProjectId })
                    .ToListAsync(cancellationToken);
                completedThisWeek = completedTasks
                    .Select(t => ($"{t.Title} — {accessibleProjects.GetValueOrDefault(t.ProjectId)}", (string?)$"/projects/{t.ProjectId}?task={t.Id}"))
                    .ToList();
            }

            var assignmentCount = notifications.Count(n => SectionByType.GetValueOrDefault(n.Type) == "ASSIGNMENTS");
            var mentionCount = notifications.Count(n => n.Type == NotificationTypes.MentionedInComment);
            var commentCount = notifications.Count(n => n.Type == NotificationTypes.CommentAdded);

            var hasContent = sections.Count > 0 || overdueItems.Count > 0 || dueTodayItems.Count > 0 || completedThisWeek.Count > 0;

            if (hasContent)
            {
                var digestType = isWeekly ? DefaultDigestTemplates.WeeklyDigestType : DefaultDigestTemplates.DailyDigestType;
                var appName = await _systemSettings.GetStringAsync(SystemSettingKeys.ApplicationName);
                var appBaseUrl = await AppUrlResolver.ResolveAsync(_systemSettings, _configuration);

                var contentHtml = RenderContentHtml(sections, overdueItems, dueTodayItems, upcomingItems, completedThisWeek, dueTodayCount, dueThisWeekCount, overdueCount, appBaseUrl);
                var variables = new DigestTemplateVariables(
                    UserName: user.Name,
                    DigestDate: today.ToString(preference.DateFormat),
                    AssignmentCount: assignmentCount,
                    MentionCount: mentionCount,
                    CommentCount: commentCount,
                    DueTodayCount: dueTodayCount,
                    OverdueCount: overdueCount,
                    ActionUrl: "/notifications");

                var (subject, html) = await _templateService.RenderDigestAsync(digestType, variables, contentHtml, appName, appBaseUrl);

                _db.EmailDeliveries.Add(new EmailDelivery
                {
                    Id = Guid.NewGuid(),
                    RecipientUserId = userId,
                    RecipientEmail = user.Email,
                    NotificationType = digestType,
                    Title = subject,
                    Message = isWeekly ? "Your weekly summary" : "Your daily summary",
                    ActionUrl = "/notifications",
                    RenderedSubject = subject,
                    RenderedHtmlBody = html,
                    Status = EmailDeliveryStatus.Queued,
                    QueuedAt = now,
                    NextAttemptAt = now
                });
            }

            if (isWeekly)
            {
                preference.LastWeeklyDigestAt = now;
            }
            else
            {
                preference.LastDailyDigestAt = now;
            }
            await _db.SaveChangesAsync(cancellationToken);

            return hasContent;
        }

        private static List<(string Section, IReadOnlyList<string> Items, int TotalCount)> BuildNotificationSections(
            IReadOnlyList<Notification> notifications, IReadOnlyDictionary<Guid, string> accessibleProjects)
        {
            // Spec §51/§57 — a notification whose ProjectId is set but no longer in the
            // accessible-project map (access revoked, or the project was deleted) is dropped
            // entirely from the digest, not just shown without its project name.
            var visible = notifications.Where(n => n.ProjectId is null || accessibleProjects.ContainsKey(n.ProjectId.Value)).ToList();

            // Spec §29 — collapse repeated same-task-same-type notifications into one "N updates
            // to X" line instead of N near-identical lines.
            var grouped = visible
                .GroupBy(n => (Section: SectionByType.GetValueOrDefault(n.Type, "OTHER UPDATES"), n.TaskId, n.Type))
                .Select(g =>
                {
                    var first = g.OrderByDescending(n => n.CreatedAt).First();
                    var projectSuffix = first.ProjectId is Guid pid && accessibleProjects.TryGetValue(pid, out var projectName)
                        ? $" — {projectName}"
                        : "";
                    var text = g.Count() > 1 && first.TaskId is not null
                        ? $"{g.Count()} updates to \"{TruncateTitle(first.Title)}\"{projectSuffix}"
                        : $"{first.Title}{projectSuffix}";
                    return (g.Key.Section, Text: text, LatestAt: first.CreatedAt);
                })
                .ToList();

            return SectionOrder
                .Select(section =>
                {
                    var items = grouped.Where(x => x.Section == section).OrderByDescending(x => x.LatestAt).ToList();
                    return (section, (IReadOnlyList<string>)items.Take(MaxItemsPerSection).Select(x => x.Text).ToList(), items.Count);
                })
                .Where(x => x.Item3 > 0)
                .ToList();
        }

        private async Task<(IReadOnlyList<(string Text, string? Url)> Items, int TotalCount)> BuildOverdueAsync(
            Guid userId, IReadOnlyDictionary<Guid, string> accessibleProjects, DateOnly today, CancellationToken cancellationToken)
        {
            var overdue = await _db.Tasks
                .Where(t => t.AssignedToUserId == userId && t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled &&
                    t.DueDate != null && t.DueDate < today && accessibleProjects.Keys.Contains(t.ProjectId))
                .OrderBy(t => t.DueDate)
                .Select(t => new { t.Id, t.Title, t.ProjectId, t.DueDate })
                .ToListAsync(cancellationToken);

            var items = overdue.Take(MaxItemsPerLiveList).Select(t =>
            {
                var days = today.DayNumber - t.DueDate!.Value.DayNumber;
                return ($"{t.Title} — {days} day{(days == 1 ? "" : "s")} overdue", (string?)$"/projects/{t.ProjectId}?task={t.Id}");
            }).ToList();

            return (items, overdue.Count);
        }

        private async Task<(IReadOnlyList<(string Text, string? Url)> Items, int TotalCount)> BuildDueTodayAsync(
            Guid userId, IReadOnlyDictionary<Guid, string> accessibleProjects, DateOnly today, CancellationToken cancellationToken)
        {
            var dueToday = await _db.Tasks
                .Where(t => t.AssignedToUserId == userId && t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled &&
                    t.DueDate == today && accessibleProjects.Keys.Contains(t.ProjectId))
                .OrderBy(t => t.Title)
                .Select(t => new { t.Id, t.Title, t.ProjectId })
                .ToListAsync(cancellationToken);

            var items = dueToday.Take(MaxItemsPerLiveList).Select(t => (t.Title, (string?)$"/projects/{t.ProjectId}?task={t.Id}")).ToList();
            return (items, dueToday.Count);
        }

        private async Task<(IReadOnlyList<(string Text, string? Url)> Items, int TotalCount)> BuildUpcomingAsync(
            Guid userId, IReadOnlyDictionary<Guid, string> accessibleProjects, DateOnly today, int lookaheadDays, CancellationToken cancellationToken)
        {
            var end = today.AddDays(lookaheadDays);
            var upcoming = await _db.Tasks
                .Where(t => t.AssignedToUserId == userId && t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled &&
                    t.DueDate != null && t.DueDate > today && t.DueDate <= end && accessibleProjects.Keys.Contains(t.ProjectId))
                .OrderBy(t => t.DueDate)
                .Select(t => new { t.Id, t.Title, t.ProjectId, t.DueDate })
                .ToListAsync(cancellationToken);

            var items = upcoming.Take(MaxItemsPerLiveList)
                .Select(t => ($"{t.DueDate!.Value:ddd MMM d} — {t.Title}", (string?)$"/projects/{t.ProjectId}?task={t.Id}"))
                .ToList();
            return (items, upcoming.Count);
        }

        private static string RenderContentHtml(
            IReadOnlyList<(string Section, IReadOnlyList<string> Items, int TotalCount)> sections,
            IReadOnlyList<(string Text, string? Url)> overdueItems,
            IReadOnlyList<(string Text, string? Url)> dueTodayItems,
            IReadOnlyList<(string Text, string? Url)> upcomingItems,
            IReadOnlyList<(string Text, string? Url)> completedThisWeek,
            int dueTodayCount, int dueThisWeekCount, int overdueCount,
            string appBaseUrl)
        {
            var html = new System.Text.StringBuilder();

            html.Append("""<div style="margin:0 0 16px;padding:10px 12px;background:#f9fafb;border-radius:6px;font-size:13px">""");
            html.Append($"""<strong>Your Tasks</strong><br/>Due Today: {dueTodayCount} &middot; Due This Week: {dueThisWeekCount} &middot; Overdue: {overdueCount}<br/>""");
            html.Append($"""<a href="{WebUtility.HtmlEncode(appBaseUrl.TrimEnd('/') + "/my-tasks")}">View My Tasks</a>""");
            html.Append("</div>");

            AppendLinkedList(html, "OVERDUE", overdueItems, overdueItems.Count, appBaseUrl);
            AppendLinkedList(html, "DUE TODAY", dueTodayItems, dueTodayItems.Count, appBaseUrl);
            AppendLinkedList(html, "UPCOMING", upcomingItems, upcomingItems.Count, appBaseUrl);

            foreach (var (section, items, totalCount) in sections)
            {
                AppendPlainList(html, section, items, totalCount, appBaseUrl);
            }

            AppendLinkedList(html, "COMPLETED THIS WEEK", completedThisWeek, completedThisWeek.Count, appBaseUrl);

            return html.ToString();
        }

        private static void AppendLinkedList(System.Text.StringBuilder html, string title, IReadOnlyList<(string Text, string? Url)> items, int totalCount, string appBaseUrl)
        {
            if (items.Count == 0)
            {
                return;
            }
            html.Append($"""<h3 style="font-size:13px;margin:16px 0 6px;color:#111827">{WebUtility.HtmlEncode(title)}</h3>""");
            html.Append("""<ul style="margin:0;padding-left:18px;font-size:13px;color:#374151">""");
            foreach (var (text, url) in items)
            {
                var safeText = WebUtility.HtmlEncode(text);
                html.Append(url is null
                    ? $"<li>{safeText}</li>"
                    : $"""<li><a href="{WebUtility.HtmlEncode(appBaseUrl.TrimEnd('/') + url)}" style="color:#374151">{safeText}</a></li>""");
            }
            html.Append("</ul>");
            AppendMoreLink(html, items.Count, totalCount, appBaseUrl);
        }

        private static void AppendPlainList(System.Text.StringBuilder html, string title, IReadOnlyList<string> items, int totalCount, string appBaseUrl)
        {
            if (items.Count == 0)
            {
                return;
            }
            html.Append($"""<h3 style="font-size:13px;margin:16px 0 6px;color:#111827">{WebUtility.HtmlEncode(title)}</h3>""");
            html.Append("""<ul style="margin:0;padding-left:18px;font-size:13px;color:#374151">""");
            foreach (var text in items)
            {
                html.Append($"<li>{WebUtility.HtmlEncode(text)}</li>");
            }
            html.Append("</ul>");
            AppendMoreLink(html, items.Count, totalCount, appBaseUrl);
        }

        private static void AppendMoreLink(System.Text.StringBuilder html, int shown, int totalCount, string appBaseUrl)
        {
            // Spec §32 — bound worst-case email size instead of dumping an unbounded list.
            if (totalCount > shown)
            {
                html.Append($"""<p style="margin:4px 0 0;font-size:12px"><a href="{WebUtility.HtmlEncode(appBaseUrl.TrimEnd('/') + "/notifications")}">+ {totalCount - shown} more &rarr; View All</a></p>""");
            }
        }

        private static string TruncateTitle(string title) => title.Length <= 60 ? title : string.Concat(title.AsSpan(0, 59), "…");

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

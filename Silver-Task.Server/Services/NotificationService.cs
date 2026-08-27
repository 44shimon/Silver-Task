using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// The single reusable entry point every other service calls to raise a notification —
        /// centralizes the rules that would otherwise be duplicated at every call site:
        /// never notify a user about their own action, never notify a deactivated/deleted user,
        /// respect that recipient's UserNotificationSetting for this type (in-app and email are
        /// independently gated — defaulting to enabled, same as UserNotificationSettingsService),
        /// resolve a sensible Priority/ActionUrl automatically when the caller doesn't supply one,
        /// and (opt-in, via eventId) de-duplicate against the same (user, type, event) triple.
        ///
        /// Deliberately does NOT call SaveChangesAsync for the in-app row — this only adds to the
        /// AppDbContext change tracker, so the caller's own SaveChangesAsync (already happening
        /// right after the task/comment/membership change it's reporting on) persists both
        /// atomically. A notification is never created for a change that then fails to save,
        /// because it's added to the very same unit of work as that change. The optional email
        /// side effect, by contrast, is fired inline and best-effort (see IEmailService.SendAsync)
        /// since it isn't part of that transactional unit of work at all.
        /// </summary>
        Task NotifyAsync(
            Guid recipientUserId,
            Guid? actorUserId,
            string type,
            string title,
            string message,
            Guid? taskId = null,
            Guid? projectId = null,
            string? metadata = null,
            NotificationPriority? priority = null,
            Guid? commentId = null,
            Guid? fileId = null,
            string? actionUrl = null,
            Guid? eventId = null);

        /// <param name="isRead">Null returns both read and unread.</param>
        /// <param name="types">Multi-value alternative to <paramref name="type"/> — backs the
        /// notification center's category tabs (Tasks/Projects span several individual types;
        /// see Common.NotificationCategories). Both filters apply together (AND) if somehow both
        /// are supplied, though the controller only ever sends one or the other.</param>
        Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetForUserAsync(
            Guid userId, bool? isRead, int page, int pageSize,
            string? search = null, string? type = null, NotificationPriority? priority = null,
            Guid? projectId = null, Guid? taskId = null, DateTime? dateFrom = null, DateTime? dateTo = null,
            IReadOnlyCollection<string>? types = null);

        Task<Notification> GetByIdAsync(Guid notificationId, Guid userId);

        Task<int> GetUnreadCountAsync(Guid userId);

        Task MarkReadAsync(Guid notificationId, Guid userId);

        Task MarkUnreadAsync(Guid notificationId, Guid userId);

        Task MarkAllReadAsync(Guid userId);

        Task DeleteAsync(Guid notificationId, Guid userId);

        /// <summary>Bulk mark-read for the notification center's multi-select toolbar — ids
        /// outside the caller's own notifications are silently ignored (re-scoped by UserId in
        /// the same query), never a 404/403 that would let a caller distinguish "not mine" from
        /// "doesn't exist".</summary>
        Task BulkMarkReadAsync(IReadOnlyList<Guid> ids, Guid userId);

        Task BulkDeleteAsync(IReadOnlyList<Guid> ids, Guid userId);

        /// <summary>Called periodically by DueDateNotificationBackgroundService — scans assigned,
        /// still-open tasks for ones due today/tomorrow or already overdue, and raises TaskDueSoon
        /// / TaskOverdue notifications. Safe to call repeatedly: a task+user+type that already has
        /// a notification is skipped, so this never produces duplicates no matter how often or how
        /// many times it runs.</summary>
        Task CreateDueSoonAndOverdueNotificationsAsync();

        /// <summary>Called periodically by NotificationRetentionBackgroundService — bulk-deletes
        /// notifications older than the configured retention window. Never touches recent
        /// notifications regardless of read state (the spec's own "do not automatically delete
        /// recent notifications" instruction) — only age matters, not IsRead.</summary>
        Task PurgeExpiredAsync();
    }

    public class NotificationService(
        AppDbContext db, IEmailService emailService, ISystemSettingsService systemSettings, IConfiguration configuration) : INotificationService
    {
        private const int MaxPageSize = 100;

        private readonly AppDbContext _db = db;
        private readonly IEmailService _emailService = emailService;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly IConfiguration _configuration = configuration;

        public async Task NotifyAsync(
            Guid recipientUserId,
            Guid? actorUserId,
            string type,
            string title,
            string message,
            Guid? taskId = null,
            Guid? projectId = null,
            string? metadata = null,
            NotificationPriority? priority = null,
            Guid? commentId = null,
            Guid? fileId = null,
            string? actionUrl = null,
            Guid? eventId = null)
        {
            if (recipientUserId == actorUserId)
            {
                return;
            }

            var recipient = await _db.Users
                .Where(u => u.Id == recipientUserId)
                .Select(u => new { u.IsActive, u.Email, u.Name })
                .FirstOrDefaultAsync();
            if (recipient is null || !recipient.IsActive)
            {
                return;
            }

            if (eventId is Guid eid)
            {
                var alreadyRaised = await _db.Notifications
                    .AnyAsync(n => n.UserId == recipientUserId && n.Type == type && n.EventId == eid);
                if (alreadyRaised)
                {
                    return;
                }
            }

            var (inAppEnabled, emailEnabled) = await GetChannelSettingAsync(recipientUserId, type);
            var resolvedPriority = priority ?? NotificationPriorities.For(type);
            var resolvedActionUrl = actionUrl ?? DefaultActionUrl(taskId, projectId);

            if (inAppEnabled)
            {
                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = recipientUserId,
                    Type = type,
                    Title = title,
                    Message = message,
                    Priority = resolvedPriority,
                    ActorUserId = actorUserId,
                    TaskId = taskId,
                    ProjectId = projectId,
                    CommentId = commentId,
                    FileId = fileId,
                    ActionUrl = resolvedActionUrl,
                    EventId = eventId,
                    Metadata = metadata
                });
            }

            if (emailEnabled)
            {
                await MaybeSendEmailAsync(recipientUserId, recipient.Email, recipient.Name, resolvedPriority, title, message, resolvedActionUrl);
            }
        }

        public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetForUserAsync(
            Guid userId, bool? isRead, int page, int pageSize,
            string? search = null, string? type = null, NotificationPriority? priority = null,
            Guid? projectId = null, Guid? taskId = null, DateTime? dateFrom = null, DateTime? dateTo = null,
            IReadOnlyCollection<string>? types = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _db.Notifications.Include(n => n.ActorUser).Where(n => n.UserId == userId);
            if (isRead is bool read)
            {
                query = query.Where(n => n.IsRead == read);
            }
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(n => n.Type == type);
            }
            if (types is { Count: > 0 })
            {
                query = query.Where(n => types.Contains(n.Type));
            }
            if (priority is NotificationPriority p)
            {
                query = query.Where(n => n.Priority == p);
            }
            if (projectId is Guid pid)
            {
                query = query.Where(n => n.ProjectId == pid);
            }
            if (taskId is Guid tid)
            {
                query = query.Where(n => n.TaskId == tid);
            }
            if (dateFrom is DateTime from)
            {
                query = query.Where(n => n.CreatedAt >= from);
            }
            if (dateTo is DateTime to)
            {
                query = query.Where(n => n.CreatedAt < to);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Notification text/task/project names only — never a cross-user search (query is
                // already scoped to this caller's own UserId above), satisfying the spec's own
                // "search must only return notifications belonging to the current user" rule.
                query = query.Where(n =>
                    EF.Functions.ILike(n.Title, $"%{search}%") ||
                    EF.Functions.ILike(n.Message, $"%{search}%") ||
                    (n.Task != null && EF.Functions.ILike(n.Task.Title, $"%{search}%")) ||
                    (n.Project != null && EF.Functions.ILike(n.Project.Name, $"%{search}%")) ||
                    (n.ActorUser != null && EF.Functions.ILike(n.ActorUser.Name, $"%{search}%")));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public Task<Notification> GetByIdAsync(Guid notificationId, Guid userId) => LoadOwnNotificationAsync(notificationId, userId);

        public Task<int> GetUnreadCountAsync(Guid userId) =>
            _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task MarkReadAsync(Guid notificationId, Guid userId)
        {
            var notification = await LoadOwnNotificationAsync(notificationId, userId);
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        public async Task MarkUnreadAsync(Guid notificationId, Guid userId)
        {
            var notification = await LoadOwnNotificationAsync(notificationId, userId);
            if (notification.IsRead)
            {
                notification.IsRead = false;
                notification.ReadAt = null;
                await _db.SaveChangesAsync();
            }
        }

        public async Task MarkAllReadAsync(Guid userId)
        {
            var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            var now = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
            }
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid notificationId, Guid userId)
        {
            var notification = await LoadOwnNotificationAsync(notificationId, userId);
            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync();
        }

        public async Task BulkMarkReadAsync(IReadOnlyList<Guid> ids, Guid userId)
        {
            var now = DateTime.UtcNow;
            await _db.Notifications
                .Where(n => n.UserId == userId && ids.Contains(n.Id) && !n.IsRead)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now));
        }

        public async Task BulkDeleteAsync(IReadOnlyList<Guid> ids, Guid userId)
        {
            await _db.Notifications
                .Where(n => n.UserId == userId && ids.Contains(n.Id))
                .ExecuteDeleteAsync();
        }

        public async Task CreateDueSoonAndOverdueNotificationsAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var dueSoonThreshold = today.AddDays(1);
            var maxBatch = await _systemSettings.GetIntAsync(SystemSettingKeys.MaxNotificationBatchSize);

            var candidates = await _db.Tasks
                .Where(t =>
                    t.AssignedToUserId != null &&
                    t.Status != TaskItemStatus.Complete &&
                    t.Status != TaskItemStatus.Cancelled &&
                    t.DueDate != null &&
                    t.DueDate <= dueSoonThreshold)
                .OrderBy(t => t.DueDate)
                .Take(maxBatch)
                .Select(t => new
                {
                    t.Id, t.Title, t.ProjectId, t.ParentTaskId,
                    AssignedToUserId = t.AssignedToUserId!.Value, DueDate = t.DueDate!.Value
                })
                .ToListAsync();

            foreach (var task in candidates)
            {
                var type = task.DueDate < today ? Common.NotificationTypes.TaskOverdue : Common.NotificationTypes.TaskDueSoon;

                var alreadyNotified = await _db.Notifications.AnyAsync(n =>
                    n.UserId == task.AssignedToUserId && n.TaskId == task.Id && n.Type == type);
                if (alreadyNotified)
                {
                    continue;
                }

                var noun = task.ParentTaskId is null ? "Task" : "Subtask";
                var (title, message) = type == Common.NotificationTypes.TaskOverdue
                    ? ($"{noun} overdue", $"\"{task.Title}\" is overdue.")
                    : ($"{noun} due soon", $"\"{task.Title}\" is due soon.");

                await NotifyAsync(task.AssignedToUserId, actorUserId: null, type, title, message, task.Id, task.ProjectId);
            }

            await _db.SaveChangesAsync();
        }

        public async Task PurgeExpiredAsync()
        {
            var retentionDays = await _systemSettings.GetIntAsync(SystemSettingKeys.NotificationRetentionDays);
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            await _db.Notifications.Where(n => n.CreatedAt < cutoff).ExecuteDeleteAsync();
        }

        private async Task<Notification> LoadOwnNotificationAsync(Guid notificationId, Guid userId)
        {
            // Scoped by UserId in the same query, not loaded then permission-checked — a
            // mismatch (wrong owner, or the id doesn't exist at all) is indistinguishable 404
            // either way, so a caller can't use this to probe whether some other user's
            // notification id exists.
            var notification = await _db.Notifications.Include(n => n.ActorUser)
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            return notification ?? throw new NotFoundException($"Notification '{notificationId}' was not found.");
        }

        /// <summary>Reads the same UserNotificationSettings table UserNotificationSettingsService
        /// owns (never a second, parallel preference store) — a narrow single-type query instead
        /// of that service's GetAllAsync, since NotifyAsync only ever needs one type's value and
        /// GetAllAsync always materializes every known type.</summary>
        private async Task<(bool InApp, bool Email)> GetChannelSettingAsync(Guid userId, string type)
        {
            var setting = await _db.UserNotificationSettings
                .Where(s => s.UserId == userId && s.NotificationType == type)
                .Select(s => new { s.InAppEnabled, s.EmailEnabled })
                .FirstOrDefaultAsync();

            return setting is null ? (true, true) : (setting.InAppEnabled, setting.EmailEnabled);
        }

        private static string? DefaultActionUrl(Guid? taskId, Guid? projectId)
        {
            if (taskId is Guid t && projectId is Guid p)
            {
                return $"/projects/{p}?task={t}";
            }
            if (projectId is Guid onlyProject)
            {
                return $"/projects/{onlyProject}";
            }
            return null;
        }

        /// <summary>The email side of NotifyAsync — gated independently by (in order) whether
        /// SMTP is even configured, the admin's global Notifications.EmailNotificationsEnabled
        /// switch, this user's DigestFrequency (Daily batches non-Urgent email into the digest
        /// instead — see UserPreference.DigestFrequency's own doc comment; Never sends none), and
        /// finally quiet hours (suppresses email only — the in-app row above is unaffected either
        /// way, satisfying the spec's "do not lose notifications" rule).</summary>
        private async Task MaybeSendEmailAsync(
            Guid userId, string email, string name, NotificationPriority priority, string title, string message, string? actionUrl)
        {
            if (!_emailService.IsConfigured)
            {
                return;
            }
            if (!await _systemSettings.GetBoolAsync(SystemSettingKeys.EmailNotificationsEnabled))
            {
                return;
            }

            var preference = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
            if (preference?.DigestFrequency == "Never")
            {
                return;
            }
            if (preference?.DigestFrequency == "Daily" && priority != NotificationPriority.Urgent)
            {
                // Batched into the next daily digest instead — see
                // NotificationDigestBackgroundService, and "do not send duplicate emails when
                // immediate notification is enabled" (the inverse: Daily users don't also get
                // the immediate one).
                return;
            }
            if (preference is not null && preference.QuietHoursEnabled && IsWithinQuietHours(preference, DateTime.UtcNow))
            {
                return;
            }

            var appName = await _systemSettings.GetStringAsync(SystemSettingKeys.ApplicationName);
            var appBaseUrl = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault() ?? "";
            var (subject, html) = NotificationTemplates.ForNotification(title, message, actionUrl, appBaseUrl, appName);
            await _emailService.SendAsync(email, name, subject, html);
        }

        /// <summary>Interpreted in the user's own TimeZone, not UTC/server time — handles the
        /// overnight wrap-around case (e.g. 8 PM to 7 AM) the same way any "is it currently
        /// between X and Y" check spanning midnight must.</summary>
        internal static bool IsWithinQuietHours(UserPreference preference, DateTime utcNow)
        {
            if (preference.QuietHoursStart is not TimeOnly start || preference.QuietHoursEnd is not TimeOnly end)
            {
                return false;
            }

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZone);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                timeZone = TimeZoneInfo.Utc;
            }

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            var localTime = TimeOnly.FromDateTime(localNow);

            return start <= end
                ? localTime >= start && localTime < end
                : localTime >= start || localTime < end;
        }
    }
}

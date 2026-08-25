using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// The single reusable entry point every other service calls to raise a notification —
        /// centralizes the rules that would otherwise be duplicated at every call site:
        /// never notify a user about their own action, never notify a deactivated/deleted user,
        /// and respect that recipient's UserNotificationSetting for this type (defaulting to
        /// enabled, same as UserNotificationSettingsService).
        ///
        /// Deliberately does NOT call SaveChangesAsync — this only adds to the AppDbContext
        /// change tracker, so the caller's own SaveChangesAsync (already happening right after
        /// the task/comment/membership change it's reporting on) persists both atomically. A
        /// notification is never created for a change that then fails to save, because it's
        /// added to the very same unit of work as that change.
        /// </summary>
        Task NotifyAsync(
            Guid recipientUserId,
            Guid? actorUserId,
            string type,
            string title,
            string message,
            Guid? taskId = null,
            Guid? projectId = null,
            string? metadata = null);

        /// <param name="isRead">Null returns both read and unread.</param>
        Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetForUserAsync(Guid userId, bool? isRead, int page, int pageSize);

        Task<int> GetUnreadCountAsync(Guid userId);

        Task MarkReadAsync(Guid notificationId, Guid userId);

        Task MarkUnreadAsync(Guid notificationId, Guid userId);

        Task MarkAllReadAsync(Guid userId);

        Task DeleteAsync(Guid notificationId, Guid userId);

        /// <summary>Called periodically by DueDateNotificationBackgroundService — scans assigned,
        /// still-open tasks for ones due today/tomorrow or already overdue, and raises TaskDueSoon
        /// / TaskOverdue notifications. Safe to call repeatedly: a task+user+type that already has
        /// a notification is skipped, so this never produces duplicates no matter how often or how
        /// many times it runs.</summary>
        Task CreateDueSoonAndOverdueNotificationsAsync();
    }

    public class NotificationService(AppDbContext db) : INotificationService
    {
        private const int MaxPageSize = 100;

        private readonly AppDbContext _db = db;

        public async Task NotifyAsync(
            Guid recipientUserId,
            Guid? actorUserId,
            string type,
            string title,
            string message,
            Guid? taskId = null,
            Guid? projectId = null,
            string? metadata = null)
        {
            if (recipientUserId == actorUserId)
            {
                return;
            }

            var recipientIsActive = await _db.Users
                .Where(u => u.Id == recipientUserId)
                .Select(u => u.IsActive)
                .FirstOrDefaultAsync();
            if (!recipientIsActive)
            {
                return;
            }

            if (!await IsTypeEnabledAsync(recipientUserId, type))
            {
                return;
            }

            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipientUserId,
                Type = type,
                Title = title,
                Message = message,
                TaskId = taskId,
                ProjectId = projectId,
                Metadata = metadata
            });
        }

        public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetForUserAsync(Guid userId, bool? isRead, int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _db.Notifications.Where(n => n.UserId == userId);
            if (isRead is bool read)
            {
                query = query.Where(n => n.IsRead == read);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

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

        public async Task CreateDueSoonAndOverdueNotificationsAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var dueSoonThreshold = today.AddDays(1);

            var candidates = await _db.Tasks
                .Where(t =>
                    t.AssignedToUserId != null &&
                    t.Status != Models.Entities.Enums.TaskItemStatus.Complete &&
                    t.Status != Models.Entities.Enums.TaskItemStatus.Cancelled &&
                    t.DueDate != null &&
                    t.DueDate <= dueSoonThreshold)
                .Select(t => new { t.Id, t.Title, t.ProjectId, AssignedToUserId = t.AssignedToUserId!.Value, DueDate = t.DueDate!.Value })
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

                var (title, message) = type == Common.NotificationTypes.TaskOverdue
                    ? ("Task overdue", $"\"{task.Title}\" is overdue.")
                    : ("Task due soon", $"\"{task.Title}\" is due soon.");

                await NotifyAsync(task.AssignedToUserId, actorUserId: null, type, title, message, task.Id, task.ProjectId);
            }

            await _db.SaveChangesAsync();
        }

        private async Task<Notification> LoadOwnNotificationAsync(Guid notificationId, Guid userId)
        {
            // Scoped by UserId in the same query, not loaded then permission-checked — a
            // mismatch (wrong owner, or the id doesn't exist at all) is indistinguishable 404
            // either way, so a caller can't use this to probe whether some other user's
            // notification id exists.
            var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            return notification ?? throw new NotFoundException($"Notification '{notificationId}' was not found.");
        }

        /// <summary>Reads the same UserNotificationSettings table UserNotificationSettingsService
        /// owns (never a second, parallel preference store) — a narrow single-type query instead
        /// of that service's GetAllAsync, since NotifyAsync only ever needs one type's value and
        /// GetAllAsync always materializes all twelve.</summary>
        private async Task<bool> IsTypeEnabledAsync(Guid userId, string type)
        {
            var setting = await _db.UserNotificationSettings
                .Where(s => s.UserId == userId && s.NotificationType == type)
                .Select(s => (bool?)s.IsEnabled)
                .FirstOrDefaultAsync();

            return setting ?? true;
        }
    }
}

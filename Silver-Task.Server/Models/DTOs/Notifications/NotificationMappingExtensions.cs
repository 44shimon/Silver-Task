using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Notifications
{
    public static class NotificationMappingExtensions
    {
        public static NotificationDto ToDto(this Notification notification) => new()
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Priority = notification.Priority.ToString(),
            Actor = notification.ActorUser?.ToSummaryDto(),
            TaskId = notification.TaskId,
            ProjectId = notification.ProjectId,
            CommentId = notification.CommentId,
            FileId = notification.FileId,
            ActionUrl = ResolveActionUrl(notification),
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };

        /// <summary>Recomputed from the notification's *current* TaskId/ProjectId rather than
        /// trusting the stored ActionUrl snapshot verbatim — the stored value is set once at
        /// creation time, but TaskId/ProjectId can independently go null later (SetNull-on-delete,
        /// see NotificationConfiguration) without the stored string being updated to match. Without
        /// this, a notification about a since-deleted task would keep pointing at a stale
        /// "?task={deletedId}" link instead of correctly degrading to "no destination" (or falling
        /// back to the project alone) — see the spec's own "do not expose deleted/private
        /// information incorrectly" requirement. Only ever recomputes the task/project pattern
        /// every current call site actually produces; a future call site with a genuinely
        /// different (non task/project) ActionUrl would fall through to the stored value.</summary>
        private static string? ResolveActionUrl(Notification notification)
        {
            if (notification.TaskId is Guid taskId && notification.ProjectId is Guid projectId)
            {
                return $"/projects/{projectId}?task={taskId}";
            }
            if (notification.ProjectId is Guid onlyProjectId)
            {
                return $"/projects/{onlyProjectId}";
            }
            if (notification.TaskId is null && notification.ProjectId is null && notification.ActionUrl is not null &&
                notification.ActionUrl.StartsWith("/projects/", StringComparison.Ordinal))
            {
                // The stored link was a task/project route and both ids are now gone — the
                // resource no longer exists, so there is genuinely nothing left to link to.
                return null;
            }
            return notification.ActionUrl;
        }
    }
}

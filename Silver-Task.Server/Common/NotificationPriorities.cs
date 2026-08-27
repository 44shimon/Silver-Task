using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Common
{
    /// <summary>Per-type default priority (Phase 36) — centralized here so every call site that
    /// raises a notification gets a sensible priority automatically (NotificationService.NotifyAsync
    /// resolves this whenever the caller doesn't pass one explicitly) rather than every one of the
    /// ~15 call sites across TaskService/CommentService/ProjectService/etc. having to know and
    /// repeat it. Mentions are deliberately Important (higher than plain CommentAdded, per the
    /// spec's own "mentions should be higher priority than ordinary comments" rule) and overdue is
    /// the only Urgent type — matching the spec's explicit examples and its "do not overuse urgent"
    /// instruction.</summary>
    public static class NotificationPriorities
    {
        private static readonly Dictionary<string, NotificationPriority> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            [NotificationTypes.TaskAssigned] = NotificationPriority.Important,
            [NotificationTypes.TaskReassigned] = NotificationPriority.Important,
            [NotificationTypes.TaskUnassigned] = NotificationPriority.Normal,
            [NotificationTypes.TaskStatusChanged] = NotificationPriority.Normal,
            [NotificationTypes.TaskPriorityChanged] = NotificationPriority.Normal,
            [NotificationTypes.TaskDueDateChanged] = NotificationPriority.Normal,
            [NotificationTypes.TaskDueSoon] = NotificationPriority.Important,
            [NotificationTypes.TaskOverdue] = NotificationPriority.Urgent,
            [NotificationTypes.TaskCompleted] = NotificationPriority.Important,
            [NotificationTypes.TaskReopened] = NotificationPriority.Important,
            [NotificationTypes.CommentAdded] = NotificationPriority.Normal,
            [NotificationTypes.MentionedInComment] = NotificationPriority.Important,
            [NotificationTypes.UserAddedToProject] = NotificationPriority.Normal,
            [NotificationTypes.UserRemovedFromProject] = NotificationPriority.Normal,
            [NotificationTypes.ProjectTaskCompleted] = NotificationPriority.Normal,
            [NotificationTypes.TaskDependencyCompleted] = NotificationPriority.Normal,
            [NotificationTypes.RecurringTaskAssigneeInactive] = NotificationPriority.Important,
            [NotificationTypes.ProjectRoleChanged] = NotificationPriority.Important,
            [NotificationTypes.SystemRoleChanged] = NotificationPriority.Important,
            [NotificationTypes.AutomationNotification] = NotificationPriority.Normal,
            [NotificationTypes.FileUploaded] = NotificationPriority.Normal,
            [NotificationTypes.ProjectStatusChanged] = NotificationPriority.Normal,
        };

        public static NotificationPriority For(string type) => Map.TryGetValue(type, out var priority) ? priority : NotificationPriority.Normal;
    }
}

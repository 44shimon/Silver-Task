namespace Silver_Task.Server.Common
{
    /// <summary>The notification center's coarse tab groupings (All/Unread/Mentions/Tasks/
    /// Projects/Files/Automations/System) — several NotificationTypes fold into one category
    /// (e.g. every Task* type is the "tasks" tab), so this can't be expressed as a single-value
    /// `type` filter; NotificationsController's `category` query param resolves to this set of
    /// types instead. Mirrors the frontend's own categoryOf() (types/notification.ts) exactly —
    /// keep both in sync if a new NotificationType is added.</summary>
    public static class NotificationCategories
    {
        private static readonly HashSet<string> TaskTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            NotificationTypes.TaskAssigned, NotificationTypes.TaskReassigned, NotificationTypes.TaskUnassigned,
            NotificationTypes.TaskStatusChanged, NotificationTypes.TaskPriorityChanged, NotificationTypes.TaskDueDateChanged,
            NotificationTypes.TaskDueSoon, NotificationTypes.TaskOverdue, NotificationTypes.TaskCompleted,
            NotificationTypes.TaskReopened, NotificationTypes.CommentAdded, NotificationTypes.ProjectTaskCompleted,
            NotificationTypes.TaskDependencyCompleted, NotificationTypes.RecurringTaskAssigneeInactive
        };

        private static readonly HashSet<string> ProjectTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            NotificationTypes.UserAddedToProject, NotificationTypes.UserRemovedFromProject,
            NotificationTypes.ProjectStatusChanged, NotificationTypes.ProjectRoleChanged
        };

        private static readonly HashSet<string> SystemTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            NotificationTypes.SystemRoleChanged
        };

        /// <summary>Null means "no category filter" (the caller passed an unrecognized/absent
        /// value) — the controller ignores the param entirely in that case, same as any other
        /// unrecognized filter value elsewhere in this app.</summary>
        public static IReadOnlyCollection<string>? Resolve(string? category) => category?.ToLowerInvariant() switch
        {
            "mentions" => [NotificationTypes.MentionedInComment],
            "files" => [NotificationTypes.FileUploaded],
            "automations" => [NotificationTypes.AutomationNotification],
            "system" => SystemTypes,
            "tasks" => TaskTypes,
            "projects" => ProjectTypes,
            _ => null
        };
    }
}

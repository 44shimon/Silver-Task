namespace Silver_Task.Server.Common
{
    /// <summary>The known notification types, as plain strings rather than a C# enum, matching
    /// how CustomFieldType is stored as text so new field types never need a migration — adding
    /// a notification type later is just adding a line to `All` (and something that actually
    /// raises it), not a schema change.</summary>
    public static class NotificationTypes
    {
        public const string TaskAssigned = "TaskAssigned";
        public const string TaskReassigned = "TaskReassigned";
        public const string TaskStatusChanged = "TaskStatusChanged";
        public const string TaskPriorityChanged = "TaskPriorityChanged";
        public const string TaskDueDateChanged = "TaskDueDateChanged";
        public const string TaskDueSoon = "TaskDueSoon";
        public const string TaskOverdue = "TaskOverdue";
        public const string CommentAdded = "CommentAdded";
        public const string MentionedInComment = "MentionedInComment";
        public const string UserAddedToProject = "UserAddedToProject";
        public const string UserRemovedFromProject = "UserRemovedFromProject";
        public const string ProjectTaskCompleted = "ProjectTaskCompleted";

        /// <summary>Phase 29 — fires for a dependent task's assignee when its last remaining
        /// incomplete prerequisite (Finish-to-Start) is completed, i.e. the moment the task
        /// actually becomes unblocked, not on every prerequisite completion if others still block it.</summary>
        public const string TaskDependencyCompleted = "TaskDependencyCompleted";

        /// <summary>Phase 31 — fires (to the recurring task's creator, not "all admins" — this app
        /// has no broadcast-to-admins notification pattern) when a newly generated occurrence
        /// can't be auto-assigned because the configured assignee is no longer an active project
        /// member. Deliberately distinct from TaskAssigned: this is "please look at this", not a
        /// per-occurrence assignment notice — the occurrence itself generates zero assignment
        /// notification in this case, since nobody was actually assigned.</summary>
        public const string RecurringTaskAssigneeInactive = "RecurringTaskAssigneeInactive";

        /// <summary>Phase 32 — fires when a member's per-project role (Manager/Member/Viewer)
        /// changes, doubling as the closest existing analog to a security-change audit trail (see
        /// ProjectService.SetMemberRoleAsync's own doc comment on why a second, dedicated audit
        /// log wasn't added for this phase).</summary>
        public const string ProjectRoleChanged = "ProjectRoleChanged";

        /// <summary>Phase 32 — fires when an Administrator changes another user's system-wide
        /// role. Same audit-trail rationale as ProjectRoleChanged.</summary>
        public const string SystemRoleChanged = "SystemRoleChanged";

        /// <summary>Phase 35 — the "Send Notification" automation action's own notification type,
        /// covering every automation regardless of which one sent it (the title always includes
        /// the specific automation's name, so this one type is enough to distinguish them in the
        /// notification feed without a type-per-automation explosion).</summary>
        public const string AutomationNotification = "AutomationNotification";

        /// <summary>Phase 36 — fires when a task's assignee is cleared (set to null) without a
        /// replacement; distinct from TaskReassigned (which fires for the *new* assignee when one
        /// is set in the same edit). Notifies the person who was removed, not anyone else.</summary>
        public const string TaskUnassigned = "TaskUnassigned";

        /// <summary>Phase 36 — fires for the task's own assignee when it's marked Complete,
        /// distinct from ProjectTaskCompleted (which notifies the *project owner*, not the
        /// assignee, and already existed before this phase). Deliberately not merged with it: an
        /// assignee and a project owner are different audiences who may want this independently
        /// controlled via their own notification preference.</summary>
        public const string TaskCompleted = "TaskCompleted";

        /// <summary>Phase 36 — fires for the task's assignee when a previously-Complete task is
        /// reopened (status changed away from Complete).</summary>
        public const string TaskReopened = "TaskReopened";

        /// <summary>Phase 36 — fires when a file is uploaded to a task (notifies the assignee) or
        /// a project (notifies the owner) — deliberately not "every project member for every
        /// file" per the spec's own "do not notify everyone unless configured" instruction.</summary>
        public const string FileUploaded = "FileUploaded";

        /// <summary>Phase 36 — fires when a project is archived or restored, notifying every
        /// project member (a project-wide event, unlike a per-task change).</summary>
        public const string ProjectStatusChanged = "ProjectStatusChanged";

        /// <summary>Phase 39 — fires for a task's assignee the moment a newly-added dependency
        /// immediately blocks it (the prerequisite doesn't yet satisfy the relationship type).
        /// Distinct from TaskDependencyCompleted (Phase 29), which is the "no longer blocked"
        /// counterpart.</summary>
        public const string TaskBecameBlocked = "TaskBecameBlocked";

        public static readonly IReadOnlyList<string> All =
        [
            TaskAssigned,
            TaskReassigned,
            TaskStatusChanged,
            TaskPriorityChanged,
            TaskDueDateChanged,
            TaskDueSoon,
            TaskOverdue,
            CommentAdded,
            MentionedInComment,
            UserAddedToProject,
            UserRemovedFromProject,
            ProjectTaskCompleted,
            TaskDependencyCompleted,
            RecurringTaskAssigneeInactive,
            ProjectRoleChanged,
            SystemRoleChanged,
            AutomationNotification,
            TaskUnassigned,
            TaskCompleted,
            TaskReopened,
            FileUploaded,
            ProjectStatusChanged,
            TaskBecameBlocked
        ];
    }
}

namespace Silver_Task.Server.Common
{
    /// <summary>The known notification types, as plain strings rather than a C# enum, matching
    /// how CustomFieldType is stored as text so new field types never need a migration — adding
    /// a notification type later is just adding a line to `All` (and something that actually
    /// raises it), not a schema change.</summary>
    public static class NotificationTypes
    {
        public const string TaskAssigned = "TaskAssigned";
        public const string TaskStatusChanged = "TaskStatusChanged";
        public const string TaskDueSoon = "TaskDueSoon";
        public const string TaskOverdue = "TaskOverdue";
        public const string CommentAdded = "CommentAdded";
        public const string MentionedInComment = "MentionedInComment";

        public static readonly IReadOnlyList<string> All =
        [
            TaskAssigned,
            TaskStatusChanged,
            TaskDueSoon,
            TaskOverdue,
            CommentAdded,
            MentionedInComment
        ];
    }
}

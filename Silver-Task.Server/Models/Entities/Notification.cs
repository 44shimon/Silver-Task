namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A single in-app notification for one user. Deliberately not an EAV table like
    /// CustomFields/SystemSettings — Type is still free text (see Common.NotificationTypes) so a
    /// new type never needs a migration, but the row shape itself (Title/Message/TaskId/
    /// ProjectId/read state) is fixed because every notification genuinely has all of these
    /// fields, unlike a custom field's per-type-varying value.</summary>
    public class Notification
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Type { get; set; }

        public required string Title { get; set; }

        public required string Message { get; set; }

        /// <summary>Nullable and SetNull-on-delete — a task can be deleted after a notification
        /// about it was created; the notification's Title/Message already carry the human-
        /// readable text, so losing the link just means "open task" degrades gracefully instead
        /// of the notification itself being destroyed.</summary>
        public Guid? TaskId { get; set; }

        public Guid? ProjectId { get; set; }

        /// <summary>Small JSON blob for anything a notification type needs beyond Title/Message
        /// (e.g. the old/new value for a field-change notification) — optional, most types don't
        /// need it since the message text already says everything relevant.</summary>
        public string? Metadata { get; set; }

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public TaskItem? Task { get; set; }

        public Project? Project { get; set; }
    }
}

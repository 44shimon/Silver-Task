namespace Silver_Task.Server.Models.Entities
{
    public class TaskComment
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public Guid UserId { get; set; }

        public required string Text { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        /// <summary>True for a comment an Automation's "Add Comment" action posted (Phase 35) —
        /// the comment is still authored by the automation's CreatedByUserId (UserId above), so it
        /// participates in ownership/edit rules identically to a normal comment; this flag only
        /// drives the "⚙ Automation" badge the frontend shows, per the spec's "clearly indicate
        /// automated comments" requirement.</summary>
        public bool IsAutomated { get; set; }

        public Guid? AutomationId { get; set; }

        public TaskItem? Task { get; set; }

        public Automation? Automation { get; set; }

        public User? User { get; set; }

        public ICollection<Attachment> Attachments { get; set; } = [];
    }
}

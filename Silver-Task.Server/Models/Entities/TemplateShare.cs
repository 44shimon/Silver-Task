namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Explicit user-to-user share of either template type — exactly one of
    /// ProjectTemplateId/TaskTemplateId is set (DB check constraint), same polymorphic-parent
    /// pattern Attachment already established for ProjectId/TaskId/CommentId (Phase 33). Mirrors
    /// SavedReportShare's own email-based, explicit-user-only sharing model (Phase 38) rather than
    /// bulk project/role sharing — a disclosed scope cut, see the Phase 40 final report.</summary>
    public class TemplateShare
    {
        public Guid Id { get; set; }

        public Guid? ProjectTemplateId { get; set; }

        public Guid? TaskTemplateId { get; set; }

        public Guid SharedWithUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public ProjectTemplate? ProjectTemplate { get; set; }

        public TaskTemplate? TaskTemplate { get; set; }

        public User? SharedWithUser { get; set; }
    }
}

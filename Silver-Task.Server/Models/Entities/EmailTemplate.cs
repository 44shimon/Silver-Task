namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 45 — an Administrator's override of one notification type's default email
    /// copy (Common.DefaultEmailTemplates). One row per NotificationType (unique), every field
    /// nullable — a null field means "still use the built-in default for that field", so an
    /// admin can, say, only change the CtaText without having to re-author the whole body. Every
    /// field is presentation-only free text rendered through EmailTemplateService's controlled
    /// {{Variable}} substitution (Common.EmailTemplateVariables' fixed allow-list) — there is no
    /// code-execution path from this table to the rendered email.</summary>
    public class EmailTemplate
    {
        public Guid Id { get; set; }

        public required string NotificationType { get; set; }

        public string? SubjectTemplate { get; set; }

        public string? HeadingTemplate { get; set; }

        public string? BodyTemplate { get; set; }

        public string? CtaText { get; set; }

        public string? FooterTemplate { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Guid? UpdatedByUserId { get; set; }

        public User? UpdatedByUser { get; set; }
    }
}

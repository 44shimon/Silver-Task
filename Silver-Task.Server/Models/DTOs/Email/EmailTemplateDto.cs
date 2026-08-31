namespace Silver_Task.Server.Models.DTOs.Email
{
    /// <summary>Merges an EmailTemplate override (if any) with its DefaultEmailTemplates.ByType
    /// fallback text — DefaultSubject/DefaultHeading/etc. let the admin editor show placeholder
    /// text for any field with no override, and IsCustomized tells the UI whether "Reset to
    /// Default" has anything to do.</summary>
    public class EmailTemplateDto
    {
        public required string NotificationType { get; set; }

        public string? SubjectTemplate { get; set; }

        public string? HeadingTemplate { get; set; }

        public string? BodyTemplate { get; set; }

        public string? CtaText { get; set; }

        public string? FooterTemplate { get; set; }

        public required string DefaultSubject { get; set; }

        public required string DefaultHeading { get; set; }

        public required string DefaultBody { get; set; }

        public required string DefaultCtaText { get; set; }

        public bool IsCustomized { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UpdatedByName { get; set; }
    }
}

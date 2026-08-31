namespace Silver_Task.Server.Models.DTOs.Email
{
    /// <summary>A null/blank field means "use the default for this field" — see
    /// EmailTemplateService.UpsertAsync's NullIfBlank normalization.</summary>
    public class UpsertEmailTemplateRequest
    {
        public string? SubjectTemplate { get; set; }

        public string? HeadingTemplate { get; set; }

        public string? BodyTemplate { get; set; }

        public string? CtaText { get; set; }

        public string? FooterTemplate { get; set; }
    }
}

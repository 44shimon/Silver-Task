namespace Silver_Task.Server.Models.DTOs.Email
{
    /// <summary>Render-only response for the admin "Preview" action — sample data, never sent.
    /// See EmailTemplateService.Render / IEmailTemplateService.RenderEmailAsync.</summary>
    public class EmailTemplatePreviewDto
    {
        public required string Subject { get; set; }

        public required string HtmlBody { get; set; }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Email;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Phase 45 — Administrator-only email configuration, templates, and delivery log.
    /// Every action here matches AdminSettingsController's own [Authorize(Roles=Administrator)]
    /// pattern; nothing on this controller is reachable by an ordinary Member/Manager (spec's own
    /// "most email notification operations should be internal" / "only authorized administrators
    /// may send test emails or modify templates" requirements).</summary>
    [ApiController]
    [Route("api/admin/email")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminEmailController(
        IEmailService emailService,
        IEmailTemplateService templateService,
        IEmailDeliveryService deliveryService,
        ISystemSettingsService systemSettings,
        IConfiguration configuration) : ControllerBase
    {
        private readonly IEmailService _emailService = emailService;
        private readonly IEmailTemplateService _templateService = templateService;
        private readonly IEmailDeliveryService _deliveryService = deliveryService;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly IConfiguration _configuration = configuration;

        /// <summary>Whether SMTP is configured — never the host/port/username/etc. themselves
        /// (those only ever live in appsettings/user-secrets/environment variables, never
        /// surfaced through the API — see EmailService's own doc comment).</summary>
        [HttpGet("status")]
        public ActionResult<object> GetStatus() => Ok(new { isConfigured = _emailService.IsConfigured });

        [HttpPost("test")]
        public async Task<ActionResult<TestEmailResultDto>> SendTest([FromBody] TestEmailRequest request)
        {
            if (!_emailService.IsConfigured)
            {
                return Ok(new TestEmailResultDto { Success = false, Message = "Email is not configured." });
            }

            var appName = await _systemSettings.GetStringAsync(SystemSettingKeys.ApplicationName);
            var appBaseUrl = await AppUrlResolver.ResolveAsync(_systemSettings, _configuration);
            var (subject, html) = NotificationTemplates.RenderCard(
                appName, appBaseUrl,
                subject: $"{appName} — Test Email",
                heading: "Test Email",
                body: "This is a test email from Silver Task. If you received this, outgoing email delivery is configured correctly.",
                ctaText: null, actionUrl: null, footerText: null);

            var result = await _emailService.SendAsync(request.ToEmail, request.ToEmail, subject, html);
            return Ok(new TestEmailResultDto
            {
                Success = result.Success,
                Message = result.Success ? "Test email sent." : "Email delivery failed. Check server logs for details."
            });
        }

        [HttpGet("templates")]
        public async Task<ActionResult<IReadOnlyList<EmailTemplateDto>>> GetTemplates()
        {
            var templates = await _templateService.GetAllAsync();
            return Ok(templates.Select(t => t.ToDto()));
        }

        [HttpPut("templates/{notificationType}")]
        public async Task<ActionResult<EmailTemplateDto>> UpsertTemplate(string notificationType, [FromBody] UpsertEmailTemplateRequest request)
        {
            var template = await _templateService.UpsertAsync(User.GetUserId(), notificationType, new Models.Entities.EmailTemplate
            {
                NotificationType = notificationType,
                SubjectTemplate = request.SubjectTemplate,
                HeadingTemplate = request.HeadingTemplate,
                BodyTemplate = request.BodyTemplate,
                CtaText = request.CtaText,
                FooterTemplate = request.FooterTemplate
            });
            return Ok(template.ToDto());
        }

        [HttpPost("templates/{notificationType}/reset")]
        public async Task<IActionResult> ResetTemplate(string notificationType)
        {
            await _templateService.ResetAsync(notificationType);
            return NoContent();
        }

        [HttpPost("templates/{notificationType}/preview")]
        public async Task<ActionResult<EmailTemplatePreviewDto>> PreviewTemplate(string notificationType)
        {
            var appName = await _systemSettings.GetStringAsync(SystemSettingKeys.ApplicationName);
            var appBaseUrl = await AppUrlResolver.ResolveAsync(_systemSettings, _configuration);

            if (DefaultDigestTemplates.ByType.ContainsKey(notificationType))
            {
                var sampleDigestVariables = new DigestTemplateVariables(
                    UserName: "Jane Doe", DigestDate: DateTime.UtcNow.ToString("MM/dd/yyyy"),
                    AssignmentCount: 3, MentionCount: 2, CommentCount: 5, DueTodayCount: 2, OverdueCount: 1,
                    ActionUrl: "/notifications");
                var sampleContentHtml =
                    """<h3 style="font-size:13px;margin:16px 0 6px">ASSIGNMENTS</h3><ul style="margin:0;padding-left:18px;font-size:13px"><li>Complete Final Inspection &mdash; 123 Main Street Renovation</li><li>Submit Permit Application &mdash; 123 Main Street Renovation</li></ul>""";

                var (digestSubject, digestHtml) = await _templateService.RenderDigestAsync(
                    notificationType, sampleDigestVariables, sampleContentHtml, appName, appBaseUrl);
                return Ok(new EmailTemplatePreviewDto { Subject = digestSubject, HtmlBody = digestHtml });
            }

            if (!DefaultEmailTemplates.ByType.ContainsKey(notificationType))
            {
                throw new ValidationException($"'{notificationType}' does not support a custom email template.");
            }

            var sampleVariables = new EmailTemplateVariables(
                UserName: "Jane Doe",
                ActorName: "John Smith",
                TaskName: "Complete Final Inspection",
                ProjectName: "123 Main Street Renovation",
                DueDate: DateTime.UtcNow.AddDays(3).ToString("MM/dd/yyyy"),
                ActionUrl: "/projects/00000000-0000-0000-0000-000000000000?task=00000000-0000-0000-0000-000000000000");

            var (subject, html) = await _templateService.RenderEmailAsync(
                notificationType, sampleVariables, title: "Sample notification", message: "Sample notification body.", appName, appBaseUrl);

            return Ok(new EmailTemplatePreviewDto { Subject = subject, HtmlBody = html });
        }

        [HttpGet("deliveries")]
        public async Task<ActionResult<EmailDeliveryPageDto>> GetDeliveries([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            var (items, totalCount) = await _deliveryService.GetDeliveryLogAsync(page, pageSize);
            return Ok(new EmailDeliveryPageDto
            {
                Items = items.Select(d => d.ToDto()).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
    }
}

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    public interface IEmailTemplateService
    {
        /// <summary>Every customizable type (Common.DefaultEmailTemplates.ByType's keys), each
        /// merged with its stored override row if one exists — this is what backs the admin
        /// Email Templates list/editor, not a raw dump of the EmailTemplates table (a type with
        /// no override row still needs to show up, carrying its built-in default text).</summary>
        Task<IReadOnlyList<EmailTemplate>> GetAllAsync();

        Task<EmailTemplate?> GetOverrideAsync(string notificationType);

        Task<EmailTemplate> UpsertAsync(Guid updatedByUserId, string notificationType, EmailTemplate fields);

        Task ResetAsync(string notificationType);

        /// <summary>Render-only — never sends, never touches EmailDeliveries. Backs both the
        /// admin "Preview" action (sample data) and the real send path (real data), so the two
        /// can never visually drift.</summary>
        (string Subject, string HeadingText, string BodyText, string? CtaText) Render(string notificationType, EmailTemplateVariables variables, EmailTemplate? overrideRow);

        /// <summary>The actual HTML email for a real notification — falls back to the original
        /// generic NotificationTemplates.ForNotification(title, message, ...) rendering for any
        /// type outside DefaultEmailTemplates.ByType (most of the 23 known types), so every
        /// notification type keeps emailing exactly as it did before Phase 45 unless it's one of
        /// the handful the admin screen exposes for customization.</summary>
        Task<(string Subject, string HtmlBody)> RenderEmailAsync(
            string notificationType, EmailTemplateVariables variables, string title, string message, string appName, string appBaseUrl);
    }

    /// <summary>
    /// Phase 45 — admin-customizable email copy for the notification types listed in
    /// Common.DefaultEmailTemplates. Every stored field is presentation-only free text; the only
    /// "logic" a template can express is which of the fixed EmailTemplateVariables tokens it
    /// references (Substitute below never evaluates anything — an unrecognized {{Token}} is left
    /// as literal text, and everything is HTML-encoded as a whole by NotificationTemplates.RenderCard
    /// after substitution, so there is no path from stored template text to script execution).
    /// </summary>
    public class EmailTemplateService(AppDbContext db) : IEmailTemplateService
    {
        // Matches {{TokenName}} — deliberately simple/non-nested, matching the spec's own
        // "controlled template-variable system" (not a general templating engine).
        private static readonly Regex TokenPattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

        private const int MaxFieldLength = 2000;

        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<EmailTemplate>> GetAllAsync()
        {
            var overrides = await _db.EmailTemplates.Include(t => t.UpdatedByUser).ToDictionaryAsync(t => t.NotificationType);

            return DefaultEmailTemplates.ByType.Keys
                .Select(type => overrides.TryGetValue(type, out var existing)
                    ? existing
                    : new EmailTemplate { NotificationType = type })
                .ToList();
        }

        public Task<EmailTemplate?> GetOverrideAsync(string notificationType) =>
            _db.EmailTemplates.FirstOrDefaultAsync(t => t.NotificationType == notificationType)!;

        public async Task<EmailTemplate> UpsertAsync(Guid updatedByUserId, string notificationType, EmailTemplate fields)
        {
            if (!DefaultEmailTemplates.ByType.ContainsKey(notificationType))
            {
                throw new ValidationException($"'{notificationType}' does not support a custom email template.");
            }

            ValidateField(fields.SubjectTemplate, nameof(fields.SubjectTemplate));
            ValidateField(fields.HeadingTemplate, nameof(fields.HeadingTemplate));
            ValidateField(fields.BodyTemplate, nameof(fields.BodyTemplate));
            ValidateField(fields.CtaText, nameof(fields.CtaText));
            ValidateField(fields.FooterTemplate, nameof(fields.FooterTemplate));

            var existing = await _db.EmailTemplates.FirstOrDefaultAsync(t => t.NotificationType == notificationType);
            if (existing is null)
            {
                existing = new EmailTemplate { Id = Guid.NewGuid(), NotificationType = notificationType };
                _db.EmailTemplates.Add(existing);
            }

            existing.SubjectTemplate = NullIfBlank(fields.SubjectTemplate);
            existing.HeadingTemplate = NullIfBlank(fields.HeadingTemplate);
            existing.BodyTemplate = NullIfBlank(fields.BodyTemplate);
            existing.CtaText = NullIfBlank(fields.CtaText);
            existing.FooterTemplate = NullIfBlank(fields.FooterTemplate);
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByUserId = updatedByUserId;

            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task ResetAsync(string notificationType)
        {
            var existing = await _db.EmailTemplates.FirstOrDefaultAsync(t => t.NotificationType == notificationType);
            if (existing is not null)
            {
                _db.EmailTemplates.Remove(existing);
                await _db.SaveChangesAsync();
            }
        }

        public (string Subject, string HeadingText, string BodyText, string? CtaText) Render(
            string notificationType, EmailTemplateVariables variables, EmailTemplate? overrideRow)
        {
            var defaults = DefaultEmailTemplates.ByType[notificationType];
            var vars = variables.ToDictionary();

            var subject = Substitute(overrideRow?.SubjectTemplate ?? defaults.Subject, vars);
            var heading = Substitute(overrideRow?.HeadingTemplate ?? defaults.Heading, vars);
            var body = Substitute(overrideRow?.BodyTemplate ?? defaults.Body, vars);
            var cta = Substitute(overrideRow?.CtaText ?? defaults.CtaText, vars);

            return (subject, heading, body, cta);
        }

        public async Task<(string Subject, string HtmlBody)> RenderEmailAsync(
            string notificationType, EmailTemplateVariables variables, string title, string message, string appName, string appBaseUrl)
        {
            if (!DefaultEmailTemplates.ByType.ContainsKey(notificationType))
            {
                // Every other notification type — unchanged pre-Phase-45 rendering.
                return NotificationTemplates.ForNotification(title, message, variables.ActionUrl, appBaseUrl, appName);
            }

            var overrideRow = await GetOverrideAsync(notificationType);
            var (subject, heading, body, cta) = Render(notificationType, variables, overrideRow);
            var footer = overrideRow?.FooterTemplate is null ? null : Substitute(overrideRow.FooterTemplate, variables.ToDictionary());

            return NotificationTemplates.RenderCard(appName, appBaseUrl, subject, heading, body, cta, variables.ActionUrl, footer);
        }

        private static string Substitute(string template, IReadOnlyDictionary<string, string> variables) =>
            TokenPattern.Replace(template, match => variables.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

        private static void ValidateField(string? value, string fieldName)
        {
            if (value is null)
            {
                return;
            }
            if (value.Length > MaxFieldLength)
            {
                throw new ValidationException($"{fieldName} is too long (max {MaxFieldLength} characters).");
            }

            // Every {{Token}} referenced must be one of the known variables — an admin can't
            // reference something that doesn't exist (it would otherwise silently render as
            // literal "{{Typo}}" text, which is confusing rather than dangerous, but rejecting it
            // up front is a better admin experience and matches the spec's own "validate
            // templates before saving" requirement).
            var known = new EmailTemplateVariables("", "", null, null, null, null).ToDictionary().Keys;
            foreach (Match match in TokenPattern.Matches(value))
            {
                var token = match.Groups[1].Value;
                if (!known.Contains(token))
                {
                    throw new ValidationException($"'{{{{{token}}}}}' is not a recognized template variable.");
                }
            }
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

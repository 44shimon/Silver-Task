using System.Net;
using System.Net.Mail;

namespace Silver_Task.Server.Services
{
    public interface IEmailService
    {
        /// <summary>Best-effort — never throws. A failed/unconfigured send is logged and
        /// swallowed, exactly like an automation action failure never fails the user's original
        /// request (see AutomationService's own doc comment on that same principle) — an email
        /// is a side effect of a notification, not the notification itself, so it must never be
        /// able to break the operation that triggered it.</summary>
        Task SendAsync(string toEmail, string toName, string subject, string htmlBody);

        /// <summary>Whether SMTP is actually configured — NotificationService checks this (in
        /// addition to the Notifications.EmailNotificationsEnabled system setting) so it can skip
        /// the "would send but can't" path entirely rather than attempting and logging a failure
        /// for every single notification when nobody has configured SMTP yet.</summary>
        bool IsConfigured { get; }
    }

    /// <summary>
    /// Built on System.Net.Mail.SmtpClient — the .NET runtime's own built-in SMTP client, chosen
    /// specifically because it ships in the shared framework already (no new NuGet dependency for
    /// a feature that has no real SMTP server to test against in this environment). Microsoft's
    /// docs steer new projects toward MailKit for advanced scenarios (OAuth, connection pooling),
    /// but for a from-scratch "send a plain HTML email via SMTP" need behind a narrow IEmailService
    /// interface, swapping the implementation later is a fully contained change (the same
    /// "swappable behind an interface" precedent IAttachmentService already established for
    /// storage backends) — nothing outside this file would need to change.
    ///
    /// Configuration lives under the "Smtp" section (Host/Port/EnableSsl/Username/Password/
    /// FromAddress/FromName) via the standard appsettings/user-secrets/environment-variable chain
    /// this app already uses for Jwt/ConnectionStrings — never hardcoded, never committed. If
    /// Smtp:Host is unset, this service is simply not configured: every call becomes a no-op (with
    /// a one-line log), so the notification system fully works end-to-end (in-app) without an SMTP
    /// server ever existing, and turning email on later is purely a configuration change.
    /// </summary>
    public class EmailService(IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<EmailService> _logger = logger;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"]);

        public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            if (!IsConfigured)
            {
                _logger.LogDebug("Email not sent to {ToEmail} ('{Subject}') — Smtp:Host is not configured.", toEmail, subject);
                return;
            }

            try
            {
                var host = _configuration["Smtp:Host"]!;
                var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
                var enableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var explicitSsl) || explicitSsl;
                var username = _configuration["Smtp:Username"];
                var password = _configuration["Smtp:Password"];
                var fromAddress = _configuration["Smtp:FromAddress"] ?? username ?? "no-reply@silvertask.local";
                var fromName = _configuration["Smtp:FromName"] ?? "Silver Task";

                using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
                if (!string.IsNullOrEmpty(username))
                {
                    client.Credentials = new NetworkCredential(username, password);
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(new MailAddress(toEmail, toName));

                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                // Never propagate — see IEmailService.SendAsync's own doc comment.
                _logger.LogWarning(ex, "Failed to send email to {ToEmail} ('{Subject}').", toEmail, subject);
            }
        }
    }
}

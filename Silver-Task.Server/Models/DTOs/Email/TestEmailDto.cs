using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Email
{
    public class TestEmailRequest
    {
        [Required, EmailAddress]
        public required string ToEmail { get; set; }
    }

    /// <summary>Deliberately just a boolean + a short generic message — never the underlying
    /// SmtpException/host/credentials (spec's own "test email must not expose passwords, API
    /// keys, or connection strings" requirement). See IEmailService.SendAsync's EmailSendResult.</summary>
    public class TestEmailResultDto
    {
        public bool Success { get; set; }

        public required string Message { get; set; }
    }
}

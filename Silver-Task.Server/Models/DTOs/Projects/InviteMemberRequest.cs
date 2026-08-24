using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Projects
{
    /// <summary>Creates a brand-new user (always Role.Member — never settable here) and adds
    /// them to the project in one step, for when "add by email" 404s because no account exists
    /// yet. Administrator-only; the inviter shares the password out-of-band since there's no
    /// email-sending infrastructure in this app.</summary>
    public class InviteMemberRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [Required, EmailAddress, StringLength(320)]
        public required string Email { get; set; }

        [Required, MinLength(8)]
        public required string Password { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Users
{
    public class ResetPasswordRequest
    {
        // See CreateUserRequest.Password — validated against configurable Security settings
        // in UserService, not a static attribute.
        [Required]
        public required string NewPassword { get; set; }
    }
}

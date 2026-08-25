using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class ChangePasswordRequest
    {
        [Required]
        public required string CurrentPassword { get; set; }

        // See CreateUserRequest.Password — validated against configurable Security settings
        // in UserService, not a static attribute.
        [Required]
        public required string NewPassword { get; set; }

        [Required]
        public required string ConfirmNewPassword { get; set; }
    }
}

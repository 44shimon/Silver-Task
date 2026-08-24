using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class ChangePasswordRequest
    {
        [Required]
        public required string CurrentPassword { get; set; }

        [Required, MinLength(8)]
        public required string NewPassword { get; set; }

        [Required]
        public required string ConfirmNewPassword { get; set; }
    }
}

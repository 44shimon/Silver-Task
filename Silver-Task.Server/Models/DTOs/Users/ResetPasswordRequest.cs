using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Users
{
    public class ResetPasswordRequest
    {
        [Required, MinLength(8)]
        public required string NewPassword { get; set; }
    }
}

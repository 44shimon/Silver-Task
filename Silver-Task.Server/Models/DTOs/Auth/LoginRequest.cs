using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Auth
{
    public class LoginRequest
    {
        [Required, EmailAddress, StringLength(320)]
        public required string Email { get; set; }

        [Required]
        public required string Password { get; set; }
    }
}

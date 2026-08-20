using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Users
{
    public class CreateUserRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [Required, EmailAddress, StringLength(320)]
        public required string Email { get; set; }

        [Required, MinLength(8)]
        public required string Password { get; set; }

        /// <summary>
        /// Ignored when this is the very first user in the system — that account is always
        /// created as Administrator so there's someone able to manage the app.
        /// </summary>
        public UserRole Role { get; set; } = UserRole.Member;
    }
}

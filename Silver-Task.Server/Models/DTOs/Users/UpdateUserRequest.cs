using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Users
{
    public class UpdateUserRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [Required]
        public UserRole Role { get; set; }

        public bool IsActive { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Projects
{
    public class AddProjectMemberRequest
    {
        [Required, EmailAddress, StringLength(320)]
        public required string Email { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Projects
{
    public class CreateProjectRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }
    }
}

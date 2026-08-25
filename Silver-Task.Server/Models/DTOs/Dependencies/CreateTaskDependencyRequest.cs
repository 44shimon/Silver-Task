using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Dependencies
{
    public class CreateTaskDependencyRequest
    {
        [Required]
        public Guid DependsOnTaskId { get; set; }
    }
}

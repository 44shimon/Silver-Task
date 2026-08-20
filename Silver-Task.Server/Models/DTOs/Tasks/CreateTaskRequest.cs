using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public class CreateTaskRequest
    {
        [Required, StringLength(500, MinimumLength = 1)]
        public required string Title { get; set; }

        [StringLength(10000)]
        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public Guid? AssignedToUserId { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? DueDate { get; set; }
    }
}

using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public class TaskDto
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public UserSummaryDto? AssignedTo { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        public double SortOrder { get; set; }

        public List<TaskCustomValueDto> CustomValues { get; set; } = [];

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

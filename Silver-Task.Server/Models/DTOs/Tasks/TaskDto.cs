using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public class TaskDto
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        /// <summary>Only populated when the task was loaded with its Project included (e.g. the
        /// cross-project "my tasks" endpoint) — null for the per-project task list, which already
        /// knows its own project name from context.</summary>
        public string? ProjectName { get; set; }

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

        /// <summary>How many other tasks this one depends on (its prerequisites).</summary>
        public int DependsOnCount { get; set; }

        /// <summary>Of DependsOnCount, how many prerequisites are not yet Complete — 0 means this
        /// task isn't currently dependency-blocked. Never written back to Status; purely a
        /// computed display value (see TaskItem.BlockedByCount).</summary>
        public int BlockedByCount { get; set; }

        /// <summary>How many other tasks depend on this one (the "Blocking" count).</summary>
        public int DependentCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

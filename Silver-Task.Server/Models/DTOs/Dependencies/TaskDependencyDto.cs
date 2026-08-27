using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Dependencies
{
    /// <summary>One row in either the "Depends On" list or the "Blocking" list on the Task Detail
    /// panel — DependencyId is the TaskDependency row (needed to remove it); everything else
    /// describes the *other* task in the relationship (the prerequisite for "Depends On", the
    /// dependent for "Blocking").</summary>
    public class TaskDependencyDto
    {
        public Guid DependencyId { get; set; }

        public required string DependencyType { get; set; }

        /// <summary>Whether this specific relationship's own condition currently holds (see
        /// TaskDependencyService.IsRelationshipSatisfied) — backs the checkbox-style indicator
        /// next to each row in the Dependencies panel. Not the same as "is the task overall
        /// Ready" — a task can have several dependencies, only some unsatisfied.</summary>
        public bool IsSatisfied { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid TaskId { get; set; }

        public required string Title { get; set; }

        public TaskItemStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public UserSummaryDto? AssignedTo { get; set; }

        public DateOnly? DueDate { get; set; }
    }
}

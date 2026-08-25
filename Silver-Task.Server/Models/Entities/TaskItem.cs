using System.ComponentModel.DataAnnotations.Schema;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// A single row in the project spreadsheet. Named TaskItem (not Task) to avoid
    /// colliding with System.Threading.Tasks.Task, which is in scope everywhere via
    /// ImplicitUsings.
    /// </summary>
    public class TaskItem
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public Guid? AssignedToUserId { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        /// <summary>Fractional index used to persist manual row ordering within a project.</summary>
        public double SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Project? Project { get; set; }

        public User? AssignedTo { get; set; }

        public ICollection<TaskComment> Comments { get; set; } = [];

        public ICollection<TaskActivity> Activities { get; set; } = [];

        public ICollection<TaskAttachment> Attachments { get; set; } = [];

        public ICollection<TaskCustomValue> CustomValues { get; set; } = [];

        /// <summary>Not persisted — populated in bulk (one aggregate query per task list, never
        /// per-task) by TaskService whenever it loads tasks, so TaskDto can show dependency
        /// counts on every list/table/kanban/etc. without an N+1 query per task. See
        /// TaskService.AttachDependencySummaryAsync.</summary>
        [NotMapped]
        public int DependsOnCount { get; set; }

        /// <summary>Of DependsOnCount, how many prerequisites are not yet Complete — this is the
        /// "blocked" count. Deliberately never written back to Status (see TaskDependencyService/
        /// README "Blocked state" reasoning) — purely a computed display value.</summary>
        [NotMapped]
        public int BlockedByCount { get; set; }

        /// <summary>How many other tasks depend on this one (the "Blocking" count).</summary>
        [NotMapped]
        public int DependentCount { get; set; }
    }
}

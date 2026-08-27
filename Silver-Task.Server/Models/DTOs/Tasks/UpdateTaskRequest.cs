using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    /// <summary>Full-resource replace, matching the PUT semantics used elsewhere in this API.</summary>
    public class UpdateTaskRequest
    {
        [Required, StringLength(500, MinimumLength = 1)]
        public required string Title { get; set; }

        [StringLength(10000)]
        public string? Description { get; set; }

        [Required]
        public TaskItemStatus Status { get; set; }

        [Required]
        public TaskPriority Priority { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? DueDate { get; set; }

        [Required]
        public double SortOrder { get; set; }

        /// <summary>Phase 39 — set true (alongside OverrideReason) to bypass a dependency block
        /// that would otherwise reject this status change; ignored entirely when the status
        /// change isn't actually blocked. Requires Permissions.DependenciesOverride (project
        /// Manager/Administrator/owner) — see TaskService.EnsureNotBlockedByDependenciesAsync.</summary>
        public bool OverrideDependencyBlock { get; set; }

        [StringLength(1000)]
        public string? OverrideReason { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.V1
{
    /// <summary>Phase 61 — the public v1 contract for a task. Its own type, not a reuse of the
    /// internal TaskDto (Models/DTOs/Tasks/TaskDto.cs) — see ProjectV1Dto's doc comment for why.
    /// Deliberately excludes internal-only/implementation-detail fields the internal TaskDto
    /// carries (recurrence bookkeeping, dependency/subtask computed counts, custom field values) —
    /// a v1 foundation should expose a small, stable core, not everything the SPA happens to
    /// need; those can be added deliberately in a future v1 revision if a real integration needs
    /// them.</summary>
    public class TaskV1Dto
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

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class CreateTaskV1Request
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required, StringLength(500, MinimumLength = 1)]
        public required string Title { get; set; }

        [StringLength(10000)]
        public string? Description { get; set; }

        public TaskItemStatus? Status { get; set; }

        public TaskPriority? Priority { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? DueDate { get; set; }
    }

    /// <summary>Full-resource replace, matching PUT semantics used elsewhere in this API — same
    /// convention as the internal UpdateTaskRequest.</summary>
    public class UpdateTaskV1Request
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
    }
}

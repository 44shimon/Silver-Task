using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Templates
{
    public class ProjectTemplateTaskDto
    {
        public Guid Id { get; set; }

        public Guid? ParentTemplateTaskId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public int? StartOffsetDays { get; set; }

        public int? DueOffsetDays { get; set; }

        public int? EstimatedDurationDays { get; set; }

        public required string AssignmentMode { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public string? AssignedToName { get; set; }

        public double SortOrder { get; set; }

        public List<string> Tags { get; set; } = [];

        public List<TemplateCustomValueDto> CustomValues { get; set; } = [];

        public List<TemplateChecklistItemDto> ChecklistItems { get; set; } = [];
    }

    public class ProjectTemplateDependencyDto
    {
        public Guid Id { get; set; }

        public Guid TemplateTaskId { get; set; }

        public Guid DependsOnTemplateTaskId { get; set; }

        public required string DependencyType { get; set; }
    }

    public class ProjectTemplateDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }

        public required string CreatedByName { get; set; }

        public bool IsArchived { get; set; }

        public bool IsPublic { get; set; }

        public int UsageCount { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public bool IsOwnedByMe { get; set; }

        public bool IsFavorite { get; set; }

        /// <summary>Only populated for the owner's own view — a recipient sees their own access,
        /// not the full share list (same convention as SavedReportDto).</summary>
        public List<TemplateSharedUserDto>? SharedWith { get; set; }

        public List<ProjectTemplateTaskDto> Tasks { get; set; } = [];

        public List<ProjectTemplateDependencyDto> Dependencies { get; set; } = [];

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    // ---------- Save (create/update) — a full-resource replace of the task/dependency graph,
    // same "PUT replaces everything" convention UpdateTaskRequest already established, rather
    // than a diff/patch API. ClientId/ParentClientId/*ClientId fields are IDs the FRONTEND mints
    // (a fresh Guid per new task, or the task's real persisted Id if editing an existing one) used
    // only to correlate parent/dependency references WITHIN this one request — never trusted as
    // anything other than a same-request correlation key. ----------

    public class SaveProjectTemplateTaskRequest
    {
        public Guid ClientId { get; set; }

        public Guid? ParentClientId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public int? StartOffsetDays { get; set; }

        public int? DueOffsetDays { get; set; }

        public int? EstimatedDurationDays { get; set; }

        public required string AssignmentMode { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public double SortOrder { get; set; }

        public List<string> Tags { get; set; } = [];

        public List<TemplateCustomValueDto> CustomValues { get; set; } = [];

        public List<string> ChecklistItems { get; set; } = [];
    }

    public class SaveProjectTemplateDependencyRequest
    {
        public Guid TemplateTaskClientId { get; set; }

        public Guid DependsOnTemplateTaskClientId { get; set; }

        public required string DependencyType { get; set; }
    }

    public class SaveProjectTemplateRequest
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public bool IsPublic { get; set; }

        public List<SaveProjectTemplateTaskRequest> Tasks { get; set; } = [];

        public List<SaveProjectTemplateDependencyRequest> Dependencies { get; set; } = [];
    }
}

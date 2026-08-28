using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Templates
{
    public class TaskTemplateDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public int? StartOffsetDays { get; set; }

        public int? DueOffsetDays { get; set; }

        public int? EstimatedDurationDays { get; set; }

        public required string AssignmentMode { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public string? AssignedToName { get; set; }

        public Guid CreatedByUserId { get; set; }

        public required string CreatedByName { get; set; }

        public bool IsArchived { get; set; }

        public bool IsPublic { get; set; }

        public int UsageCount { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public bool IsOwnedByMe { get; set; }

        public bool IsFavorite { get; set; }

        public List<TemplateSharedUserDto>? SharedWith { get; set; }

        public List<string> Tags { get; set; } = [];

        public List<TemplateCustomValueDto> CustomValues { get; set; } = [];

        public List<TemplateChecklistItemDto> ChecklistItems { get; set; } = [];

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class SaveTaskTemplateRequest
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public int? StartOffsetDays { get; set; }

        public int? DueOffsetDays { get; set; }

        public int? EstimatedDurationDays { get; set; }

        public required string AssignmentMode { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public bool IsPublic { get; set; }

        public List<string> Tags { get; set; } = [];

        public List<TemplateCustomValueDto> CustomValues { get; set; } = [];

        public List<string> ChecklistItems { get; set; } = [];
    }
}

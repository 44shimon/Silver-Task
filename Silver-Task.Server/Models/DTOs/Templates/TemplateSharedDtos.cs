namespace Silver_Task.Server.Models.DTOs.Templates
{
    /// <summary>Shared by both ProjectTemplateTask and TaskTemplate — a stored default custom
    /// field value, re-validated against the real CustomField through
    /// TaskService.SetCustomValueAsync at instantiation time (never a parallel validation copy).</summary>
    public class TemplateCustomValueDto
    {
        public Guid CustomFieldId { get; set; }

        public string? Value { get; set; }
    }

    public class TemplateChecklistItemDto
    {
        public Guid Id { get; set; }

        public required string Text { get; set; }

        public double SortOrder { get; set; }
    }

    public class TemplateSharedUserDto
    {
        public Guid UserId { get; set; }

        public required string Name { get; set; }
    }

    public class ShareTemplateRequest
    {
        public required string Email { get; set; }
    }

    /// <summary>The Template Home list — one row per template regardless of type (ProjectTemplate
    /// or TaskTemplate), matching the spec's own flat "My Templates" mockup. TaskCount is total
    /// tasks (including subtasks) for a ProjectTemplate, always 1 for a TaskTemplate.</summary>
    public class TemplateSummaryDto
    {
        public Guid Id { get; set; }

        public required string Type { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }

        public required string CreatedByName { get; set; }

        public bool IsArchived { get; set; }

        public int TaskCount { get; set; }

        public int UsageCount { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public bool IsOwnedByMe { get; set; }

        public bool IsFavorite { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 41 — the value of one EntityType.Project custom field on one project.
    /// Mirrors TaskCustomValue exactly (same EAV pattern, same text storage validated/normalized
    /// per CustomField.FieldType).</summary>
    public class ProjectCustomValue
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public Guid CustomFieldId { get; set; }

        public string? Value { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Project? Project { get; set; }

        public CustomField? CustomField { get; set; }
    }
}

using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A project-defined task column, e.g. "Contractor" (Text) or "Cost" (Currency).</summary>
    public class CustomField
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public required string Name { get; set; }

        public CustomFieldType FieldType { get; set; }

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Project? Project { get; set; }

        public ICollection<CustomFieldOption> Options { get; set; } = [];

        public ICollection<TaskCustomValue> Values { get; set; } = [];
    }
}

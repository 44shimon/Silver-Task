using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A task column, e.g. "Contractor" (Text) or "Cost" (Currency). Null ProjectId
    /// means the field applies to every project (an Administrator-only capability — see
    /// CustomFieldService.EnsureCanManageFieldAsync); a concrete ProjectId scopes it to one
    /// project, same as before Phase 25.</summary>
    public class CustomField
    {
        public Guid Id { get; set; }

        public Guid? ProjectId { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public CustomFieldType FieldType { get; set; }

        public bool IsRequired { get; set; }

        /// <summary>Deactivating (rather than deleting) a field that already has task values is
        /// the spec's preferred "don't silently destroy data" path — existing values stay
        /// visible/intact, but TaskService.ValidateAndNormalizeCustomValueAsync rejects setting a
        /// new non-null value on an inactive field.</summary>
        public bool IsActive { get; set; } = true;

        public string? DefaultValue { get; set; }

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Project? Project { get; set; }

        public ICollection<CustomFieldOption> Options { get; set; } = [];

        public ICollection<TaskCustomValue> Values { get; set; } = [];
    }
}

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

        /// <summary>Phase 41 — a stable, immutable, snake_case internal key generated from Name
        /// at creation time (e.g. "Property Address" -> "property_address"). Renaming the field
        /// later never changes this, so nothing that references a field by Identifier (imports,
        /// automation conditions using the raw key, external integrations) breaks. Never
        /// regenerated on update.</summary>
        public required string Identifier { get; set; }

        public string? Description { get; set; }

        public CustomFieldType FieldType { get; set; }

        /// <summary>Phase 41 — which kind of object this field's values attach to (Task or
        /// Project). Immutable after creation, same reasoning as FieldType.</summary>
        public CustomFieldEntityType EntityType { get; set; } = CustomFieldEntityType.Task;

        public bool IsRequired { get; set; }

        /// <summary>Deactivating (rather than deleting) a field that already has task values is
        /// the spec's preferred "don't silently destroy data" path — existing values stay
        /// visible/intact, but TaskService.ValidateAndNormalizeCustomValueAsync rejects setting a
        /// new non-null value on an inactive field.</summary>
        public bool IsActive { get; set; } = true;

        public string? DefaultValue { get; set; }

        public int SortOrder { get; set; }

        /// <summary>Phase 41 — an optional display-only section label (e.g. "Property
        /// Information"); fields sharing the same GroupName render together, in SortOrder, under
        /// one heading. Null fields render ungrouped. Deliberately just a string, not a separate
        /// CustomFieldGroup table — there's nothing else a group needs to own.</summary>
        public string? GroupName { get; set; }

        /// <summary>Text/LongText/Url/Email/Phone only.</summary>
        public string? Placeholder { get; set; }

        /// <summary>Text/LongText/Url/Email/Phone only.</summary>
        public int? MaxLength { get; set; }

        /// <summary>Number/Currency only.</summary>
        public decimal? MinValue { get; set; }

        /// <summary>Number/Currency only.</summary>
        public decimal? MaxValue { get; set; }

        /// <summary>Number/Currency only. Null = any precision allowed; 0 = integers only.</summary>
        public int? DecimalPlaces { get; set; }

        /// <summary>Phase 41 — a private field's VALUE is redacted (never returned, not just
        /// hidden by CSS) from any DTO built for a caller who isn't the field's own creator scope
        /// owner (Administrator, or — for a project-scoped field — that project's owner/Manager),
        /// unless their role is also listed in VisibleToRoles. Enforced in the mapping layer
        /// (TaskMappingExtensions/ProjectMappingExtensions), never left to the frontend to hide.</summary>
        public bool IsPrivate { get; set; }

        /// <summary>Comma-separated UserRole names additionally allowed to see a private field's
        /// value (e.g. "Administrator,Manager"). Null means only an Administrator or the relevant
        /// project's own owner/Manager (the normal manage-tier) can see it.</summary>
        public string? VisibleToRoles { get; set; }

        /// <summary>Phase 41 — basic single-condition visibility: this field is only shown/
        /// required when ConditionField's value compares to ConditionValue via ConditionOperator.
        /// Null ConditionFieldId means always visible (no condition). Self-referencing FK,
        /// Restrict on delete — deleting the controlling field must not cascade-delete the field
        /// that depends on it (see ProjectTemplateTaskConfiguration's own precedent for why a
        /// self-reference uses Restrict, not Cascade).</summary>
        public Guid? ConditionFieldId { get; set; }

        public AutomationConditionOperator? ConditionOperator { get; set; }

        public string? ConditionValue { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Project? Project { get; set; }

        public CustomField? ConditionField { get; set; }

        public ICollection<CustomFieldOption> Options { get; set; } = [];

        public ICollection<TaskCustomValue> Values { get; set; } = [];

        public ICollection<ProjectCustomValue> ProjectValues { get; set; } = [];
    }
}

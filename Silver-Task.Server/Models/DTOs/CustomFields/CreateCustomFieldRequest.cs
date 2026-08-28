using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public class CreateCustomFieldRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        public CustomFieldType FieldType { get; set; }

        /// <summary>Defaults to Task — every field created before Phase 41 (and every existing
        /// call site that doesn't send this) behaves exactly as before.</summary>
        public CustomFieldEntityType EntityType { get; set; } = CustomFieldEntityType.Task;

        public bool IsRequired { get; set; }

        [StringLength(1000)]
        public string? DefaultValue { get; set; }

        /// <summary>Initial options for Dropdown/MultiSelect fields; ignored for other types.</summary>
        public List<string>? Options { get; set; }

        [StringLength(200)]
        public string? GroupName { get; set; }

        [StringLength(200)]
        public string? Placeholder { get; set; }

        public int? MaxLength { get; set; }

        public decimal? MinValue { get; set; }

        public decimal? MaxValue { get; set; }

        public int? DecimalPlaces { get; set; }

        public bool IsPrivate { get; set; }

        /// <summary>Comma-separated UserRole names, e.g. "Administrator,Manager".</summary>
        [StringLength(200)]
        public string? VisibleToRoles { get; set; }

        public Guid? ConditionFieldId { get; set; }

        public AutomationConditionOperator? ConditionOperator { get; set; }

        [StringLength(1000)]
        public string? ConditionValue { get; set; }
    }
}

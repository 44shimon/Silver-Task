using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    /// <summary>FieldType and EntityType are intentionally not editable — changing either after
    /// values exist could leave those values impossible to interpret correctly, or silently move
    /// which kind of object the field applies to.</summary>
    public class UpdateCustomFieldRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public bool IsRequired { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(1000)]
        public string? DefaultValue { get; set; }

        [Required]
        public int SortOrder { get; set; }

        [StringLength(200)]
        public string? GroupName { get; set; }

        [StringLength(200)]
        public string? Placeholder { get; set; }

        public int? MaxLength { get; set; }

        public decimal? MinValue { get; set; }

        public decimal? MaxValue { get; set; }

        public int? DecimalPlaces { get; set; }

        public bool IsPrivate { get; set; }

        [StringLength(200)]
        public string? VisibleToRoles { get; set; }

        public Guid? ConditionFieldId { get; set; }

        public AutomationConditionOperator? ConditionOperator { get; set; }

        [StringLength(1000)]
        public string? ConditionValue { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    /// <summary>FieldType is intentionally not editable — changing it after values exist could
    /// leave those values impossible to interpret correctly (e.g. Number -> Date).</summary>
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
    }
}

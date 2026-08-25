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

        public bool IsRequired { get; set; }

        [StringLength(1000)]
        public string? DefaultValue { get; set; }

        /// <summary>Initial options for Dropdown/MultiSelect fields; ignored for other types.</summary>
        public List<string>? Options { get; set; }
    }
}

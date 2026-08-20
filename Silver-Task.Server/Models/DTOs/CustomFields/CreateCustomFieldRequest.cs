using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public class CreateCustomFieldRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [Required]
        public CustomFieldType FieldType { get; set; }

        /// <summary>Initial options for Dropdown/MultiSelect fields; ignored for other types.</summary>
        public List<string>? Options { get; set; }
    }
}

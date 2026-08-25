using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public class CustomFieldDto
    {
        public Guid Id { get; set; }

        /// <summary>Null means this field applies to every project.</summary>
        public Guid? ProjectId { get; set; }

        /// <summary>Null when ProjectId is null (applies to every project) — "All Projects" in the UI.</summary>
        public string? ProjectName { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public CustomFieldType FieldType { get; set; }

        public bool IsRequired { get; set; }

        public bool IsActive { get; set; }

        public string? DefaultValue { get; set; }

        public int SortOrder { get; set; }

        public List<CustomFieldOptionDto> Options { get; set; } = [];

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

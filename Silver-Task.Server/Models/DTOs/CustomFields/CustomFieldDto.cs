using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public class CustomFieldDto
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public required string Name { get; set; }

        public CustomFieldType FieldType { get; set; }

        public int SortOrder { get; set; }

        public List<CustomFieldOptionDto> Options { get; set; } = [];

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

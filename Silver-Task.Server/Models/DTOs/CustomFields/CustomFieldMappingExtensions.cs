using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public static class CustomFieldMappingExtensions
    {
        public static CustomFieldDto ToDto(this CustomField field) => new()
        {
            Id = field.Id,
            ProjectId = field.ProjectId,
            Name = field.Name,
            FieldType = field.FieldType,
            SortOrder = field.SortOrder,
            Options = field.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => o.ToDto())
                .ToList(),
            CreatedAt = field.CreatedAt,
            UpdatedAt = field.UpdatedAt
        };

        public static CustomFieldOptionDto ToDto(this CustomFieldOption option) => new()
        {
            Id = option.Id,
            Value = option.Value,
            SortOrder = option.SortOrder
        };
    }
}

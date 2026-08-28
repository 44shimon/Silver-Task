using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public static class CustomFieldMappingExtensions
    {
        public static CustomFieldDto ToDto(this CustomField field) => new()
        {
            Id = field.Id,
            ProjectId = field.ProjectId,
            ProjectName = field.Project?.Name,
            Name = field.Name,
            Identifier = field.Identifier,
            Description = field.Description,
            FieldType = field.FieldType,
            EntityType = field.EntityType,
            IsRequired = field.IsRequired,
            IsActive = field.IsActive,
            DefaultValue = field.DefaultValue,
            SortOrder = field.SortOrder,
            GroupName = field.GroupName,
            Placeholder = field.Placeholder,
            MaxLength = field.MaxLength,
            MinValue = field.MinValue,
            MaxValue = field.MaxValue,
            DecimalPlaces = field.DecimalPlaces,
            IsPrivate = field.IsPrivate,
            VisibleToRoles = field.VisibleToRoles,
            ConditionFieldId = field.ConditionFieldId,
            ConditionOperator = field.ConditionOperator,
            ConditionValue = field.ConditionValue,
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
            SortOrder = option.SortOrder,
            IsActive = option.IsActive
        };
    }
}

using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.FileCategories
{
    public static class FileCategoryMappingExtensions
    {
        public static FileCategoryDto ToDto(this FileCategory category) => new()
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }
}

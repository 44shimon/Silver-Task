using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Tags
{
    public static class TagMappingExtensions
    {
        public static TagDto ToDto(this Tag tag) => new()
        {
            Id = tag.Id,
            Name = tag.Name,
            Description = tag.Description,
            Color = tag.Color,
            IsActive = tag.IsActive,
            CreatedAt = tag.CreatedAt
        };
    }
}

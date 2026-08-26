using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Users
{
    public static class UserMappingExtensions
    {
        public static UserDto ToDto(this User user, List<string>? permissions = null) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            Permissions = permissions,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        public static UserSummaryDto ToSummaryDto(this User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            IsActive = user.IsActive
        };
    }
}

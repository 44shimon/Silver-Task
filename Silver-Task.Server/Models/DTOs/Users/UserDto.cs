using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Users
{
    /// <summary>Public shape of a User — never includes the password hash.</summary>
    public class UserDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Email { get; set; }

        public UserRole Role { get; set; }

        public bool IsActive { get; set; }

        /// <summary>The caller's own system-level permissions (Phase 32) — only populated on
        /// /auth/login and /auth/me (the two responses the frontend actually builds its
        /// usePermissions() cache from); left null everywhere else UserDto is reused (e.g. the
        /// Admin Users list), which has no reason to compute another user's permission set.</summary>
        public List<string>? Permissions { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

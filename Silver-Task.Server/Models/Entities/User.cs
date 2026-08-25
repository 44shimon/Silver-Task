using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    public class User
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Email { get; set; }

        public required string PasswordHash { get; set; }

        public UserRole Role { get; set; } = UserRole.Member;

        public bool IsActive { get; set; } = true;

        /// <summary>Consecutive failed login attempts since the last success — reset to 0 on any
        /// successful login. Compared against Security.MaxFailedLoginAttempts in AuthService.</summary>
        public int FailedLoginAttempts { get; set; }

        /// <summary>Set when FailedLoginAttempts reaches the configured max; login is rejected
        /// while this is in the future, regardless of password correctness. Null means not
        /// locked out.</summary>
        public DateTime? LockedOutUntil { get; set; }

        /// <summary>Soft-delete flag (Phase 26) — a "deleted" user's row is never removed, only
        /// marked. This keeps every existing FK (task assignments, comments, activity history,
        /// project ownership/membership) pointing at a real row instead of needing to be
        /// nulled out or destroyed. Deleting always also sets IsActive=false (see
        /// UserService.DeleteAsync), so login-rejection reuses the existing IsActive check rather
        /// than needing a second one.</summary>
        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public Guid? DeletedByUserId { get; set; }

        public User? DeletedByUser { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<Project> OwnedProjects { get; set; } = [];

        public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];

        public ICollection<TaskItem> AssignedTasks { get; set; } = [];
    }
}

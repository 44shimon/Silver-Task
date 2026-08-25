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

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<Project> OwnedProjects { get; set; } = [];

        public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];

        public ICollection<TaskItem> AssignedTasks { get; set; } = [];
    }
}

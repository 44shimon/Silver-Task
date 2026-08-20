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

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<Project> OwnedProjects { get; set; } = [];

        public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];

        public ICollection<TaskItem> AssignedTasks { get; set; } = [];
    }
}

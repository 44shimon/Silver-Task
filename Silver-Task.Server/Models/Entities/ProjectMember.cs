using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Join entity granting a user access to a project.</summary>
    public class ProjectMember
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public Guid UserId { get; set; }

        /// <summary>Per-project role (Phase 32) — see ProjectRole's own doc comment. Defaults to
        /// Member for every new membership except the project owner's own row, which is always
        /// created with Manager. Drives ProjectAccessService.EnsureCanManageAsync/EnsureCanEditAsync,
        /// independently of the member's system-wide UserRole.</summary>
        public ProjectRole Role { get; set; } = ProjectRole.Member;

        public DateTime CreatedAt { get; set; }

        public Project? Project { get; set; }

        public User? User { get; set; }
    }
}

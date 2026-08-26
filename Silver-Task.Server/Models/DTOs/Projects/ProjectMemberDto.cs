using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Projects
{
    public class ProjectMemberDto
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public required UserSummaryDto User { get; set; }

        /// <summary>Per-project role (Phase 32) — see ProjectRole. The project owner's own
        /// membership row is always Manager.</summary>
        public ProjectRole Role { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

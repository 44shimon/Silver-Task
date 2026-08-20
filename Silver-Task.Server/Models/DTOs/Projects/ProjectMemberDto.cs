using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Projects
{
    public class ProjectMemberDto
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public required UserSummaryDto User { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

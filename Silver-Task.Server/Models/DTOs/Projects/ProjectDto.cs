using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Projects
{
    public class ProjectDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public required UserSummaryDto Owner { get; set; }

        public int MemberCount { get; set; }

        /// <summary>Only populated by the endpoints that bother computing it (the project list,
        /// for the Admin Projects page) — null elsewhere rather than paying for an extra query
        /// every time a single project is created/renamed/archived.</summary>
        public int? TaskCount { get; set; }

        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

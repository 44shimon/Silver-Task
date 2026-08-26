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

        /// <summary>The caller's own effective permissions within this project (Phase 32) — see
        /// IPermissionService.GetProjectPermissionsAsync. Only populated by GetById (the single-
        /// project fetch every ProjectPage load already makes); null on list endpoints, which
        /// would otherwise pay for one extra permission computation per row for data the list
        /// views don't need.</summary>
        public List<string>? MyPermissions { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

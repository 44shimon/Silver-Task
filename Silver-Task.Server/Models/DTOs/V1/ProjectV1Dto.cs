using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.V1
{
    /// <summary>Phase 61 — the public v1 contract for a project. Deliberately its own type, not a
    /// reuse of the internal ProjectDto (Models/DTOs/Projects/ProjectDto.cs): a future change to
    /// that internal shape for SPA-only reasons (e.g. MyPermissions, TaskCount's "only some
    /// endpoints populate this" quirk) must never silently change what an external v1 integration
    /// already depends on. See docs/public-api.md.</summary>
    public class ProjectV1Dto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public required UserSummaryDto Owner { get; set; }

        public int MemberCount { get; set; }

        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class CreateProjectV1Request
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }
    }

    public class UpdateProjectV1Request
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }
    }
}

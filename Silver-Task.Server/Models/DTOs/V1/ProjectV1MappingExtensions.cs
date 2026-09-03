using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.V1
{
    public static class ProjectV1MappingExtensions
    {
        /// <summary>Assumes Owner and Members are loaded — true for every Project instance
        /// IProjectService ever returns (GetAllForUserAsync/LoadProjectAsync both .Include them;
        /// see ProjectService.cs), the same assumption the internal ProjectMappingExtensions.ToDto
        /// already relies on.</summary>
        public static ProjectV1Dto ToV1Dto(this Project project) => new()
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Owner = project.Owner!.ToSummaryDto(),
            MemberCount = project.Members.Count,
            IsArchived = project.IsArchived,
            ArchivedAt = project.ArchivedAt,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}

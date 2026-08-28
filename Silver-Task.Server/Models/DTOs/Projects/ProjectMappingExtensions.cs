using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Projects
{
    public static class ProjectMappingExtensions
    {
        public static ProjectDto ToDto(this Project project, int? taskCount = null, List<string>? myPermissions = null) => new()
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Owner = project.Owner!.ToSummaryDto(),
            MemberCount = project.Members.Count,
            TaskCount = taskCount,
            IsArchived = project.IsArchived,
            ArchivedAt = project.ArchivedAt,
            MyPermissions = myPermissions,
            CustomValues = project.CustomValues
                .Select(v => new ProjectCustomValueDto { CustomFieldId = v.CustomFieldId, Value = v.Value })
                .ToList(),
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };

        public static ProjectMemberDto ToDto(this ProjectMember member) => new()
        {
            Id = member.Id,
            ProjectId = member.ProjectId,
            User = member.User!.ToSummaryDto(),
            Role = member.Role,
            CreatedAt = member.CreatedAt
        };
    }
}

using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Folders
{
    public static class FolderMappingExtensions
    {
        public static FolderDto ToDto(this Folder folder) => new()
        {
            Id = folder.Id,
            Name = folder.Name,
            ParentFolderId = folder.ParentFolderId,
            ProjectId = folder.ProjectId,
            CreatedBy = folder.CreatedBy!.ToSummaryDto(),
            IsDeleted = folder.IsDeleted,
            DeletedAt = folder.DeletedAt,
            DeletedBy = folder.DeletedByUser?.ToSummaryDto(),
            CreatedAt = folder.CreatedAt,
            UpdatedAt = folder.UpdatedAt
        };
    }
}

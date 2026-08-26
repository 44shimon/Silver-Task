using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Folders
{
    public class FolderDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public Guid? ParentFolderId { get; set; }

        public Guid ProjectId { get; set; }

        public required UserSummaryDto CreatedBy { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public UserSummaryDto? DeletedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class TaskAttachmentDto
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public required string FileName { get; set; }

        public long FileSize { get; set; }

        public required string MimeType { get; set; }

        public required UserSummaryDto UploadedBy { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

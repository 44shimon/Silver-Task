using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public static class TaskAttachmentMappingExtensions
    {
        public static TaskAttachmentDto ToDto(this TaskAttachment attachment) => new()
        {
            Id = attachment.Id,
            TaskId = attachment.TaskId,
            FileName = attachment.FileName,
            FileSize = attachment.FileSize,
            MimeType = attachment.MimeType,
            UploadedBy = attachment.UploadedBy!.ToSummaryDto(),
            CreatedAt = attachment.CreatedAt
        };
    }
}

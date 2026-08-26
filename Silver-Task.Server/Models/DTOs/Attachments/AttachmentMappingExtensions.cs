using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public static class AttachmentMappingExtensions
    {
        /// <summary>Requires the caller to have loaded whichever of Project/Task(.Project)/
        /// Comment(.Task.Project) applies to this attachment — AttachmentService's loaders always
        /// include the right chain for the resource type being queried.</summary>
        public static AttachmentDto ToDto(this Attachment attachment) => new()
        {
            Id = attachment.Id,
            ProjectId = attachment.ProjectId,
            TaskId = attachment.TaskId,
            CommentId = attachment.CommentId,
            FileName = attachment.FileName,
            FileSize = attachment.FileSize,
            MimeType = attachment.MimeType,
            FileHash = attachment.FileHash,
            UploadedBy = attachment.UploadedBy!.ToSummaryDto(),
            IsDeleted = attachment.IsDeleted,
            DeletedAt = attachment.DeletedAt,
            DeletedBy = attachment.DeletedByUser?.ToSummaryDto(),
            Location = DescribeLocation(attachment),
            CreatedAt = attachment.CreatedAt,
            UpdatedAt = attachment.UpdatedAt
        };

        private static string DescribeLocation(Attachment attachment)
        {
            if (attachment.Project is Project project)
            {
                return project.Name;
            }
            if (attachment.Task is TaskItem task)
            {
                return task.Project is Project taskProject ? $"{taskProject.Name} → {task.Title}" : task.Title;
            }
            if (attachment.Comment is TaskComment comment)
            {
                var taskTitle = comment.Task?.Title ?? "a task";
                var projectName = comment.Task?.Project?.Name;
                return projectName is not null ? $"{projectName} → {taskTitle} (comment)" : $"{taskTitle} (comment)";
            }
            return "Unknown";
        }
    }
}

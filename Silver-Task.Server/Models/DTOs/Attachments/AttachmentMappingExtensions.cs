using Silver_Task.Server.Models.DTOs.FileCategories;
using Silver_Task.Server.Models.DTOs.Tags;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public static class AttachmentMappingExtensions
    {
        /// <summary>Requires the caller to have loaded whichever of Project/Task(.Project)/
        /// Comment(.Task.Project) applies to this attachment, plus Folder/Category/FileTags.Tag
        /// (Phase 34) — AttachmentService's loaders always include the right chain for the
        /// resource/query shape being used. <paramref name="isFavorite"/> defaults to false;
        /// callers that know the current caller's favorited-id set should pass it explicitly (see
        /// IAttachmentService.GetFavoritedFileIdsAsync).</summary>
        public static AttachmentDto ToDto(this Attachment attachment, bool isFavorite = false) => new()
        {
            Id = attachment.Id,
            ProjectId = attachment.ProjectId,
            EffectiveProjectId = ResolveEffectiveProjectId(attachment),
            TaskId = attachment.TaskId,
            CommentId = attachment.CommentId,
            FolderId = attachment.FolderId,
            FolderName = attachment.Folder?.Name,
            FileName = attachment.FileName,
            FileSize = attachment.FileSize,
            MimeType = attachment.MimeType,
            FileHash = attachment.FileHash,
            Description = attachment.Description,
            Category = attachment.Category?.ToDto(),
            Tags = [.. attachment.FileTags.Where(ft => ft.Tag is not null).Select(ft => ft.Tag!.ToDto())],
            IsFavorite = isFavorite,
            UploadedBy = attachment.UploadedBy!.ToSummaryDto(),
            IsDeleted = attachment.IsDeleted,
            DeletedAt = attachment.DeletedAt,
            DeletedBy = attachment.DeletedByUser?.ToSummaryDto(),
            Location = DescribeLocation(attachment),
            CreatedAt = attachment.CreatedAt,
            UpdatedAt = attachment.UpdatedAt
        };

        /// <summary>The project a file's folder-move/category pickers should operate against
        /// (Phase 34) — unlike ProjectId (null for task/comment attachments, an accurate
        /// statement of "this isn't a project-level file"), this always resolves to *some*
        /// project, since every attachment belongs to exactly one, directly or via its task/
        /// comment. Mirrors AttachmentService.ResolveAccessContext's own branching.</summary>
        private static Guid ResolveEffectiveProjectId(Attachment attachment)
        {
            if (attachment.Project is Project project)
            {
                return project.Id;
            }
            if (attachment.Task is TaskItem task)
            {
                return task.ProjectId;
            }
            if (attachment.Comment?.Task is TaskItem commentTask)
            {
                return commentTask.ProjectId;
            }
            return Guid.Empty;
        }

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

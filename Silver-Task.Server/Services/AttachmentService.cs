using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IAttachmentService
    {
        Task<IReadOnlyList<TaskAttachment>> GetAllForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskAttachment> UploadAsync(Guid taskId, IFormFile file, Guid callerId, UserRole callerRole);

        Task<(TaskAttachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, Guid callerId, UserRole callerRole);

        /// <summary>The uploader may always remove their own attachment; otherwise the manage tier applies (same as deleting a task).</summary>
        Task DeleteAsync(Guid attachmentId, Guid callerId, UserRole callerRole);
    }

    /// <summary>
    /// Local-disk storage, deliberately — the spec asks for the database/API architecture for
    /// attachments now and explicitly says not to build "complicated object storage" yet. Files
    /// live outside wwwroot (not directly web-accessible; every read goes through DownloadAsync's
    /// authorization check) under GUID-based names on disk, while the original filename is kept
    /// only in the database for display/download — this avoids path traversal and collisions
    /// without trusting anything about the client-supplied name for the actual file path.
    /// </summary>
    public class AttachmentService(AppDbContext db, IProjectAccessService projectAccess, IConfiguration configuration, IWebHostEnvironment environment) : IAttachmentService
    {
        // 25 MB — generous enough for phone photos and scanned PDF documents (this app's actual
        // domain: permits, inspections), while still guarding against unbounded uploads.
        private const long MaxFileSizeBytes = 25 * 1024 * 1024;

        private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".sh", ".ps1", ".msi", ".com", ".scr"
        };

        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly string _storageRoot = ResolveStorageRoot(configuration, environment);

        public async Task<IReadOnlyList<TaskAttachment>> GetAllForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            return await _db.TaskAttachments
                .Include(a => a.UploadedBy)
                .Where(a => a.TaskId == taskId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<TaskAttachment> UploadAsync(Guid taskId, IFormFile file, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            if (file.Length == 0)
            {
                throw new ValidationException("The uploaded file is empty.");
            }
            if (file.Length > MaxFileSizeBytes)
            {
                throw new ValidationException($"Files must be {MaxFileSizeBytes / (1024 * 1024)} MB or smaller.");
            }

            var originalFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalFileName);
            if (BlockedExtensions.Contains(extension))
            {
                throw new ValidationException($"Files with extension '{extension}' are not allowed.");
            }

            var attachmentId = Guid.NewGuid();
            var relativePath = Path.Combine(taskId.ToString(), $"{attachmentId}{extension}");
            var fullPath = Path.Combine(_storageRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using (var destination = File.Create(fullPath))
            {
                await file.CopyToAsync(destination);
            }

            var attachment = new TaskAttachment
            {
                Id = attachmentId,
                TaskId = taskId,
                FileName = originalFileName,
                FileSize = file.Length,
                MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                StoragePath = relativePath,
                UploadedByUserId = callerId
            };

            _db.TaskAttachments.Add(attachment);
            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "AttachmentAdded",
                NewValue = originalFileName
            });

            await _db.SaveChangesAsync();

            attachment.UploadedBy = await _db.Users.FindAsync(callerId);
            return attachment;
        }

        public async Task<(TaskAttachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, Guid callerId, UserRole callerRole)
        {
            var attachment = await LoadAttachmentAsync(attachmentId);
            await _projectAccess.EnsureCanParticipateAsync(attachment.Task!.ProjectId, attachment.Task.Project!.OwnerId, callerId, callerRole);

            var fullPath = Path.Combine(_storageRoot, attachment.StoragePath);
            if (!File.Exists(fullPath))
            {
                throw new NotFoundException("The attached file could not be found in storage.");
            }

            return (attachment, File.OpenRead(fullPath));
        }

        public async Task DeleteAsync(Guid attachmentId, Guid callerId, UserRole callerRole)
        {
            var attachment = await LoadAttachmentAsync(attachmentId);
            var task = attachment.Task!;

            if (attachment.UploadedByUserId != callerId)
            {
                await _projectAccess.EnsureCanManageAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            }
            else
            {
                await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            }

            var fullPath = Path.Combine(_storageRoot, attachment.StoragePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            _db.TaskAttachments.Remove(attachment);
            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = callerId,
                Action = "AttachmentRemoved",
                OldValue = attachment.FileName
            });

            await _db.SaveChangesAsync();
        }

        private async Task<TaskItem> LoadTaskAsync(Guid taskId)
        {
            var task = await _db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
            return task ?? throw new NotFoundException($"Task '{taskId}' was not found.");
        }

        private async Task<TaskAttachment> LoadAttachmentAsync(Guid attachmentId)
        {
            var attachment = await _db.TaskAttachments
                .Include(a => a.UploadedBy)
                .Include(a => a.Task)
                    .ThenInclude(t => t!.Project)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
            return attachment ?? throw new NotFoundException($"Attachment '{attachmentId}' was not found.");
        }

        private static string ResolveStorageRoot(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configuredRoot = configuration["Attachments:StorageRoot"];
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                configuredRoot = "App_Data/attachments";
            }
            return Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(environment.ContentRootPath, configuredRoot);
        }
    }
}

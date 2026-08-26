using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IAttachmentService
    {
        Task<IReadOnlyList<Attachment>> GetAllForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<Attachment> UploadForTaskAsync(Guid taskId, IFormFile file, Guid callerId, UserRole callerRole);

        /// <param name="onlyDeleted">False (default): the normal, active file list any project
        /// participant can view. True: the soft-deleted list, Manage-tier only — backs the
        /// Restore UI.</param>
        /// <param name="search">Case-insensitive substring match against FileName.</param>
        /// <param name="type">One of pdf/image/spreadsheet/document/archive/other — bucketed off
        /// MimeType; null/omitted means no type filter.</param>
        /// <param name="sortField">One of name/date/size/type/uploadedBy; unrecognized/null falls
        /// back to date (CreatedAt), matching every other list view's default in this app.</param>
        Task<(IReadOnlyList<Attachment> Items, int TotalCount)> GetAllForProjectAsync(
            Guid projectId, Guid callerId, UserRole callerRole, bool onlyDeleted, int page, int pageSize,
            string? search = null, string? type = null, Guid? uploadedByUserId = null,
            DateTime? dateFrom = null, DateTime? dateTo = null,
            string? sortField = null, bool sortDescending = true);

        Task<Attachment> UploadForProjectAsync(Guid projectId, IFormFile file, Guid callerId, UserRole callerRole);

        Task<IReadOnlyList<Attachment>> GetAllForCommentAsync(Guid commentId, Guid callerId, UserRole callerRole);

        Task<Attachment> UploadForCommentAsync(Guid commentId, IFormFile file, Guid callerId, UserRole callerRole);

        Task<Attachment> GetByIdAsync(Guid attachmentId, Guid callerId, UserRole callerRole);

        Task<(Attachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, Guid callerId, UserRole callerRole);

        /// <summary>Metadata-only — never touches the file on disk (StoragePath is immutable
        /// after upload).</summary>
        Task<Attachment> RenameAsync(Guid attachmentId, string newFileName, Guid callerId, UserRole callerRole);

        /// <summary>Soft delete — see Attachment.IsDeleted. The uploader may always remove their
        /// own attachment (still edit-tier, never a Viewer); otherwise the manage tier applies,
        /// same rule DeleteAsync always used before Phase 33 generalized this entity.</summary>
        Task DeleteAsync(Guid attachmentId, Guid callerId, UserRole callerRole);

        /// <summary>Manage-tier only, regardless of who deleted or uploaded the file — per spec,
        /// restore is an administrator/project-manager action, not a self-service one.</summary>
        Task<Attachment> RestoreAsync(Guid attachmentId, Guid callerId, UserRole callerRole);

        Task<StorageHealth> GetStorageHealthAsync();
    }

    public record StorageHealth(bool IsWritable, string Provider, string RootPath, int FileCount, long TotalBytes);

    /// <summary>
    /// Local-disk storage, deliberately (unchanged from the original Task-only design this
    /// generalizes — see Attachment's own doc comment) — no MinIO/S3 exists anywhere in this
    /// codebase to reuse, and introducing one now would be a stack change out of scope for this
    /// phase. Files live outside wwwroot under GUID-based names on disk, organized as
    /// attachments/{projects|tasks|comments}/{resourceId}/{attachmentId}{extension} — the
    /// client-supplied original filename is kept only as display metadata (Attachment.FileName)
    /// and never used to build a filesystem path.
    ///
    /// Project-level attachments have no existing Activity History to log into — TaskActivity is
    /// strictly task-scoped (a non-nullable TaskId FK) and there is no project-level activity log
    /// anywhere in this app (same gap already disclosed for project role changes in Phase 32).
    /// Per "do not create a second activity system," project file events are simply not logged
    /// beyond the attachment row's own CreatedAt/UploadedBy/UpdatedAt/DeletedAt/DeletedByUserId —
    /// a disclosed, deliberate limitation, not an oversight.
    /// </summary>
    public class AttachmentService(
        AppDbContext db,
        IProjectAccessService projectAccess,
        ISystemSettingsService systemSettings,
        IConfiguration configuration,
        IWebHostEnvironment environment) : IAttachmentService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly string _storageRoot = ResolveStorageRoot(configuration, environment);

        public async Task<IReadOnlyList<Attachment>> GetAllForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            return await _db.Attachments
                .Include(a => a.UploadedBy)
                .Where(a => a.TaskId == taskId && !a.IsDeleted)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<Attachment> UploadForTaskAsync(Guid taskId, IFormFile file, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            var attachment = await BuildAndWriteAttachmentAsync(file, "tasks", taskId, callerId, projectId: null, taskId: taskId, commentId: null);
            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "AttachmentAdded",
                NewValue = attachment.FileName
            });
            await SaveWithCleanupAsync(attachment);

            attachment.UploadedBy = await _db.Users.FindAsync(callerId);
            return attachment;
        }

        public async Task<(IReadOnlyList<Attachment> Items, int TotalCount)> GetAllForProjectAsync(
            Guid projectId, Guid callerId, UserRole callerRole, bool onlyDeleted, int page, int pageSize,
            string? search = null, string? type = null, Guid? uploadedByUserId = null,
            DateTime? dateFrom = null, DateTime? dateTo = null,
            string? sortField = null, bool sortDescending = true)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            if (onlyDeleted)
            {
                await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.Attachments.Where(a => a.ProjectId == projectId && a.IsDeleted == onlyDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => EF.Functions.ILike(a.FileName, $"%{search}%"));
            }
            if (uploadedByUserId is Guid uploader)
            {
                query = query.Where(a => a.UploadedByUserId == uploader);
            }
            if (dateFrom is DateTime from)
            {
                query = query.Where(a => a.CreatedAt >= from);
            }
            if (dateTo is DateTime to)
            {
                query = query.Where(a => a.CreatedAt < to);
            }
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = type.ToLowerInvariant() switch
                {
                    "pdf" => query.Where(a => a.MimeType == "application/pdf"),
                    "image" => query.Where(a => a.MimeType.StartsWith("image/")),
                    "spreadsheet" => query.Where(a =>
                        a.MimeType == "text/csv" ||
                        a.MimeType == "application/vnd.ms-excel" ||
                        a.MimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
                    "document" => query.Where(a =>
                        a.MimeType == "text/plain" ||
                        a.MimeType == "application/msword" ||
                        a.MimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
                    "archive" => query.Where(a => a.MimeType == "application/zip" || a.MimeType == "application/x-zip-compressed"),
                    "other" => query.Where(a =>
                        a.MimeType != "application/pdf" &&
                        !a.MimeType.StartsWith("image/") &&
                        a.MimeType != "text/csv" && a.MimeType != "application/vnd.ms-excel" &&
                        a.MimeType != "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" &&
                        a.MimeType != "text/plain" && a.MimeType != "application/msword" &&
                        a.MimeType != "application/vnd.openxmlformats-officedocument.wordprocessingml.document" &&
                        a.MimeType != "application/zip" && a.MimeType != "application/x-zip-compressed"),
                    _ => query
                };
            }

            var totalCount = await query.CountAsync();

            IOrderedQueryable<Attachment> sorted = sortField?.ToLowerInvariant() switch
            {
                "name" => sortDescending ? query.OrderByDescending(a => a.FileName) : query.OrderBy(a => a.FileName),
                "size" => sortDescending ? query.OrderByDescending(a => a.FileSize) : query.OrderBy(a => a.FileSize),
                "type" => sortDescending ? query.OrderByDescending(a => a.MimeType) : query.OrderBy(a => a.MimeType),
                "uploadedby" => sortDescending
                    ? query.OrderByDescending(a => a.UploadedBy!.Name)
                    : query.OrderBy(a => a.UploadedBy!.Name),
                _ => sortDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
            };

            var items = await sorted
                .Include(a => a.UploadedBy)
                .Include(a => a.DeletedByUser)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Attachment> UploadForProjectAsync(Guid projectId, IFormFile file, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanEditAsync(project.Id, project.OwnerId, callerId, callerRole);

            var attachment = await BuildAndWriteAttachmentAsync(file, "projects", projectId, callerId, projectId: projectId, taskId: null, commentId: null);
            await SaveWithCleanupAsync(attachment);

            attachment.UploadedBy = await _db.Users.FindAsync(callerId);
            return attachment;
        }

        public async Task<IReadOnlyList<Attachment>> GetAllForCommentAsync(Guid commentId, Guid callerId, UserRole callerRole)
        {
            var comment = await LoadCommentAsync(commentId);
            await _projectAccess.EnsureCanParticipateAsync(comment.Task!.ProjectId, comment.Task.Project!.OwnerId, callerId, callerRole);

            return await _db.Attachments
                .Include(a => a.UploadedBy)
                .Where(a => a.CommentId == commentId && !a.IsDeleted)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<Attachment> UploadForCommentAsync(Guid commentId, IFormFile file, Guid callerId, UserRole callerRole)
        {
            var comment = await LoadCommentAsync(commentId);
            var task = comment.Task!;
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            var attachment = await BuildAndWriteAttachmentAsync(file, "comments", commentId, callerId, projectId: null, taskId: null, commentId: commentId);
            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = callerId,
                Action = "AttachmentAdded",
                NewValue = attachment.FileName
            });
            await SaveWithCleanupAsync(attachment);

            attachment.UploadedBy = await _db.Users.FindAsync(callerId);
            return attachment;
        }

        public async Task<Attachment> GetByIdAsync(Guid attachmentId, Guid callerId, UserRole callerRole)
        {
            var attachment = await LoadAttachmentAsync(attachmentId);
            var (projectId, ownerId) = ResolveAccessContext(attachment);
            await _projectAccess.EnsureCanParticipateAsync(projectId, ownerId, callerId, callerRole);
            return attachment;
        }

        public async Task<(Attachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, Guid callerId, UserRole callerRole)
        {
            var attachment = await LoadAttachmentAsync(attachmentId);
            if (attachment.IsDeleted)
            {
                // Indistinguishable from "never existed" to a caller who doesn't already have
                // manage-tier visibility into the deleted list — never confirms a deleted file's
                // former existence to an ordinary participant.
                throw new NotFoundException($"Attachment '{attachmentId}' was not found.");
            }

            var (projectId, ownerId) = ResolveAccessContext(attachment);
            await _projectAccess.EnsureCanParticipateAsync(projectId, ownerId, callerId, callerRole);

            var fullPath = Path.Combine(_storageRoot, attachment.StoragePath);
            if (!File.Exists(fullPath))
            {
                throw new NotFoundException("The attached file could not be found in storage.");
            }

            return (attachment, File.OpenRead(fullPath));
        }

        public async Task<Attachment> RenameAsync(Guid attachmentId, string newFileName, Guid callerId, UserRole callerRole)
        {
            var attachment = await LoadAttachmentAsync(attachmentId);
            if (attachment.IsDeleted)
            {
                throw new NotFoundException($"Attachment '{attachmentId}' was not found.");
            }

            var (projectId, ownerId) = ResolveAccessContext(attachment);
            await EnsureCanModifyAsync(attachment, projectId, ownerId, callerId, callerRole);

            var sanitized = SanitizeFileName(newFileName);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                throw new ValidationException("File name cannot be empty.");
            }

            var oldFileName = attachment.FileName;
            if (oldFileName == sanitized)
            {
                return attachment;
            }

            attachment.FileName = sanitized;
            attachment.UpdatedAt = DateTime.UtcNow;

            if (ResolveActivityTaskId(attachment) is Guid taskId)
            {
                _db.TaskActivities.Add(new TaskActivity
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskId,
                    UserId = callerId,
                    Action = "AttachmentRenamed",
                    OldValue = oldFileName,
                    NewValue = sanitized
                });
            }

            await _db.SaveChangesAsync();
            return attachment;
        }

        public async Task DeleteAsync(Guid attachmentId, Guid callerId, UserRole callerRole)
        {
            var attachment = await LoadAttachmentAsync(attachmentId);
            if (attachment.IsDeleted)
            {
                return;
            }

            var (projectId, ownerId) = ResolveAccessContext(attachment);
            await EnsureCanModifyAsync(attachment, projectId, ownerId, callerId, callerRole);

            attachment.IsDeleted = true;
            attachment.DeletedAt = DateTime.UtcNow;
            attachment.DeletedByUserId = callerId;
            attachment.UpdatedAt = DateTime.UtcNow;
            // The physical file is deliberately left on disk — see Attachment's own doc comment
            // (Restore needs it; a retention/purge job is explicitly out of scope this phase).

            if (ResolveActivityTaskId(attachment) is Guid taskId)
            {
                _db.TaskActivities.Add(new TaskActivity
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskId,
                    UserId = callerId,
                    Action = "AttachmentRemoved",
                    OldValue = attachment.FileName
                });
            }

            await _db.SaveChangesAsync();
        }

        public async Task<Attachment> RestoreAsync(Guid attachmentId, Guid callerId, UserRole callerRole)
        {
            var attachment = await LoadAttachmentAsync(attachmentId);
            if (!attachment.IsDeleted)
            {
                return attachment;
            }

            var (projectId, ownerId) = ResolveAccessContext(attachment);
            await _projectAccess.EnsureCanManageAsync(projectId, ownerId, callerId, callerRole);

            var fullPath = Path.Combine(_storageRoot, attachment.StoragePath);
            if (!File.Exists(fullPath))
            {
                throw new ConflictException("This file's storage object no longer exists and cannot be restored.");
            }

            attachment.IsDeleted = false;
            attachment.DeletedAt = null;
            attachment.DeletedByUserId = null;
            attachment.UpdatedAt = DateTime.UtcNow;

            if (ResolveActivityTaskId(attachment) is Guid taskId)
            {
                _db.TaskActivities.Add(new TaskActivity
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskId,
                    UserId = callerId,
                    Action = "AttachmentRestored",
                    NewValue = attachment.FileName
                });
            }

            await _db.SaveChangesAsync();
            return attachment;
        }

        public Task<StorageHealth> GetStorageHealthAsync()
        {
            var isWritable = true;
            try
            {
                Directory.CreateDirectory(_storageRoot);
                var probePath = Path.Combine(_storageRoot, $".health-{Guid.NewGuid():N}");
                File.WriteAllText(probePath, string.Empty);
                File.Delete(probePath);
            }
            catch
            {
                isWritable = false;
            }

            var fileCount = 0;
            long totalBytes = 0;
            if (Directory.Exists(_storageRoot))
            {
                foreach (var path in Directory.EnumerateFiles(_storageRoot, "*", SearchOption.AllDirectories))
                {
                    fileCount++;
                    totalBytes += new FileInfo(path).Length;
                }
            }

            return Task.FromResult(new StorageHealth(isWritable, "Local Disk", _storageRoot, fileCount, totalBytes));
        }

        /// <summary>Uploader may always modify (rename/delete) their own attachment at edit-tier;
        /// anyone else needs manage-tier — the exact rule this entity's Delete used before Phase
        /// 33 generalized it beyond tasks.</summary>
        private async Task EnsureCanModifyAsync(Attachment attachment, Guid projectId, Guid ownerId, Guid callerId, UserRole callerRole)
        {
            if (attachment.UploadedByUserId != callerId)
            {
                await _projectAccess.EnsureCanManageAsync(projectId, ownerId, callerId, callerRole);
            }
            else
            {
                await _projectAccess.EnsureCanEditAsync(projectId, ownerId, callerId, callerRole);
            }
        }

        private static Guid? ResolveActivityTaskId(Attachment attachment) =>
            attachment.TaskId ?? attachment.Comment?.TaskId;

        private static (Guid ProjectId, Guid OwnerId) ResolveAccessContext(Attachment attachment)
        {
            if (attachment.Project is Project project)
            {
                return (project.Id, project.OwnerId);
            }
            if (attachment.Task is TaskItem task)
            {
                return (task.ProjectId, task.Project!.OwnerId);
            }
            if (attachment.Comment is TaskComment comment)
            {
                return (comment.Task!.ProjectId, comment.Task.Project!.OwnerId);
            }
            throw new InvalidOperationException($"Attachment '{attachment.Id}' has no resolvable parent resource.");
        }

        /// <summary>Validates, writes the file to disk (hashing it in the same pass), and tracks
        /// a new Attachment entity — deliberately does not call SaveChangesAsync, so a caller
        /// that also needs to add a TaskActivity in the same unit of work still persists both
        /// together via SaveWithCleanupAsync.</summary>
        private async Task<Attachment> BuildAndWriteAttachmentAsync(
            IFormFile file, string resourceFolder, Guid resourceFolderId, Guid callerId,
            Guid? projectId, Guid? taskId, Guid? commentId)
        {
            if (!await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowAttachments))
            {
                throw new ForbiddenException("Attachments are currently disabled by an Administrator.");
            }

            if (file.Length == 0)
            {
                throw new ValidationException("The uploaded file is empty.");
            }

            var maxSizeBytes = await _systemSettings.GetIntAsync(SystemSettingKeys.MaxAttachmentSizeMb) * 1024L * 1024L;
            if (file.Length > maxSizeBytes)
            {
                throw new ValidationException($"Files must be {maxSizeBytes / (1024 * 1024)} MB or smaller.");
            }

            var originalFileName = SanitizeFileName(Path.GetFileName(file.FileName));
            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                throw new ValidationException("The uploaded file must have a name.");
            }

            var extension = Path.GetExtension(originalFileName);
            var allowedExtensions = AttachmentValidation.ParseAllowedExtensions(
                await _systemSettings.GetStringAsync(SystemSettingKeys.AllowedAttachmentExtensions));
            if (string.IsNullOrEmpty(extension) || !AttachmentValidation.IsExtensionAllowed(extension, allowedExtensions))
            {
                var shown = string.IsNullOrEmpty(extension) ? "(none)" : extension;
                throw new ValidationException($"Files with extension '{shown}' are not allowed.");
            }

            if (!AttachmentValidation.IsContentTypeConsistent(extension, file.ContentType))
            {
                throw new ValidationException($"'{originalFileName}' does not appear to be a valid {extension} file.");
            }

            var attachmentId = Guid.NewGuid();
            // Never derived from the client-supplied filename — attachments/{resourceFolder}/
            // {resourceFolderId}/{attachmentId}{extension}, entirely server-generated.
            var relativePath = Path.Combine("attachments", resourceFolder, resourceFolderId.ToString(), $"{attachmentId}{extension}");
            var fullPath = Path.Combine(_storageRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            string fileHash;
            try
            {
                await using var sourceStream = file.OpenReadStream();

                // Peek the header for magic-byte verification, then rewind — ASP.NET's IFormFile
                // streams (in-memory or temp-file-buffered, per the multipart body size) are
                // seekable, so this is one read of the stream, not two.
                var header = new byte[16];
                var headerBytesRead = await sourceStream.ReadAsync(header.AsMemory(0, header.Length));
                if (!AttachmentValidation.IsSignatureConsistent(extension, header.AsSpan(0, headerBytesRead)))
                {
                    throw new ValidationException($"'{originalFileName}' does not appear to be a valid {extension} file.");
                }
                sourceStream.Position = 0;

                using var sha256 = SHA256.Create();
                await using (var destination = File.Create(fullPath))
                await using (var hashingStream = new CryptoStream(destination, sha256, CryptoStreamMode.Write))
                {
                    await sourceStream.CopyToAsync(hashingStream);
                }
                fileHash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
            }
            catch
            {
                // Storage write failed (or failed validation after the file was partially
                // written) — never leave a partial/broken file behind.
                TryDeleteFile(fullPath);
                throw;
            }

            var attachment = new Attachment
            {
                Id = attachmentId,
                ProjectId = projectId,
                TaskId = taskId,
                CommentId = commentId,
                FileName = originalFileName,
                FileSize = file.Length,
                MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                StoragePath = relativePath,
                FileHash = fileHash,
                UploadedByUserId = callerId
            };
            _db.Attachments.Add(attachment);
            return attachment;
        }

        /// <summary>Persists the tracked attachment (and anything else added to the same unit of
        /// work, e.g. a TaskActivity) — if the database save fails after the file was already
        /// written to disk, the orphaned file is cleaned up rather than left behind with no
        /// matching record.</summary>
        private async Task SaveWithCleanupAsync(Attachment attachment)
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                TryDeleteFile(Path.Combine(_storageRoot, attachment.StoragePath));
                throw;
            }
        }

        private static void TryDeleteFile(string fullPath)
        {
            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch
            {
                // Best-effort cleanup only — the original exception is what the caller should see.
            }
        }

        /// <summary>Collapses whitespace and strips control characters from a *display* filename
        /// — this is not the path-traversal guard (Path.GetFileName already strips any directory
        /// component before this runs, and StoragePath is always a separate, GUID-based value
        /// never derived from this at all); it only keeps the metadata column and UI reasonable.</summary>
        private static string SanitizeFileName(string fileName)
        {
            var withoutControlChars = new string(fileName.Where(c => !char.IsControl(c)).ToArray()).Trim();
            var collapsed = System.Text.RegularExpressions.Regex.Replace(withoutControlChars, @"\s+", " ");
            return collapsed.Length > 255 ? collapsed[..255] : collapsed;
        }

        private async Task<TaskItem> LoadTaskAsync(Guid taskId)
        {
            var task = await _db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
            return task ?? throw new NotFoundException($"Task '{taskId}' was not found.");
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        private async Task<TaskComment> LoadCommentAsync(Guid commentId)
        {
            var comment = await _db.TaskComments
                .Include(c => c.Task)
                    .ThenInclude(t => t!.Project)
                .FirstOrDefaultAsync(c => c.Id == commentId);
            return comment ?? throw new NotFoundException($"Comment '{commentId}' was not found.");
        }

        private async Task<Attachment> LoadAttachmentAsync(Guid attachmentId)
        {
            var attachment = await _db.Attachments
                .Include(a => a.Project)
                .Include(a => a.Task)
                    .ThenInclude(t => t!.Project)
                .Include(a => a.Comment)
                    .ThenInclude(c => c!.Task)
                        .ThenInclude(t => t!.Project)
                .Include(a => a.UploadedBy)
                .Include(a => a.DeletedByUser)
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

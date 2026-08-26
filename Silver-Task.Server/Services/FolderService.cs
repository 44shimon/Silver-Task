using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IFolderService
    {
        /// <param name="includeDeleted">Manage-tier only — backs the Restore UI, same convention
        /// as IAttachmentService.GetAllForProjectAsync's onlyDeleted.</param>
        Task<IReadOnlyList<Folder>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole, bool includeDeleted = false);

        Task<Folder> GetByIdAsync(Guid folderId, Guid callerId, UserRole callerRole);

        Task<Folder> CreateAsync(Guid projectId, string name, Guid? parentFolderId, Guid callerId, UserRole callerRole);

        /// <summary>Metadata-only — a folder has no physical storage location of its own to move.</summary>
        Task<Folder> RenameAsync(Guid folderId, string newName, Guid callerId, UserRole callerRole);

        Task<Folder> MoveAsync(Guid folderId, Guid? newParentFolderId, Guid callerId, UserRole callerRole);

        /// <summary>Counts every file and subfolder that would be affected — backs the "This
        /// folder contains N files and M subfolders" confirmation before an actual delete.</summary>
        Task<(int FileCount, int SubfolderCount)> GetDeletePreviewAsync(Guid folderId, Guid callerId, UserRole callerRole);

        Task DeleteAsync(Guid folderId, FolderDeleteMode mode, Guid callerId, UserRole callerRole);

        /// <summary>Manage-tier only, regardless of who created or deleted the folder — mirrors
        /// IAttachmentService.RestoreAsync exactly. Refuses to restore into a parent that's itself
        /// deleted or gone (the spec's "never restore into a location the user no longer has
        /// permission to access" — a missing/deleted parent is the concrete case that can actually
        /// happen here, since project access itself is re-checked by EnsureCanManageAsync above).</summary>
        Task<Folder> RestoreAsync(Guid folderId, Guid callerId, UserRole callerRole);
    }

    /// <summary>
    /// Project-scoped folder hierarchy (Phase 34) — mirrors TaskService's ParentTaskId hierarchy
    /// handling (Phase 30) almost exactly: same IsDescendantOfAsync circular-check shape, same
    /// breadth-first CollectDescendantsAsync, same reparent-or-cascade delete choice. Folders never
    /// span projects — a folder's ParentFolderId must reference a folder in the same ProjectId,
    /// enforced on both create and move.
    /// </summary>
    public class FolderService(AppDbContext db, IProjectAccessService projectAccess) : IFolderService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;

        public async Task<IReadOnlyList<Folder>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole, bool includeDeleted = false)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            if (includeDeleted)
            {
                await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);
                return await _db.Folders
                    .Include(f => f.CreatedBy)
                    .Include(f => f.DeletedByUser)
                    .Where(f => f.ProjectId == projectId && f.IsDeleted)
                    .OrderBy(f => f.Name)
                    .ToListAsync();
            }

            return await _db.Folders
                .Include(f => f.CreatedBy)
                .Where(f => f.ProjectId == projectId && !f.IsDeleted)
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<Folder> GetByIdAsync(Guid folderId, Guid callerId, UserRole callerRole)
        {
            var folder = await LoadFolderAsync(folderId);
            await _projectAccess.EnsureCanParticipateAsync(folder.ProjectId, folder.Project!.OwnerId, callerId, callerRole);
            return folder;
        }

        public async Task<Folder> CreateAsync(Guid projectId, string name, Guid? parentFolderId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanEditAsync(project.Id, project.OwnerId, callerId, callerRole);

            var sanitized = ValidateName(name);

            if (parentFolderId is Guid parentId)
            {
                var parent = await LoadFolderAsync(parentId);
                if (parent.ProjectId != projectId)
                {
                    throw new ValidationException("Parent and child folders must belong to the same project.");
                }
            }

            await EnsureNameIsAvailableAsync(projectId, parentFolderId, sanitized, excludingFolderId: null);

            var folder = new Folder
            {
                Id = Guid.NewGuid(),
                Name = sanitized,
                ParentFolderId = parentFolderId,
                ProjectId = projectId,
                CreatedByUserId = callerId
            };
            _db.Folders.Add(folder);
            await _db.SaveChangesAsync();

            folder.CreatedBy = await _db.Users.FindAsync(callerId);
            return folder;
        }

        public async Task<Folder> RenameAsync(Guid folderId, string newName, Guid callerId, UserRole callerRole)
        {
            var folder = await LoadFolderAsync(folderId);
            await EnsureCanModifyAsync(folder, callerId, callerRole);

            var sanitized = ValidateName(newName);
            if (sanitized == folder.Name)
            {
                return folder;
            }

            await EnsureNameIsAvailableAsync(folder.ProjectId, folder.ParentFolderId, sanitized, excludingFolderId: folder.Id);

            folder.Name = sanitized;
            folder.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return folder;
        }

        public async Task<Folder> MoveAsync(Guid folderId, Guid? newParentFolderId, Guid callerId, UserRole callerRole)
        {
            var folder = await LoadFolderAsync(folderId);
            await EnsureCanModifyAsync(folder, callerId, callerRole);

            if (newParentFolderId == folder.ParentFolderId)
            {
                return folder;
            }

            if (newParentFolderId is Guid newParentId)
            {
                if (newParentId == folderId)
                {
                    throw new ValidationException("A folder cannot be moved into itself.");
                }

                var newParent = await LoadFolderAsync(newParentId);
                if (newParent.ProjectId != folder.ProjectId)
                {
                    throw new ValidationException("A folder can only be moved within the same project.");
                }

                // Would newParentId end up *below* folder in the tree? Walking up from
                // newParentId and finding folder means folder is already an ancestor of
                // newParentId — moving folder under newParentId would close that loop.
                if (await IsDescendantOfAsync(newParentId, folderId))
                {
                    throw new ValidationException("Cannot move this folder because it would create a circular hierarchy.");
                }
            }

            await EnsureNameIsAvailableAsync(folder.ProjectId, newParentFolderId, folder.Name, excludingFolderId: folder.Id);

            folder.ParentFolderId = newParentFolderId;
            folder.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return folder;
        }

        public async Task<(int FileCount, int SubfolderCount)> GetDeletePreviewAsync(Guid folderId, Guid callerId, UserRole callerRole)
        {
            var folder = await LoadFolderAsync(folderId);
            await _projectAccess.EnsureCanParticipateAsync(folder.ProjectId, folder.Project!.OwnerId, callerId, callerRole);

            var descendantFolderIds = await CollectDescendantIdsAsync(folderId);
            var allFolderIds = new List<Guid>(descendantFolderIds) { folderId };

            var fileCount = await _db.Attachments.CountAsync(a => a.FolderId != null && allFolderIds.Contains(a.FolderId!.Value) && !a.IsDeleted);
            return (fileCount, descendantFolderIds.Count);
        }

        public async Task DeleteAsync(Guid folderId, FolderDeleteMode mode, Guid callerId, UserRole callerRole)
        {
            var folder = await LoadFolderAsync(folderId);
            await EnsureCanModifyAsync(folder, callerId, callerRole);

            var descendantFolderIds = await CollectDescendantIdsAsync(folderId);
            var allFolderIds = new List<Guid>(descendantFolderIds) { folderId };
            var now = DateTime.UtcNow;

            if (mode == FolderDeleteMode.DeleteContents)
            {
                var files = await _db.Attachments.Where(a => a.FolderId != null && allFolderIds.Contains(a.FolderId!.Value) && !a.IsDeleted).ToListAsync();
                foreach (var file in files)
                {
                    file.IsDeleted = true;
                    file.DeletedAt = now;
                    file.DeletedByUserId = callerId;
                    file.UpdatedAt = now;
                }

                var subfolders = await _db.Folders.Where(f => descendantFolderIds.Contains(f.Id)).ToListAsync();
                foreach (var subfolder in subfolders)
                {
                    subfolder.IsDeleted = true;
                    subfolder.DeletedAt = now;
                    subfolder.DeletedByUserId = callerId;
                    subfolder.UpdatedAt = now;
                }
            }
            else
            {
                // "Move contents to parent" — only the direct children (files and subfolders) of
                // *this* folder move up; a subfolder's own descendants stay exactly where they are
                // relative to it, same reparent-only-direct-children reasoning as
                // TaskService.DeleteAsync's "delete task only" path.
                var directFiles = await _db.Attachments.Where(a => a.FolderId == folderId && !a.IsDeleted).ToListAsync();
                foreach (var file in directFiles)
                {
                    file.FolderId = folder.ParentFolderId;
                    file.UpdatedAt = now;
                }

                var directSubfolders = await _db.Folders.Where(f => f.ParentFolderId == folderId && !f.IsDeleted).ToListAsync();
                foreach (var subfolder in directSubfolders)
                {
                    subfolder.ParentFolderId = folder.ParentFolderId;
                    subfolder.UpdatedAt = now;
                }
            }

            folder.IsDeleted = true;
            folder.DeletedAt = now;
            folder.DeletedByUserId = callerId;
            folder.UpdatedAt = now;

            await _db.SaveChangesAsync();
        }

        public async Task<Folder> RestoreAsync(Guid folderId, Guid callerId, UserRole callerRole)
        {
            var folder = await LoadFolderAsync(folderId);
            if (!folder.IsDeleted)
            {
                return folder;
            }

            await _projectAccess.EnsureCanManageAsync(folder.ProjectId, folder.Project!.OwnerId, callerId, callerRole);

            if (folder.ParentFolderId is Guid parentId)
            {
                var parentStillValid = await _db.Folders.AnyAsync(f => f.Id == parentId && !f.IsDeleted);
                if (!parentStillValid)
                {
                    throw new ConflictException("This folder's parent no longer exists or was deleted — move it to the top level before restoring, or restore the parent first.");
                }
            }

            await EnsureNameIsAvailableAsync(folder.ProjectId, folder.ParentFolderId, folder.Name, excludingFolderId: folder.Id);

            folder.IsDeleted = false;
            folder.DeletedAt = null;
            folder.DeletedByUserId = null;
            folder.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return folder;
        }

        /// <summary>Creator may modify (rename/move/delete) at edit-tier; anyone else needs
        /// manage-tier — the same rule AttachmentService.EnsureCanModifyAsync already uses for
        /// files, applied here to folders for consistency.</summary>
        private async Task EnsureCanModifyAsync(Folder folder, Guid callerId, UserRole callerRole)
        {
            if (folder.CreatedByUserId != callerId)
            {
                await _projectAccess.EnsureCanManageAsync(folder.ProjectId, folder.Project!.OwnerId, callerId, callerRole);
            }
            else
            {
                await _projectAccess.EnsureCanEditAsync(folder.ProjectId, folder.Project!.OwnerId, callerId, callerRole);
            }
        }

        private static string ValidateName(string name)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                throw new ValidationException("Folder name cannot be empty.");
            }
            if (trimmed.Length > 255)
            {
                throw new ValidationException("Folder name cannot exceed 255 characters.");
            }
            if (trimmed.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            {
                throw new ValidationException("Folder name contains invalid characters.");
            }
            return trimmed;
        }

        private async Task EnsureNameIsAvailableAsync(Guid projectId, Guid? parentFolderId, string name, Guid? excludingFolderId)
        {
            var normalized = name.Trim().ToLower();
            var exists = await _db.Folders.AnyAsync(f =>
                f.Id != excludingFolderId &&
                f.ProjectId == projectId &&
                f.ParentFolderId == parentFolderId &&
                !f.IsDeleted &&
                f.Name.ToLower() == normalized);

            if (exists)
            {
                throw new ConflictException($"A folder named '{name}' already exists in that location.");
            }
        }

        /// <summary>True if walking up from <paramref name="startFolderId"/> via ParentFolderId
        /// ever reaches <paramref name="candidateAncestorId"/> — i.e. whether candidateAncestorId
        /// is an ancestor of (or the same folder as) startFolderId. Mirrors
        /// TaskService.IsDescendantOfAsync exactly.</summary>
        private async Task<bool> IsDescendantOfAsync(Guid startFolderId, Guid candidateAncestorId)
        {
            var currentId = (Guid?)startFolderId;
            var guard = 0;
            while (currentId is Guid id)
            {
                if (id == candidateAncestorId)
                {
                    return true;
                }
                if (++guard > 1000)
                {
                    return true;
                }
                currentId = await _db.Folders.Where(f => f.Id == id).Select(f => f.ParentFolderId).FirstOrDefaultAsync();
            }
            return false;
        }

        /// <summary>Breadth-first walk collecting every descendant folder id of
        /// <paramref name="rootFolderId"/> (not including the root itself) — one query per depth
        /// level, not one query per node. Mirrors TaskService.CollectDescendantsAsync exactly.</summary>
        private async Task<List<Guid>> CollectDescendantIdsAsync(Guid rootFolderId)
        {
            var all = new List<Guid>();
            var frontier = new List<Guid> { rootFolderId };

            while (frontier.Count > 0)
            {
                var children = await _db.Folders
                    .Where(f => f.ParentFolderId != null && frontier.Contains(f.ParentFolderId!.Value))
                    .Select(f => f.Id)
                    .ToListAsync();
                if (children.Count == 0)
                {
                    break;
                }
                all.AddRange(children);
                frontier = children;
            }

            return all;
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        private async Task<Folder> LoadFolderAsync(Guid folderId)
        {
            var folder = await _db.Folders
                .Include(f => f.Project)
                .Include(f => f.CreatedBy)
                .Include(f => f.DeletedByUser)
                .FirstOrDefaultAsync(f => f.Id == folderId);
            return folder ?? throw new NotFoundException($"Folder '{folderId}' was not found.");
        }
    }
}

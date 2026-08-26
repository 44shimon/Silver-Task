using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    public interface ITagService
    {
        /// <summary>Every active tag, for "add tag" pickers — global, not project-scoped, so no
        /// project-access check applies; any authenticated user may see the shared vocabulary.</summary>
        Task<IReadOnlyList<Tag>> GetActiveAsync();

        /// <summary>Every tag including inactive ones — Administrator only (Admin -> Tags).</summary>
        Task<IReadOnlyList<Tag>> GetAllForAdminAsync();

        /// <summary>Case-insensitive find-or-create by name, reactivating an inactive match —
        /// called by AttachmentService.AddTagAsync so any project participant with file-edit
        /// rights can introduce a new tag inline while tagging a file, without needing a separate
        /// "create tag" permission of its own.</summary>
        Task<Tag> GetOrCreateAsync(string name, Guid callerId);

        Task<Tag> RenameAsync(Guid tagId, string newName);

        Task<Tag> SetActiveAsync(Guid tagId, bool isActive);

        /// <summary>Refuses if any FileTag still references this tag — see the type's own doc
        /// comment for why deactivation, not deletion, is the recommended path for a used tag.</summary>
        Task DeleteAsync(Guid tagId);
    }

    public class TagService(AppDbContext db) : ITagService
    {
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<Tag>> GetActiveAsync() =>
            await _db.Tags.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

        public async Task<IReadOnlyList<Tag>> GetAllForAdminAsync() =>
            await _db.Tags.OrderBy(t => t.Name).ToListAsync();

        public async Task<Tag> GetOrCreateAsync(string name, Guid callerId)
        {
            var sanitized = ValidateName(name);
            var normalized = sanitized.ToLower();

            var existing = await _db.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == normalized);
            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                }
                return existing;
            }

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                Name = sanitized,
                CreatedByUserId = callerId
            };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();
            return tag;
        }

        public async Task<Tag> RenameAsync(Guid tagId, string newName)
        {
            var tag = await LoadTagAsync(tagId);
            var sanitized = ValidateName(newName);
            var normalized = sanitized.ToLower();

            var collides = await _db.Tags.AnyAsync(t => t.Id != tagId && t.Name.ToLower() == normalized);
            if (collides)
            {
                throw new ConflictException($"A tag named '{sanitized}' already exists.");
            }

            tag.Name = sanitized;
            await _db.SaveChangesAsync();
            return tag;
        }

        public async Task<Tag> SetActiveAsync(Guid tagId, bool isActive)
        {
            var tag = await LoadTagAsync(tagId);
            tag.IsActive = isActive;
            await _db.SaveChangesAsync();
            return tag;
        }

        public async Task DeleteAsync(Guid tagId)
        {
            var tag = await LoadTagAsync(tagId);
            var usageCount = await _db.FileTags.CountAsync(ft => ft.TagId == tagId);
            if (usageCount > 0)
            {
                throw new ConflictException(
                    $"'{tag.Name}' is applied to {usageCount} file{(usageCount == 1 ? "" : "s")}. Deactivate it instead to keep that data.");
            }

            _db.Tags.Remove(tag);
            await _db.SaveChangesAsync();
        }

        private static string ValidateName(string name)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                throw new ValidationException("Tag name cannot be empty.");
            }
            if (trimmed.Length > 50)
            {
                throw new ValidationException("Tag name cannot exceed 50 characters.");
            }
            return trimmed;
        }

        private async Task<Tag> LoadTagAsync(Guid tagId)
        {
            var tag = await _db.Tags.FindAsync(tagId);
            return tag ?? throw new NotFoundException($"Tag '{tagId}' was not found.");
        }
    }
}

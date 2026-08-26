using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    public interface IFileCategoryService
    {
        /// <summary>Every active category, for category pickers — global, not project-scoped.</summary>
        Task<IReadOnlyList<FileCategory>> GetActiveAsync();

        /// <summary>Every category including inactive ones — Administrator only (Admin -> File Categories).</summary>
        Task<IReadOnlyList<FileCategory>> GetAllForAdminAsync();

        Task<FileCategory> CreateAsync(string name, string? description);

        Task<FileCategory> UpdateAsync(Guid categoryId, string name, string? description);

        Task<FileCategory> SetActiveAsync(Guid categoryId, bool isActive);

        /// <summary>Refuses if any Attachment still references this category — see the type's own
        /// doc comment for why deactivation, not deletion, is the recommended path.</summary>
        Task DeleteAsync(Guid categoryId);
    }

    public class FileCategoryService(AppDbContext db) : IFileCategoryService
    {
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<FileCategory>> GetActiveAsync() =>
            await _db.FileCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

        public async Task<IReadOnlyList<FileCategory>> GetAllForAdminAsync() =>
            await _db.FileCategories.OrderBy(c => c.Name).ToListAsync();

        public async Task<FileCategory> CreateAsync(string name, string? description)
        {
            var sanitized = ValidateName(name);
            await EnsureNameIsAvailableAsync(sanitized, excludingCategoryId: null);

            var category = new FileCategory { Id = Guid.NewGuid(), Name = sanitized, Description = description?.Trim() };
            _db.FileCategories.Add(category);
            await _db.SaveChangesAsync();
            return category;
        }

        public async Task<FileCategory> UpdateAsync(Guid categoryId, string name, string? description)
        {
            var category = await LoadCategoryAsync(categoryId);
            var sanitized = ValidateName(name);
            await EnsureNameIsAvailableAsync(sanitized, excludingCategoryId: categoryId);

            category.Name = sanitized;
            category.Description = description?.Trim();
            category.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return category;
        }

        public async Task<FileCategory> SetActiveAsync(Guid categoryId, bool isActive)
        {
            var category = await LoadCategoryAsync(categoryId);
            category.IsActive = isActive;
            category.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return category;
        }

        public async Task DeleteAsync(Guid categoryId)
        {
            var category = await LoadCategoryAsync(categoryId);
            var usageCount = await _db.Attachments.CountAsync(a => a.CategoryId == categoryId);
            if (usageCount > 0)
            {
                throw new ConflictException(
                    $"'{category.Name}' is applied to {usageCount} file{(usageCount == 1 ? "" : "s")}. Deactivate it instead to keep that data.");
            }

            _db.FileCategories.Remove(category);
            await _db.SaveChangesAsync();
        }

        private static string ValidateName(string name)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                throw new ValidationException("Category name cannot be empty.");
            }
            if (trimmed.Length > 100)
            {
                throw new ValidationException("Category name cannot exceed 100 characters.");
            }
            return trimmed;
        }

        private async Task EnsureNameIsAvailableAsync(string name, Guid? excludingCategoryId)
        {
            var normalized = name.ToLower();
            var exists = await _db.FileCategories.AnyAsync(c => c.Id != excludingCategoryId && c.Name.ToLower() == normalized);
            if (exists)
            {
                throw new ConflictException($"A category named '{name}' already exists.");
            }
        }

        private async Task<FileCategory> LoadCategoryAsync(Guid categoryId)
        {
            var category = await _db.FileCategories.FindAsync(categoryId);
            return category ?? throw new NotFoundException($"File category '{categoryId}' was not found.");
        }
    }
}

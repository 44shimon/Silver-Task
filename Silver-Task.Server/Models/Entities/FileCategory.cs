namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// A global, admin-curated classification for files (Contract/Invoice/Photo/etc., Phase 34) —
    /// deliberately not project-scoped (unlike Folder), since the same fixed vocabulary is useful
    /// across every project. Mirrors CustomField's IsActive convention: administrators deactivate
    /// a category still referenced by existing files rather than deleting it out from under them
    /// (see FileCategoryService.DeleteAsync's usage-count guard).
    /// </summary>
    public class FileCategory
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

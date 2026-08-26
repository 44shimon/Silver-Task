namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// A global, reusable file label (Phase 34) — any project participant with file-edit rights
    /// can create one on the fly while tagging a file (get-or-create by name, see
    /// TagService.GetOrCreateAsync); only Administrators can rename or deactivate the shared
    /// definition itself (Admin -> Tags), since that affects every project at once.
    /// </summary>
    public class Tag
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        /// <summary>Optional hex color (e.g. "#2a9d5c") for the tag chip — purely cosmetic,
        /// never required.</summary>
        public string? Color { get; set; }

        public Guid CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>Deactivated tags stop appearing in "add tag" pickers but existing FileTag
        /// links are left alone — same deactivate-don't-destroy convention as FileCategory.</summary>
        public bool IsActive { get; set; } = true;

        public User? CreatedBy { get; set; }
    }
}

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// Project-scoped file organization (Phase 34) — folders always belong to exactly one
    /// Project and optionally nest under another Folder in that same project (ParentFolderId),
    /// mirroring TaskItem's own ParentTaskId self-reference (Phase 30) rather than inventing a
    /// new hierarchy pattern. Attachment.FolderId (nullable) is the only thing that actually
    /// places a file "in" a folder — a folder never determines location from the on-disk
    /// StoragePath, which stays a separate, purely physical concern.
    /// </summary>
    public class Folder
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public Guid? ParentFolderId { get; set; }

        public Guid ProjectId { get; set; }

        public Guid CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        /// <summary>Soft delete, same shape as Attachment/User — a deleted folder's contents are
        /// always explicitly moved-to-parent or soft-deleted-along-with-it first (see
        /// FolderService.DeleteAsync); never destroyed implicitly by a bare cascade.</summary>
        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public Guid? DeletedByUserId { get; set; }

        public Project? Project { get; set; }

        public Folder? ParentFolder { get; set; }

        public ICollection<Folder> Subfolders { get; set; } = [];

        public User? CreatedBy { get; set; }

        public User? DeletedByUser { get; set; }
    }
}

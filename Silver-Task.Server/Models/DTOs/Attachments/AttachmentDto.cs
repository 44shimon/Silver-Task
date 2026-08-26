using Silver_Task.Server.Models.DTOs.FileCategories;
using Silver_Task.Server.Models.DTOs.Tags;
using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class AttachmentDto
    {
        public Guid Id { get; set; }

        public Guid? ProjectId { get; set; }

        /// <summary>Always resolves to the file's owning project, even for a task/comment
        /// attachment (where ProjectId itself is null) — see AttachmentMappingExtensions'
        /// ResolveEffectiveProjectId. Used by the frontend's folder-move/category pickers on
        /// cross-project views (Favorites, Recent) that can't rely on a single page-level
        /// projectId.</summary>
        public Guid EffectiveProjectId { get; set; }

        public Guid? TaskId { get; set; }

        public Guid? CommentId { get; set; }

        public Guid? FolderId { get; set; }

        public string? FolderName { get; set; }

        public required string FileName { get; set; }

        public long FileSize { get; set; }

        public required string MimeType { get; set; }

        public string? FileHash { get; set; }

        public string? Description { get; set; }

        public FileCategoryDto? Category { get; set; }

        public required List<TagDto> Tags { get; set; }

        /// <summary>Whether the *requesting* caller has favorited this file — per-user, never a
        /// property of the file itself (see UserFileFavorite's own doc comment). Defaults to false
        /// when the mapping caller doesn't supply a favorited-id set (e.g. contexts where it's
        /// irrelevant, like the Admin view of a deleted-files list).</summary>
        public bool IsFavorite { get; set; }

        public required UserSummaryDto UploadedBy { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public UserSummaryDto? DeletedBy { get; set; }

        /// <summary>Human-readable "where this file lives" for the file-info panel — e.g.
        /// "Property Renovation" (project file) or "Property Renovation → Install cabinets"
        /// (task file) — computed server-side so the frontend never needs its own copy of this
        /// resolution logic.</summary>
        public required string Location { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

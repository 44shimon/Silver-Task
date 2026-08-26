namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// A file attached to a Project, a Task (a subtask is still a normal Task row, per Phase 30 —
    /// no separate SubtaskId), or a Comment. Phase 33 generalizes what was previously
    /// Task-only (TaskAttachment) into this shared entity — exactly one of ProjectId/TaskId/
    /// CommentId is set (enforced by a database check constraint, not just application code — see
    /// AttachmentConfiguration), a clean relational design rather than a generic
    /// EntityType/EntityId polymorphic pair.
    ///
    /// Local-disk storage, deliberately (unchanged from the original Task-only design) — no
    /// MinIO/S3 exists anywhere in this codebase to reuse, and introducing one now would be a
    /// stack change out of scope for this phase. Files live outside wwwroot under GUID-based
    /// names on disk (StoragePath); the original, user-supplied FileName is kept only as display
    /// metadata and is never used to build a filesystem path.
    /// </summary>
    public class Attachment
    {
        public Guid Id { get; set; }

        public Guid? ProjectId { get; set; }

        public Guid? TaskId { get; set; }

        public Guid? CommentId { get; set; }

        /// <summary>Editable via Rename — display name only, never the actual storage path.</summary>
        public required string FileName { get; set; }

        public long FileSize { get; set; }

        public required string MimeType { get; set; }

        /// <summary>GUID-based, server-generated relative path under the configured storage root —
        /// immutable after upload (Rename only ever changes FileName). Never derived from the
        /// client-supplied original filename.</summary>
        public required string StoragePath { get; set; }

        /// <summary>SHA-256 hex digest, best-effort (used only to help identify identical files —
        /// never a mandatory duplicate-prevention gate, per spec).</summary>
        public string? FileHash { get; set; }

        public Guid UploadedByUserId { get; set; }

        /// <summary>Soft delete — mirrors the exact pattern already established for User
        /// (IsDeleted/DeletedAt/DeletedByUserId, Phase 26). The physical file on disk is left in
        /// place when soft-deleted (recoverable via Restore); nothing here purges it
        /// automatically — see AttachmentService's own doc comment for the disclosed scope
        /// decision not to add a retention/purge job in this phase.</summary>
        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public Guid? DeletedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Project? Project { get; set; }

        public TaskItem? Task { get; set; }

        public TaskComment? Comment { get; set; }

        public User? UploadedBy { get; set; }

        public User? DeletedByUser { get; set; }
    }
}

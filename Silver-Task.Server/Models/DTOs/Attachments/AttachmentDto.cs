using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class AttachmentDto
    {
        public Guid Id { get; set; }

        public Guid? ProjectId { get; set; }

        public Guid? TaskId { get; set; }

        public Guid? CommentId { get; set; }

        public required string FileName { get; set; }

        public long FileSize { get; set; }

        public required string MimeType { get; set; }

        public string? FileHash { get; set; }

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

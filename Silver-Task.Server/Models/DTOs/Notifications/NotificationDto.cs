using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Notifications
{
    public class NotificationDto
    {
        public Guid Id { get; set; }

        public required string Type { get; set; }

        public required string Title { get; set; }

        public required string Message { get; set; }

        public required string Priority { get; set; }

        /// <summary>Null for system/background-sweep-originated notifications with no human
        /// actor (see Notification.ActorUserId's own doc comment).</summary>
        public UserSummaryDto? Actor { get; set; }

        /// <summary>Present only when the source task still exists — see
        /// NotificationConfiguration's SetNull-on-delete. Null means "open task" isn't available
        /// for this notification anymore, not that it never had one.</summary>
        public Guid? TaskId { get; set; }

        public Guid? ProjectId { get; set; }

        public Guid? CommentId { get; set; }

        public Guid? FileId { get; set; }

        public string? ActionUrl { get; set; }

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

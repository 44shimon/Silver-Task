using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Comments
{
    public class CommentDto
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public required UserSummaryDto User { get; set; }

        public required string Text { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

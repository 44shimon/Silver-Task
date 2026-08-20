namespace Silver_Task.Server.Models.Entities
{
    public class TaskComment
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public Guid UserId { get; set; }

        public required string Text { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public TaskItem? Task { get; set; }

        public User? User { get; set; }
    }
}

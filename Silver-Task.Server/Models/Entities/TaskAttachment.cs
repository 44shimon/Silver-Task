namespace Silver_Task.Server.Models.Entities
{
    public class TaskAttachment
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public required string FileName { get; set; }

        public long FileSize { get; set; }

        public required string MimeType { get; set; }

        public required string StoragePath { get; set; }

        public Guid UploadedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public TaskItem? Task { get; set; }

        public User? UploadedBy { get; set; }
    }
}

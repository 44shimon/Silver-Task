namespace Silver_Task.Server.Models.Entities
{
    public class Project
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid OwnerId { get; set; }

        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? Owner { get; set; }

        public ICollection<ProjectMember> Members { get; set; } = [];

        public ICollection<TaskItem> Tasks { get; set; } = [];

        public ICollection<CustomField> CustomFields { get; set; } = [];

        public ICollection<Attachment> Attachments { get; set; } = [];

        public ICollection<Folder> Folders { get; set; } = [];
    }
}

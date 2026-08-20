namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// An immutable audit log entry for a task, e.g. "Shimon changed Status from
    /// Not Started to In Progress". UserId is nullable so history survives user deletion.
    /// </summary>
    public class TaskActivity
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public Guid? UserId { get; set; }

        public required string Action { get; set; }

        public string? FieldName { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public DateTime CreatedAt { get; set; }

        public TaskItem? Task { get; set; }

        public User? User { get; set; }
    }
}

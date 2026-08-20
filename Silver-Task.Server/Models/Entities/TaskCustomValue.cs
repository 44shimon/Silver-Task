namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// The value of one custom field on one task (EAV pattern). Stored as text and
    /// parsed/validated according to CustomField.FieldType at the application layer;
    /// MultiSelect values are stored as a JSON array of CustomFieldOption ids.
    /// </summary>
    public class TaskCustomValue
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public Guid CustomFieldId { get; set; }

        public string? Value { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public TaskItem? Task { get; set; }

        public CustomField? CustomField { get; set; }
    }
}

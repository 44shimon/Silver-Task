namespace Silver_Task.Server.Models.Entities
{
    public class TaskTemplateCustomValue
    {
        public Guid Id { get; set; }

        public Guid TaskTemplateId { get; set; }

        public Guid CustomFieldId { get; set; }

        public string? Value { get; set; }

        public TaskTemplate? TaskTemplate { get; set; }

        public CustomField? CustomField { get; set; }
    }
}

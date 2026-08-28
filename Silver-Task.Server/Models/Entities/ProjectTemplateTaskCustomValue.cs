namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A stored default custom field value for a template task — re-validated against
    /// the real CustomField/target project through the exact same
    /// TaskService.SetCustomValueAsync path every other custom value write already goes through
    /// (see ITemplateInstantiationService), never a parallel validation copy.</summary>
    public class ProjectTemplateTaskCustomValue
    {
        public Guid Id { get; set; }

        public Guid ProjectTemplateTaskId { get; set; }

        public Guid CustomFieldId { get; set; }

        public string? Value { get; set; }

        public ProjectTemplateTask? ProjectTemplateTask { get; set; }

        public CustomField? CustomField { get; set; }
    }
}

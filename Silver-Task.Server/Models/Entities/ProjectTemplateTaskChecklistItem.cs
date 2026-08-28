namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A checklist item definition on a template task — replayed into real
    /// TaskChecklistItem rows on the instantiated task (see ITemplateInstantiationService).</summary>
    public class ProjectTemplateTaskChecklistItem
    {
        public Guid Id { get; set; }

        public Guid ProjectTemplateTaskId { get; set; }

        public required string Text { get; set; }

        public double SortOrder { get; set; }

        public ProjectTemplateTask? ProjectTemplateTask { get; set; }
    }
}

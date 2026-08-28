namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Reuses the exact same global Tag vocabulary every other tagged entity in this app
    /// does (TaskTag/FileTag) — never a template-specific tag concept.</summary>
    public class ProjectTemplateTaskTag
    {
        public Guid Id { get; set; }

        public Guid ProjectTemplateTaskId { get; set; }

        public Guid TagId { get; set; }

        public ProjectTemplateTask? ProjectTemplateTask { get; set; }

        public Tag? Tag { get; set; }
    }
}

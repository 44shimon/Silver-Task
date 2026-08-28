namespace Silver_Task.Server.Models.Entities
{
    /// <summary>An edge in a ProjectTemplate's own dependency graph — validated at template save
    /// time with the exact same cycle-detection algorithm TaskDependencyService uses for real
    /// tasks (see ITemplateService). ProjectTemplateId is denormalized here (also reachable via
    /// TemplateTask.ProjectTemplateId) purely so cycle-detection and "load this template's whole
    /// graph" queries don't need a join through ProjectTemplateTask.</summary>
    public class ProjectTemplateTaskDependency
    {
        public Guid Id { get; set; }

        public Guid ProjectTemplateId { get; set; }

        public Guid TemplateTaskId { get; set; }

        public Guid DependsOnTemplateTaskId { get; set; }

        public required string DependencyType { get; set; }

        public DateTime CreatedAt { get; set; }

        public ProjectTemplate? ProjectTemplate { get; set; }

        public ProjectTemplateTask? TemplateTask { get; set; }

        public ProjectTemplateTask? DependsOnTemplateTask { get; set; }
    }
}

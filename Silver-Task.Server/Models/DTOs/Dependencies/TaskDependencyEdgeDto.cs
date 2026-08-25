namespace Silver_Task.Server.Models.DTOs.Dependencies
{
    /// <summary>The bare graph edge (no task details — callers like the Gantt/Timeline views
    /// already have the full Task objects for every row) backing dependency-line rendering
    /// across a whole project in one request, instead of one request per visible bar.</summary>
    public class TaskDependencyEdgeDto
    {
        public Guid TaskId { get; set; }

        public Guid DependsOnTaskId { get; set; }
    }
}

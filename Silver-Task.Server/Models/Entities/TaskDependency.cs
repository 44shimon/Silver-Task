namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A prerequisite relationship between two tasks in the same project. TaskId is the
    /// task that is waiting; DependsOnTaskId is the prerequisite. For the only currently-
    /// supported DependencyType (FinishToStart), TaskId can't meaningfully proceed until
    /// DependsOnTaskId reaches TaskItemStatus.Complete — see TaskDependencyService for where that
    /// blocked-state is actually calculated (deliberately not stored on TaskItem itself).</summary>
    public class TaskDependency
    {
        public Guid Id { get; set; }

        /// <summary>The dependent (waiting) task.</summary>
        public Guid TaskId { get; set; }

        /// <summary>The prerequisite task.</summary>
        public Guid DependsOnTaskId { get; set; }

        public required string DependencyType { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid CreatedByUserId { get; set; }

        public TaskItem? Task { get; set; }

        public TaskItem? DependsOnTask { get; set; }

        public User? CreatedByUser { get; set; }
    }
}

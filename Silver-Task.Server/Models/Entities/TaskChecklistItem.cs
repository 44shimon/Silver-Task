namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 40 — a lightweight, per-task checkable item, distinct from Subtasks (a
    /// Subtask is a full TaskItem with its own status/assignee/dates; a checklist item is just
    /// text + a checkbox, no independent lifecycle of its own). New concept in this app — nothing
    /// existing to reuse (see the Phase 40 final report's own research note). Populated either
    /// directly by a user, or replayed from a ProjectTemplateTaskChecklistItem/
    /// TaskTemplateChecklistItem at template-instantiation time.</summary>
    public class TaskChecklistItem
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public required string Text { get; set; }

        public bool IsChecked { get; set; }

        public double SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public TaskItem? Task { get; set; }
    }
}

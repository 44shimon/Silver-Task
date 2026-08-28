using System.ComponentModel.DataAnnotations.Schema;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// A single row in the project spreadsheet. Named TaskItem (not Task) to avoid
    /// colliding with System.Threading.Tasks.Task, which is in scope everywhere via
    /// ImplicitUsings.
    /// </summary>
    public class TaskItem
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public Guid? AssignedToUserId { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        /// <summary>Fractional index used to persist manual row ordering — scoped to siblings
        /// (same ParentTaskId, including the "top-level" group where ParentTaskId is null) as of
        /// Phase 30, not the whole project. Every pre-existing task has ParentTaskId=null, so this
        /// is behavior-preserving: project-level ordering is unchanged, subtasks simply get their
        /// own independent sequence under their parent.</summary>
        public double SortOrder { get; set; }

        /// <summary>Null = top-level task. Self-referencing — a subtask is still a normal
        /// TaskItem row, not a separate entity (see TaskService's hierarchy methods for the
        /// circular-parent/same-project/depth validation this requires).</summary>
        public Guid? ParentTaskId { get; set; }

        /// <summary>Set on every occurrence of a recurring series, including the first (the task
        /// the user originally attached recurrence to) — null for an ordinary, non-recurring task.
        /// See RecurringTask; this is deliberately a plain nullable FK on the existing Task row,
        /// not a second task system (Phase 31).</summary>
        public Guid? RecurringTaskId { get; set; }

        /// <summary>The calendar date this occurrence represents, per the recurrence rule — not
        /// necessarily equal to StartDate/DueDate (a user can freely reschedule a single occurrence
        /// without changing its place in the series). Combined with RecurringTaskId, this is
        /// unique — the database-level duplicate-generation guard (see TaskItemConfiguration).</summary>
        public DateOnly? RecurrenceOccurrenceDate { get; set; }

        /// <summary>1-based position in the series (1 = the template/first occurrence) — display
        /// only, never used for scheduling logic.</summary>
        public int? OccurrenceNumber { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        /// <summary>Set the first time the overdue-check sweep (Phase 35) dispatches a
        /// TaskOverdueEvent for this task — prevents re-firing that trigger every sweep interval
        /// for the same overdue transition. Cleared whenever DueDate changes or the task leaves
        /// the overdue state (completed/reopened with a future date), so a task can become
        /// overdue, get fixed, and later become overdue again without the trigger going stale
        /// forever. See AutomationOverdueCheckBackgroundService's own doc comment.</summary>
        public DateTime? OverdueAutomationProcessedAt { get; set; }

        public Project? Project { get; set; }

        public User? AssignedTo { get; set; }

        public TaskItem? ParentTask { get; set; }

        public RecurringTask? RecurringTask { get; set; }

        public ICollection<TaskItem> Subtasks { get; set; } = [];

        public ICollection<TaskComment> Comments { get; set; } = [];

        public ICollection<TaskActivity> Activities { get; set; } = [];

        public ICollection<Attachment> Attachments { get; set; } = [];

        public ICollection<TaskCustomValue> CustomValues { get; set; } = [];

        public ICollection<TaskTag> TaskTags { get; set; } = [];

        /// <summary>Phase 40 — see TaskChecklistItem's own doc comment.</summary>
        public ICollection<TaskChecklistItem> ChecklistItems { get; set; } = [];

        /// <summary>Not persisted — populated in bulk (one aggregate query per task list, never
        /// per-task) by TaskService whenever it loads tasks, so TaskDto can show dependency
        /// counts on every list/table/kanban/etc. without an N+1 query per task. See
        /// TaskService.AttachDependencySummaryAsync.</summary>
        [NotMapped]
        public int DependsOnCount { get; set; }

        /// <summary>Of DependsOnCount, how many prerequisites are not yet Complete — this is the
        /// "blocked" count. Deliberately never written back to Status (see TaskDependencyService/
        /// README "Blocked state" reasoning) — purely a computed display value.</summary>
        [NotMapped]
        public int BlockedByCount { get; set; }

        /// <summary>How many other tasks depend on this one (the "Blocking" count).</summary>
        [NotMapped]
        public int DependentCount { get; set; }

        /// <summary>Direct children count — not the full recursive subtree. Same bulk-aggregate
        /// population pattern as the dependency counts above (see
        /// TaskService.AttachSubtaskSummaryAsync).</summary>
        [NotMapped]
        public int SubtaskCount { get; set; }

        /// <summary>Of SubtaskCount, how many direct children have Status == Complete — backs the
        /// "N of M complete" / percentage progress display. Never fed back into the parent's own
        /// Status (same "computed display value, not stored state" rule as BlockedByCount).</summary>
        [NotMapped]
        public int CompletedSubtaskCount { get; set; }

        /// <summary>Only populated when ParentTaskId is set — lets a cross-project list like My
        /// Tasks show "Parent: X" without the caller needing that other project's full task list
        /// already loaded the way ProjectPage does.</summary>
        [NotMapped]
        public string? ParentTaskTitle { get; set; }
    }
}

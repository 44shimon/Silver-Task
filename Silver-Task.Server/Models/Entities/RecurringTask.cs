using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// The recurrence *rule* — deliberately separate from the Task rows it generates (see
    /// TaskItem.RecurringTaskId). Holds its own copy of Title/Description/Priority/AssignedToUserId
    /// rather than always reading them live off TemplateTask, so "Edit Recurrence" (This-and-future/
    /// Entire-series) can change what *future* occurrences look like without touching the template
    /// task's own current state, and so the template task can still be edited/completed/deleted
    /// independently without silently altering the schedule's own fields.
    /// </summary>
    public class RecurringTask
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        /// <summary>Set when the task this recurrence was attached to was itself a subtask —
        /// every generated occurrence is created as a subtask under the same parent. Null means
        /// occurrences are created at the top level.</summary>
        public Guid? ParentTaskId { get; set; }

        /// <summary>The first occurrence (the task the user attached recurrence to) — also the
        /// live source for copying each new occurrence's subtask structure (Phase 30 integration).
        /// SetNull on delete: if this task is later removed, the series keeps generating, it just
        /// stops copying subtasks (a disclosed limitation, not a crash).</summary>
        public Guid? TemplateTaskId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public Guid? AssignedToUserId { get; set; }

        public RecurrenceFrequency Frequency { get; set; }

        /// <summary>"Every N [days/weeks/months/years]" — always &gt;= 1.</summary>
        public int Interval { get; set; } = 1;

        /// <summary>Weekly only. None falls back to the StartDate's own weekday at generation time.</summary>
        public RecurrenceDayOfWeek DaysOfWeek { get; set; }

        /// <summary>Monthly/Yearly. Out-of-range-for-the-month values (e.g. 31 in February) are
        /// clamped to that month's last day at generation time — never an invalid date.</summary>
        public int? DayOfMonth { get; set; }

        /// <summary>Yearly only.</summary>
        public int? MonthOfYear { get; set; }

        public DateOnly StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public int? MaxOccurrences { get; set; }

        /// <summary>Count of occurrence dates already processed (materialized as a task *or*
        /// intentionally skipped as too-old-to-backfill) — the running counter MaxOccurrences is
        /// checked against, not just "how many Task rows exist".</summary>
        public int OccurrencesGenerated { get; set; }

        /// <summary>Null means the series is exhausted (hit EndDate/MaxOccurrences) or stopped —
        /// either way, the background generator skips it.</summary>
        public DateOnly? NextOccurrenceDate { get; set; }

        /// <summary>False = stopped. Existing generated tasks are never touched by stopping;
        /// only future generation halts.</summary>
        public bool IsActive { get; set; } = true;

        public Guid CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Project? Project { get; set; }

        public TaskItem? ParentTask { get; set; }

        public TaskItem? TemplateTask { get; set; }

        public User? AssignedToUser { get; set; }

        public User? CreatedByUser { get; set; }

        public ICollection<TaskItem> GeneratedTasks { get; set; } = [];

        public ICollection<RecurringTaskException> Exceptions { get; set; } = [];
    }
}

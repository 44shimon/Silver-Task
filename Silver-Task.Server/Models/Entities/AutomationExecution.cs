using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>One "run" of an automation against one triggering event (Phase 35) — created only
    /// when an automation's conditions actually matched and its actions were attempted (a
    /// trigger firing that simply doesn't match an automation's conditions is not logged here; it
    /// would make this table noise-dominated for broad triggers like TaskUpdated). ResultSummary
    /// is a short human-readable line ("Assigned to Alice; tag 'Urgent' added") rather than a
    /// separate AutomationExecutionAction child table — the spec's own Runs mockups only ever
    /// show one summary line per run, so a second entity wasn't "actually required" (per the
    /// spec's own database-section instruction).</summary>
    public class AutomationExecution
    {
        public Guid Id { get; set; }

        public Guid AutomationId { get; set; }

        /// <summary>The dispatched event's own id — lets a redelivered/duplicate event be
        /// recognized and skipped rather than re-executed (see AutomationService's own doc
        /// comment on duplicate-delivery protection).</summary>
        public Guid TriggerEventId { get; set; }

        /// <summary>How many automation-caused "hops" led to this run — 0 for a genuine
        /// user-initiated event, N+1 for an event raised as a side effect of a previous
        /// automation's own action. Capped by AutomationService's MaxChainDepth (see its own doc
        /// comment on loop protection).</summary>
        public int ChainDepth { get; set; }

        /// <summary>The task/file/project/comment id the trigger fired for, for display/
        /// debugging — not a foreign key, since it can point at any one of several tables
        /// depending on TriggerType.</summary>
        public Guid? EntityId { get; set; }

        public AutomationExecutionStatus Status { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int? DurationMs { get; set; }

        public string? ErrorMessage { get; set; }

        public string? ResultSummary { get; set; }

        /// <summary>Set only when this row is itself a retry of an earlier failed execution —
        /// see AutomationService.RetryAsync's own doc comment for the retry-count cap.</summary>
        public Guid? RetryOfExecutionId { get; set; }

        public Automation? Automation { get; set; }
    }
}

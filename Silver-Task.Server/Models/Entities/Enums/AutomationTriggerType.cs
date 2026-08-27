namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>The fixed set of events an Automation can subscribe to (Phase 35) — a closed
    /// enum, not an open string-constant list like NotificationTypes, because unlike
    /// notifications (where new types are added freely as features grow) a trigger implies real
    /// engine support (an event actually gets dispatched for it somewhere in the codebase); adding
    /// one always requires a corresponding code change anyway, so there's no benefit to keeping it
    /// open-ended.</summary>
    public enum AutomationTriggerType
    {
        TaskCreated,
        TaskUpdated,
        TaskCompleted,
        TaskReopened,
        TaskAssigned,
        TaskOverdue,
        CommentAdded,
        FileUploaded,
        FileTagged,
        SubtaskCompleted,
        ProjectCreated,

        // Phase 39 — dependency/workflow events. All resolve "the task in context" as the
        // dependent task (the one whose readiness/blocked state changed, or whose dependency list
        // was edited) via LoadTaskContextAsync's default case — see AutomationService.BuildContextAsync.
        /// <summary>Fires once, the moment a task's LAST unsatisfied start-blocker clears (see
        /// TaskDependencyService's satisfaction rules) — not on every prerequisite completion if
        /// others still block it. Distinct from DependencyCompleted, below, which fires per
        /// prerequisite regardless of whether the task is now fully ready.</summary>
        TaskBecameReady,

        /// <summary>Fires the moment a newly-added dependency immediately blocks a task that
        /// wasn't blocked before.</summary>
        TaskBecameBlocked,

        DependencyAdded,
        DependencyRemoved,

        /// <summary>Fires once per prerequisite that reaches Complete, for every task that
        /// depends on it — regardless of whether that dependent still has other unsatisfied
        /// prerequisites (see TaskBecameReady for the "fully unblocked" trigger).</summary>
        DependencyCompleted,

        /// <summary>Fires when an authorized user overrides a dependency block to start or
        /// complete a task anyway — see TaskService.UpdateAsync's own doc comment.</summary>
        DependencyOverridden
    }
}

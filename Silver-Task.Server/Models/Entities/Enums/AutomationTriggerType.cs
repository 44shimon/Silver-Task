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
        ProjectCreated
    }
}

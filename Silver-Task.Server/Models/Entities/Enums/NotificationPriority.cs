namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>A closed, fixed 3-level severity that applies uniformly across every
    /// notification type (unlike Type itself, which is open-ended free text) — genuinely a
    /// small, engine-relevant enum the same way TaskPriority/AutomationExecutionStatus are,
    /// not something a new feature adds values to. See Common.NotificationPriorities for the
    /// per-type default mapping.</summary>
    public enum NotificationPriority
    {
        Normal,
        Important,
        Urgent
    }
}

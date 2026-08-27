namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>How an action resolves "which user" — reused by AssignTask (target), CreateTask
    /// (assignee), and SendNotification (recipient) rather than three separate small enums.
    /// ProjectManager resolves to the project's owner (who is always that project's effective
    /// Manager and can't be changed, see ProjectService's own doc comment), avoiding a hardcoded
    /// user id for the very common "assign/notify the manager" pattern the spec's examples lean
    /// on heavily. TaskAssignee resolves to the triggering task's own current assignee (only
    /// meaningful for task-scoped triggers) — None means "leave unassigned" / "don't notify
    /// anyone" and is only valid for CreateTask/SendNotification, never AssignTask.</summary>
    public enum AutomationUserSelector
    {
        None,
        TaskAssignee,
        ProjectManager,
        SpecificUser
    }
}

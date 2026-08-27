namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>The complete, closed set of actions an automation may perform (Phase 35) —
    /// deliberately excludes anything destructive (no DeleteProject/DeleteTask/DeleteUser/etc.,
    /// see the spec's own "destructive actions" exclusion list) and anything that would move a
    /// task across projects (see AutomationAction's own doc comment on why "assign project" was
    /// left out). Every action executes through the same service methods and permission checks a
    /// normal user request would use — see AutomationService's own doc comment.</summary>
    public enum AutomationActionType
    {
        AssignTask,
        ChangeStatus,
        ChangePriority,
        AddLabel,
        RemoveLabel,
        SetDueDate,
        SetStartDate,
        AddComment,
        CreateTask,
        SendNotification,
        AddFileTag
    }
}

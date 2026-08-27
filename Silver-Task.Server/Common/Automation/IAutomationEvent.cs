using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Common.Automation
{
    /// <summary>Marker interface for the small set of domain events an Automation can subscribe
    /// to (Phase 35) — this codebase has no existing event/hook system (every "reactive" behavior
    /// prior to this phase, e.g. notifications and activity logging, is inlined directly in the
    /// mutating service method), so this is a new, deliberately lightweight one: services call
    /// IAutomationDispatcher.DispatchAsync(...) right after a change is committed; nothing else in
    /// the app subscribes to these events or needs to know they exist.</summary>
    public interface IAutomationEvent
    {
        AutomationTriggerType TriggerType { get; }

        /// <summary>Null only for events with no natural project (there are none currently, but
        /// kept nullable for future trigger types) — every existing event resolves to exactly one
        /// project, which is also how AutomationService scopes "which automations apply."</summary>
        Guid? ProjectId { get; }
    }

    public record TaskCreatedEvent(Guid TaskId, Guid ProjectId, Guid CreatedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskCreated;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    /// <summary>The broad "something important changed" trigger — fired whenever
    /// TaskService.UpdateAsync's own diff (Title/Description/Status/Priority/Assignee/StartDate/
    /// DueDate) finds at least one real change. Fires alongside (not instead of) the more specific
    /// events below when applicable, since each automation subscribes to exactly one
    /// AutomationTriggerType and simply ignores events of any other type.</summary>
    public record TaskUpdatedEvent(Guid TaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskUpdated;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record TaskCompletedEvent(Guid TaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskCompleted;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record TaskReopenedEvent(Guid TaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskReopened;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record TaskAssignedEvent(
        Guid TaskId, Guid ProjectId, Guid? PreviousAssigneeId, Guid? NewAssigneeId, Guid ChangedByUserId, DateTime Timestamp)
        : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskAssigned;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    /// <summary>Raised only by AutomationOverdueCheckBackgroundService's sweep, never by a normal
    /// request — see TaskItem.OverdueAutomationProcessedAt for the once-per-transition guard.</summary>
    public record TaskOverdueEvent(Guid TaskId, Guid ProjectId, DateOnly DueDate, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskOverdue;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record CommentAddedEvent(Guid CommentId, Guid TaskId, Guid ProjectId, Guid AuthorUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.CommentAdded;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record FileUploadedEvent(Guid FileId, Guid ProjectId, Guid? TaskId, Guid UploadedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.FileUploaded;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record FileTaggedEvent(Guid FileId, Guid ProjectId, Guid? TaskId, string TagName, Guid ActorUserId, DateTime Timestamp)
        : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.FileTagged;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    /// <summary>Fires whenever a task with a non-null ParentTaskId completes — the trigger is
    /// "a subtask completed", not "all subtasks completed"; the latter is expressed as a
    /// condition (AllSiblingSubtasksComplete) evaluated against this same trigger, per the spec's
    /// own "make this a condition/action combination rather than hard-coding special behavior"
    /// instruction. See AutomationService's own doc comment on why ParentTaskId becomes "the
    /// task in context" for condition/action purposes when this trigger fires.</summary>
    public record SubtaskCompletedEvent(Guid SubtaskId, Guid ParentTaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp)
        : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.SubtaskCompleted;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record ProjectCreatedEvent(Guid ProjectId, Guid CreatedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.ProjectCreated;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    // ---------- Phase 39: dependency / workflow events ----------

    public record TaskBecameReadyEvent(Guid TaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskBecameReady;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record TaskBecameBlockedEvent(Guid TaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp) : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.TaskBecameBlocked;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record DependencyAddedEvent(Guid TaskId, Guid DependsOnTaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp)
        : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.DependencyAdded;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record DependencyRemovedEvent(Guid TaskId, Guid DependsOnTaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp)
        : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.DependencyRemoved;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    /// <summary>DependentTaskId (not the completed prerequisite) is "the task in context" — this
    /// fires once per dependent, from the perspective of "one of your prerequisites just
    /// completed", matching TaskBecameReady's own framing.</summary>
    public record DependencyCompletedEvent(
        Guid DependentTaskId, Guid CompletedPrerequisiteTaskId, Guid ProjectId, Guid ChangedByUserId, DateTime Timestamp)
        : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.DependencyCompleted;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }

    public record DependencyOverriddenEvent(Guid TaskId, Guid ProjectId, Guid OverriddenByUserId, string Reason, DateTime Timestamp)
        : IAutomationEvent
    {
        public AutomationTriggerType TriggerType => AutomationTriggerType.DependencyOverridden;
        Guid? IAutomationEvent.ProjectId => ProjectId;
    }
}

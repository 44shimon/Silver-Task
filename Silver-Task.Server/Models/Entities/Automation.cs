using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// A user-defined "when X happens, if Y, then Z" rule (Phase 35) — ProjectId null means a
    /// global automation (Administrator-only, applies across every project); a project-scoped
    /// automation applies only to events from that project. Conditions are always AND-ed together
    /// (see AutomationCondition's own doc comment — OR/nested groups are explicitly out of scope
    /// this phase per spec). Execution always acts with CreatedByUserId's own live permissions,
    /// re-checked at run time — see AutomationService's own doc comment for why this is the
    /// security model rather than some ambient "system" identity that could bypass normal checks.
    /// </summary>
    public class Automation
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid? ProjectId { get; set; }

        public AutomationTriggerType TriggerType { get; set; }

        public Guid CreatedByUserId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        /// <summary>Denormalized onto the automation itself (rather than always computed from
        /// AutomationExecutions) purely so the list view can show "Last Run" without an extra
        /// aggregate query per row.</summary>
        public DateTime? LastRunAt { get; set; }

        public int RunCount { get; set; }

        public string? LastError { get; set; }

        /// <summary>Soft delete, same shape as Folder/Attachment/User — required so "Delete
        /// automation" can never cascade away AutomationExecution history (see the spec's own
        /// "prefer retaining execution history" instruction); a deleted automation simply stops
        /// matching events (IsActive is forced false alongside it) and is hidden from normal
        /// lists, while its past runs remain fully intact and attributable.</summary>
        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public Guid? DeletedByUserId { get; set; }

        public Project? Project { get; set; }

        public User? CreatedBy { get; set; }

        public User? DeletedByUser { get; set; }

        public ICollection<AutomationCondition> Conditions { get; set; } = [];

        public ICollection<AutomationAction> Actions { get; set; } = [];
    }
}

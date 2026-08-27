using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A single "Field Operator Value" clause. Every condition on an automation is
    /// AND-ed together — matching the spec's explicit "at minimum, all conditions must match; do
    /// not over-engineer Phase 35" guidance rather than building OR/nested condition groups.
    /// Field is a plain string key (e.g. "Task.Priority", "File.Category") rather than an enum —
    /// the valid set differs per TriggerType and spans several unrelated entity "namespaces"
    /// (Task/File/Project/User), the same open-string-key shape already used for
    /// NotificationTypes/CustomFieldType-adjacent concerns in this codebase — see
    /// Common/AutomationFields.cs for the recognized keys and AutomationValidator for where
    /// they're checked against TriggerType.</summary>
    public class AutomationCondition
    {
        public Guid Id { get; set; }

        public Guid AutomationId { get; set; }

        public required string Field { get; set; }

        public AutomationConditionOperator Operator { get; set; }

        /// <summary>Raw comparison value as text (a Guid for user/project references, an enum
        /// name for Status/Priority, an ISO date for date fields) — null/empty is valid for
        /// IsEmpty/IsNotEmpty, which ignore it entirely.</summary>
        public string? Value { get; set; }

        public int SortOrder { get; set; }

        public Automation? Automation { get; set; }
    }
}

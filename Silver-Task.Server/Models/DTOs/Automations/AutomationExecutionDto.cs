using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Automations
{
    public class AutomationExecutionDto
    {
        public Guid Id { get; set; }

        public Guid AutomationId { get; set; }

        public required string AutomationName { get; set; }

        public AutomationTriggerType TriggerType { get; set; }

        public Guid? EntityId { get; set; }

        public AutomationExecutionStatus Status { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int? DurationMs { get; set; }

        public string? ErrorMessage { get; set; }

        public string? ResultSummary { get; set; }

        public Guid? RetryOfExecutionId { get; set; }
    }

    public class AutomationExecutionListDto
    {
        public required List<AutomationExecutionDto> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    /// <summary>Result of "Test Automation" (Phase 35) — a pure dry run: conditions are evaluated
    /// against a real sample entity, but no action is ever executed and nothing is written to the
    /// database, per the spec's "without unexpectedly modifying production data" requirement.</summary>
    public class AutomationTestResultDto
    {
        public bool ConditionsMatched { get; set; }

        public required List<string> ActionPreviews { get; set; }

        public string? Explanation { get; set; }
    }
}

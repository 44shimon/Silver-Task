using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Automations
{
    public class AutomationDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid? ProjectId { get; set; }

        public AutomationTriggerType TriggerType { get; set; }

        public bool IsActive { get; set; }

        public required List<AutomationConditionDto> Conditions { get; set; }

        public required List<AutomationActionDto> Actions { get; set; }

        public required UserSummaryDto CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? LastRunAt { get; set; }

        public int RunCount { get; set; }

        public string? LastError { get; set; }
    }

    public class AutomationConditionDto
    {
        public Guid Id { get; set; }

        public required string Field { get; set; }

        public AutomationConditionOperator Operator { get; set; }

        public string? Value { get; set; }
    }

    public class AutomationActionDto
    {
        public Guid Id { get; set; }

        public AutomationActionType ActionType { get; set; }

        public required System.Text.Json.JsonElement Parameters { get; set; }
    }
}

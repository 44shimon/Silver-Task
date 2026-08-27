using System.Text.Json;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Automations
{
    public static class AutomationMappingExtensions
    {
        public static AutomationDto ToDto(this Automation automation) => new()
        {
            Id = automation.Id,
            Name = automation.Name,
            Description = automation.Description,
            ProjectId = automation.ProjectId,
            TriggerType = automation.TriggerType,
            IsActive = automation.IsActive,
            Conditions = [.. automation.Conditions
                .OrderBy(c => c.SortOrder)
                .Select(c => new AutomationConditionDto { Id = c.Id, Field = c.Field, Operator = c.Operator, Value = c.Value })],
            Actions = [.. automation.Actions
                .OrderBy(a => a.SortOrder)
                .Select(a => new AutomationActionDto { Id = a.Id, ActionType = a.ActionType, Parameters = JsonDocument.Parse(a.ParametersJson).RootElement })],
            CreatedBy = automation.CreatedBy!.ToSummaryDto(),
            CreatedAt = automation.CreatedAt,
            UpdatedAt = automation.UpdatedAt,
            LastRunAt = automation.LastRunAt,
            RunCount = automation.RunCount,
            LastError = automation.LastError
        };

        public static AutomationExecutionDto ToDto(this AutomationExecution execution) => new()
        {
            Id = execution.Id,
            AutomationId = execution.AutomationId,
            AutomationName = execution.Automation?.Name ?? "(deleted automation)",
            TriggerType = execution.Automation?.TriggerType ?? default,
            EntityId = execution.EntityId,
            Status = execution.Status,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            DurationMs = execution.DurationMs,
            ErrorMessage = execution.ErrorMessage,
            ResultSummary = execution.ResultSummary,
            RetryOfExecutionId = execution.RetryOfExecutionId
        };
    }
}

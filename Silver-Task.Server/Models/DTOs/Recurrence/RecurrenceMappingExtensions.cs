using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Models.DTOs.Recurrence
{
    public static class RecurrenceMappingExtensions
    {
        public static RecurrenceRuleDto ToDto(this RecurringTask rule) => new()
        {
            Id = rule.Id,
            ProjectId = rule.ProjectId,
            ParentTaskId = rule.ParentTaskId,
            TemplateTaskId = rule.TemplateTaskId,
            TemplateTaskTitle = rule.TemplateTask?.Title,
            Title = rule.Title,
            Description = rule.Description,
            Priority = rule.Priority,
            AssignedTo = rule.AssignedToUser?.ToSummaryDto(),
            Frequency = rule.Frequency,
            Interval = rule.Interval,
            DaysOfWeek = RecurrenceCalculator.FromMask(rule.DaysOfWeek),
            DayOfMonth = rule.DayOfMonth,
            MonthOfYear = rule.MonthOfYear,
            StartDate = rule.StartDate,
            EndDate = rule.EndDate,
            MaxOccurrences = rule.MaxOccurrences,
            OccurrencesGenerated = rule.OccurrencesGenerated,
            NextOccurrenceDate = rule.NextOccurrenceDate,
            IsActive = rule.IsActive,
            ScheduleDescription = RecurrenceDescriptionBuilder.Describe(rule),
            CreatedByUserId = rule.CreatedByUserId,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }
}

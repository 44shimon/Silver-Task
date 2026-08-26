using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Recurrence
{
    public class RecurrenceRuleDto
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public Guid? ParentTaskId { get; set; }

        public Guid? TemplateTaskId { get; set; }

        public string? TemplateTaskTitle { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskPriority Priority { get; set; }

        public UserSummaryDto? AssignedTo { get; set; }

        public RecurrenceFrequency Frequency { get; set; }

        public int Interval { get; set; }

        public List<DayOfWeek> DaysOfWeek { get; set; } = [];

        public int? DayOfMonth { get; set; }

        public int? MonthOfYear { get; set; }

        public DateOnly StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public int? MaxOccurrences { get; set; }

        public int OccurrencesGenerated { get; set; }

        public DateOnly? NextOccurrenceDate { get; set; }

        public bool IsActive { get; set; }

        /// <summary>Server-computed "Every Monday" / "Every 2 weeks on Mon, Wed, until Dec 31,
        /// 2026" summary — the single source of truth for this text (also reused verbatim in
        /// activity-log entries), so the frontend never re-derives its own formatting.</summary>
        public required string ScheduleDescription { get; set; }

        public Guid CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

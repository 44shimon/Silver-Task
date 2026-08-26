using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Recurrence
{
    /// <summary>Attaches a recurrence rule to an existing task, which becomes the series' template
    /// (occurrence #1). ProjectId/ParentTaskId are always resolved server-side from that task —
    /// never trusted from the request body, matching CreateTaskRequest's own convention. Fields
    /// below are pre-filled by the frontend from the task's current values but are otherwise a
    /// fresh, independent copy the user can adjust before submitting (see RecurringTask's own doc
    /// comment for why the rule keeps its own copy rather than always reading the template live).</summary>
    public class CreateRecurrenceRequest
    {
        [Required, StringLength(500, MinimumLength = 1)]
        public required string Title { get; set; }

        [StringLength(10000)]
        public string? Description { get; set; }

        [Required]
        public TaskPriority Priority { get; set; }

        public Guid? AssignedToUserId { get; set; }

        [Required]
        public RecurrenceFrequency Frequency { get; set; }

        [Range(1, 365)]
        public int Interval { get; set; } = 1;

        /// <summary>Weekly only; required (non-empty) when Frequency is Weekly.</summary>
        public List<DayOfWeek>? DaysOfWeek { get; set; }

        [Range(1, 31)]
        public int? DayOfMonth { get; set; }

        [Range(1, 12)]
        public int? MonthOfYear { get; set; }

        [Required]
        public DateOnly StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        [Range(1, 1000)]
        public int? MaxOccurrences { get; set; }
    }
}

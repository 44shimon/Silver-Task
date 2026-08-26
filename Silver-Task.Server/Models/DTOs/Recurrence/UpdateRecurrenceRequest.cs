using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Recurrence
{
    /// <summary>Same rule fields as CreateRecurrenceRequest, plus the edit scope. EntireSeries
    /// treats "now" as the cutoff (occurrences at/after today that haven't been individually
    /// touched are regenerated under the new rule; anything in the past, or already edited away
    /// from its default status, is left alone). ThisAndFuture uses AnchorOccurrenceDate as that
    /// cutoff instead — the specific occurrence the user was viewing when they chose "this and
    /// future". See RecurringTaskService.UpdateAsync.</summary>
    public class UpdateRecurrenceRequest
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

        [Required]
        public RecurrenceEditScope Scope { get; set; }

        /// <summary>Required when Scope is ThisAndFuture; ignored for EntireSeries.</summary>
        public DateOnly? AnchorOccurrenceDate { get; set; }
    }
}

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Records that a specific occurrence date must never be (re)generated for a
    /// recurring series — created when a user deletes a single generated occurrence, so a later
    /// generation run doesn't silently recreate the very task they just removed. Deleting the
    /// *series* itself doesn't need this: RecurringTask.IsActive=false (Stop) or removing the
    /// RecurringTask row (Delete) already stops all future generation outright.</summary>
    public class RecurringTaskException
    {
        public Guid Id { get; set; }

        public Guid RecurringTaskId { get; set; }

        public DateOnly OccurrenceDate { get; set; }

        public required string ExceptionType { get; set; }

        public DateTime CreatedAt { get; set; }

        public RecurringTask? RecurringTask { get; set; }
    }
}

namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>How far an "Edit Recurrence" change should apply. "This occurrence only" is
    /// deliberately not a value here — editing a single generated task's own fields is already
    /// just the normal PUT /api/tasks/{id} edit path (it never touches the RecurringTask row at
    /// all), so it needs no dedicated scope or endpoint.</summary>
    public enum RecurrenceEditScope
    {
        ThisAndFuture,
        EntireSeries
    }
}

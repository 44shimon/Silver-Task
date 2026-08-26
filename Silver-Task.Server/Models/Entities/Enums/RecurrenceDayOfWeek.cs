namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>Bitmask of calendar weekdays used by Weekly recurrence. Unlike TaskItemStatus/
    /// CustomFieldType (which are persisted via HasConversion&lt;string&gt; specifically so new
    /// values never need a migration), this set is fixed forever — there will never be an eighth
    /// day of the week — so it's stored as its native integer value, which is also the only
    /// sensible storage shape for a [Flags] combination like "Monday and Wednesday".</summary>
    [Flags]
    public enum RecurrenceDayOfWeek
    {
        None = 0,
        Sunday = 1,
        Monday = 2,
        Tuesday = 4,
        Wednesday = 8,
        Thursday = 16,
        Friday = 32,
        Saturday = 64
    }
}

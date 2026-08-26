namespace Silver_Task.Server.Common
{
    /// <summary>Known RecurringTaskException.ExceptionType values, as plain strings (same
    /// extensibility rationale as NotificationTypes/DependencyTypes) — currently only one, raised
    /// when a user deletes a single generated occurrence so the generator never recreates it.</summary>
    public static class RecurrenceExceptionTypes
    {
        public const string Deleted = "Deleted";

        public static readonly IReadOnlyList<string> All = [Deleted];
    }
}

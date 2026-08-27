namespace Silver_Task.Server.Common
{
    /// <summary>Plain text, not a C# enum — same reasoning as NotificationTypes/CustomFieldType:
    /// a new dependency type is a new constant, not a migration. Phase 39 activates the three
    /// types this was originally stubbed for (only FinishToStart was interpreted before) — see
    /// TaskDependencyService's own doc comment for exactly how each type gates starting vs.
    /// completing a dependent task. "Started" (used by StartToStart/StartToFinish) means the
    /// task's Status is anything other than NotStarted or Cancelled — an objective, current-state
    /// fact this app already tracks, not an invented timestamp (this app has no reliable
    /// "started at" moment recorded anywhere — see Phase 38's own disclosed Cycle Time omission
    /// for the same reasoning applied to a different feature).</summary>
    public static class DependencyTypes
    {
        /// <summary>Default. The dependent cannot START until the prerequisite is Complete.</summary>
        public const string FinishToStart = "FinishToStart";

        /// <summary>The dependent cannot START until the prerequisite has STARTED.</summary>
        public const string StartToStart = "StartToStart";

        /// <summary>The dependent cannot be marked COMPLETE until the prerequisite is Complete.
        /// Does not gate starting.</summary>
        public const string FinishToFinish = "FinishToFinish";

        /// <summary>The dependent cannot be marked COMPLETE until the prerequisite has STARTED.
        /// Does not gate starting.</summary>
        public const string StartToFinish = "StartToFinish";

        public static readonly IReadOnlyList<string> All = [FinishToStart, StartToStart, FinishToFinish, StartToFinish];
    }
}

namespace Silver_Task.Server.Common
{
    /// <summary>Plain text, not a C# enum — same reasoning as NotificationTypes/CustomFieldType:
    /// a new dependency type (Start-to-Start, Finish-to-Finish, Start-to-Finish) is a new
    /// constant later, not a migration. Only FinishToStart is actually interpreted anywhere
    /// right now (see TaskDependencyService's blocked-state calculation) — the others are listed
    /// here only as the extension point the phase spec asks for.</summary>
    public static class DependencyTypes
    {
        public const string FinishToStart = "FinishToStart";

        public static readonly IReadOnlyList<string> All = [FinishToStart];
    }
}

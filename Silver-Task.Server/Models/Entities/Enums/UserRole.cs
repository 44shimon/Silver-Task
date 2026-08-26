namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>System-wide role. Persisted via HasConversion&lt;string&gt; (UserConfiguration),
    /// so adding Viewer here (Phase 32) needed no migration — only a new value of an existing
    /// string column. See Common/Permissions.cs for what each role actually grants; this enum is
    /// deliberately just the small, fixed label set, not itself a permission source.</summary>
    public enum UserRole
    {
        Administrator,
        Manager,
        Member,

        /// <summary>Read-only across every project the account can see (own-project-membership
        /// rules still apply the same as any other role) — cannot create/edit/delete anything.</summary>
        Viewer
    }
}

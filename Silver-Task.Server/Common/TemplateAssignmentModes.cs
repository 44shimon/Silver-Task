namespace Silver_Task.Server.Common
{
    /// <summary>Plain text, not an enum — same reasoning as DependencyTypes/NotificationTypes.
    /// "Keep Template Assignment" (spec's own wizard option) isn't a stored mode here — it just
    /// means "don't override," i.e. use whatever mode the template already has, so only the three
    /// genuine defaults need a constant.</summary>
    public static class TemplateAssignmentModes
    {
        public const string Unassigned = "Unassigned";

        /// <summary>Resolves to the created project's OwnerId at instantiation time — this app has
        /// no separate "project manager" field, only an owner who is always implicitly a Manager
        /// (see ProjectAccessService) — so "assign to the Project Manager" and "assign to the
        /// project's owner" are the same thing here.</summary>
        public const string ProjectManager = "ProjectManager";

        /// <summary>Uses AssignedToUserId on the template task/template itself.</summary>
        public const string SpecificUser = "SpecificUser";

        public static readonly IReadOnlyList<string> All = [Unassigned, ProjectManager, SpecificUser];
    }
}

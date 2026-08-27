namespace Silver_Task.Server.Common
{
    /// <summary>The full permission matrix (Phase 32) — plain "Group.Action" strings, same
    /// extensibility rationale as NotificationTypes/DependencyTypes (a new permission is a new
    /// constant, not a migration). This is the single source of truth for what each system role
    /// and project role grants; PermissionService is the only place that reads it.
    ///
    /// Deliberately NOT a database-editable table: the matrix itself (which permissions exist,
    /// which roles grant them) is fixed, code-reviewed configuration — like TaskItemStatus or
    /// CustomFieldType, not like NotificationTypes' "grows over time" values. What *is* dynamic
    /// (and admin-editable) is which role a given user/membership has, not what each role means.
    /// See PermissionService's doc comment for the full reasoning behind this scope decision.</summary>
    public static class Permissions
    {
        // Users (system-level; Administrator only today — see PermissionService.SystemMatrix)
        public const string UsersView = "Users.View";
        public const string UsersCreate = "Users.Create";
        public const string UsersEdit = "Users.Edit";
        public const string UsersDelete = "Users.Delete";
        public const string UsersManageRoles = "Users.ManageRoles";

        // Projects (system-level "can create a project at all" + project-scoped "can manage
        // membership/roles/settings for THIS project")
        public const string ProjectsView = "Projects.View";
        public const string ProjectsCreate = "Projects.Create";
        public const string ProjectsEdit = "Projects.Edit";
        public const string ProjectsDelete = "Projects.Delete";
        public const string ProjectsManageMembers = "Projects.ManageMembers";

        // Tasks (project-scoped)
        public const string TasksView = "Tasks.View";
        public const string TasksCreate = "Tasks.Create";
        public const string TasksEdit = "Tasks.Edit";
        public const string TasksDelete = "Tasks.Delete";
        public const string TasksAssign = "Tasks.Assign";

        // Comments (project-scoped; delete is intentionally author-only at the service layer —
        // see CommentService's own doc comment — this permission still gates *creating* one)
        public const string CommentsCreate = "Comments.Create";
        public const string CommentsDelete = "Comments.Delete";

        // Files (project-scoped)
        public const string FilesUpload = "Files.Upload";
        public const string FilesDelete = "Files.Delete";

        // Dependencies / Recurring Tasks (project-scoped — share the Tasks.Edit tier since this
        // app has no finer-grained sub-permission for them today; listed separately here so the
        // matrix/UI can label them distinctly even though enforcement reuses the Edit tier)
        public const string DependenciesManage = "Dependencies.Manage";
        public const string RecurringTasksManage = "RecurringTasks.Manage";

        // Custom Fields (project-scoped; definition changes are Manage-tier)
        public const string CustomFieldsManage = "CustomFields.Manage";

        // Automations (Phase 35; project-scoped — View is available to every project role
        // (including Viewer) for transparency into what runs against their project; Create/Edit/
        // Delete/Execute are Manage-tier only, deliberately not relaxed for the automation's own
        // creator — see AutomationService's own doc comment on why this is uniformly stricter than
        // Files.Upload/Delete's creator-or-manager model. Execute covers Test and Retry, the only
        // two ways a user can manually trigger automation logic outside of its own event.)
        public const string AutomationsView = "Automations.View";
        public const string AutomationsCreate = "Automations.Create";
        public const string AutomationsEdit = "Automations.Edit";
        public const string AutomationsDelete = "Automations.Delete";
        public const string AutomationsExecute = "Automations.Execute";

        // Reports (system-level; no dedicated reports feature exists yet beyond Admin stats)
        public const string ReportsView = "Reports.View";
        public const string ReportsExport = "Reports.Export";

        // Settings (system-level)
        public const string SettingsView = "Settings.View";
        public const string SettingsEdit = "Settings.Edit";

        // Administration (system-level — gates the whole /admin area)
        public const string AdministrationAccess = "Administration.Access";

        public static readonly IReadOnlyList<string> All =
        [
            UsersView, UsersCreate, UsersEdit, UsersDelete, UsersManageRoles,
            ProjectsView, ProjectsCreate, ProjectsEdit, ProjectsDelete, ProjectsManageMembers,
            TasksView, TasksCreate, TasksEdit, TasksDelete, TasksAssign,
            CommentsCreate, CommentsDelete,
            FilesUpload, FilesDelete,
            DependenciesManage, RecurringTasksManage,
            CustomFieldsManage,
            AutomationsView, AutomationsCreate, AutomationsEdit, AutomationsDelete, AutomationsExecute,
            ReportsView, ReportsExport,
            SettingsView, SettingsEdit,
            AdministrationAccess
        ];

        /// <summary>Group label -> permission codes, in the exact grouping/order the spec's
        /// "Admin -> Roles & Permissions" mockup uses, for the read-only permission-matrix UI.</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Groups = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Users"] = [UsersView, UsersCreate, UsersEdit, UsersDelete, UsersManageRoles],
            ["Projects"] = [ProjectsView, ProjectsCreate, ProjectsEdit, ProjectsDelete, ProjectsManageMembers],
            ["Tasks"] = [TasksView, TasksCreate, TasksEdit, TasksDelete, TasksAssign],
            ["Comments"] = [CommentsCreate, CommentsDelete],
            ["Files"] = [FilesUpload, FilesDelete],
            ["Dependencies & Recurring Tasks"] = [DependenciesManage, RecurringTasksManage],
            ["Custom Fields"] = [CustomFieldsManage],
            ["Automations"] = [AutomationsView, AutomationsCreate, AutomationsEdit, AutomationsDelete, AutomationsExecute],
            ["Reports"] = [ReportsView, ReportsExport],
            ["Settings"] = [SettingsView, SettingsEdit],
            ["Administration"] = [AdministrationAccess]
        };
    }
}

namespace Silver_Task.Server.Common
{
    /// <summary>Every known system setting key, grouped to match the Admin System Settings
    /// page's five sections. Plain strings (not an enum) for the same reason
    /// NotificationTypes/CustomFieldType are — a new setting is a new constant, not a
    /// migration.</summary>
    public static class SystemSettingKeys
    {
        // General
        public const string ApplicationName = "General.ApplicationName";
        public const string ApplicationDescription = "General.ApplicationDescription";
        public const string DefaultTimeZone = "General.DefaultTimeZone";
        public const string DefaultDateFormat = "General.DefaultDateFormat";
        public const string DefaultTimeFormat = "General.DefaultTimeFormat";
        public const string DefaultItemsPerPage = "General.DefaultItemsPerPage";

        // Task Defaults
        public const string DefaultTaskStatus = "TaskDefaults.DefaultStatus";
        public const string DefaultTaskPriority = "TaskDefaults.DefaultPriority";

        // Recurring Tasks
        public const string RecurringTaskGenerationWindowDays = "RecurringTasks.GenerationWindowDays";

        // Project Defaults
        public const string RequireProjectDescription = "ProjectDefaults.RequireDescription";

        // Security
        public const string SessionTimeoutMinutes = "Security.SessionTimeoutMinutes";
        public const string MinPasswordLength = "Security.MinPasswordLength";
        public const string RequirePasswordComplexity = "Security.RequirePasswordComplexity";
        public const string MaxFailedLoginAttempts = "Security.MaxFailedLoginAttempts";
        public const string AccountLockoutDurationMinutes = "Security.AccountLockoutDurationMinutes";

        // System Behavior
        public const string AllowUsersToCreateProjects = "Behavior.AllowUsersToCreateProjects";
        public const string AllowMembersToCreateTasks = "Behavior.AllowMembersToCreateTasks";
        public const string AllowMembersToDeleteTasks = "Behavior.AllowMembersToDeleteTasks";
        public const string AllowUsersToCreateCustomFields = "Behavior.AllowUsersToCreateCustomFields";
        public const string AllowComments = "Behavior.AllowComments";
        public const string AllowAttachments = "Behavior.AllowAttachments";
    }
}

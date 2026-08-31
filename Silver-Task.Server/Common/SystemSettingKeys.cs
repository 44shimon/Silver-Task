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
        // Phase 45 — the base URL email action links (and the always-appended "manage
        // preferences" footer link) are resolved against. Falls back to the first configured
        // Cors:AllowedOrigins entry when empty (see NotificationService.ResolveAppBaseUrlAsync)
        // for backward compatibility with pre-Phase-45 deployments that never set this.
        public const string ApplicationBaseUrl = "General.ApplicationBaseUrl";

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

        // Attachments (Phase 33)
        public const string MaxAttachmentSizeMb = "Attachments.MaxSizeMb";
        public const string AllowedAttachmentExtensions = "Attachments.AllowedExtensions";

        // Notifications (Phase 36)
        public const string NotificationRetentionDays = "Notifications.RetentionDays";
        public const string EmailNotificationsEnabled = "Notifications.EmailNotificationsEnabled";
        public const string DailyDigestEnabled = "Notifications.DailyDigestEnabled";
        public const string MaxNotificationBatchSize = "Notifications.MaxBatchSize";
        // Phase 46 — defaults applied to a brand-new user's UserPreference row (see
        // UserPreferencesService.GetOrCreateAsync), same "admin default, user can override"
        // pattern as DefaultTimeZone/DefaultDateFormat.
        public const string DefaultDailyDigestTime = "Notifications.DefaultDailyDigestTime";
        public const string DefaultWeeklyDigestDay = "Notifications.DefaultWeeklyDigestDay";
        public const string DefaultWeeklyDigestTime = "Notifications.DefaultWeeklyDigestTime";
    }
}

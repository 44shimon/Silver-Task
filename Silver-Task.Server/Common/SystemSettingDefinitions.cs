namespace Silver_Task.Server.Common
{
    public enum SystemSettingSection
    {
        General,
        TaskDefaults,
        ProjectDefaults,
        Security,
        Behavior,
        RecurringTasks,
        Attachments,
        Notifications
    }

    public record SystemSettingDefinition(
        string Key,
        SystemSettingSection Section,
        string DefaultValue,
        string ValueType,
        string Description);

    /// <summary>
    /// The full known set of system settings and their defaults. Every default here matches
    /// this app's actual behavior *before* Phase 24 existed, so introducing this feature (and
    /// applying its migration) changes nothing observable until an Administrator explicitly
    /// changes a value — the safest possible rollout for something that gates real permissions.
    /// </summary>
    public static class SystemSettingDefinitions
    {
        public static readonly IReadOnlyList<SystemSettingDefinition> All =
        [
            new(SystemSettingKeys.ApplicationName, SystemSettingSection.General,
                "Silver-Task", "string", "The application's display name (Topbar, login page)."),
            new(SystemSettingKeys.ApplicationDescription, SystemSettingSection.General,
                "A spreadsheet-style task management application.", "string", "Shown on the login page."),
            new(SystemSettingKeys.DefaultTimeZone, SystemSettingSection.General,
                "UTC", "string", "Time zone assigned to a user's preferences the first time they're created."),
            new(SystemSettingKeys.DefaultDateFormat, SystemSettingSection.General,
                "MM/dd/yyyy", "string", "Date format assigned to a user's preferences the first time they're created."),
            new(SystemSettingKeys.DefaultTimeFormat, SystemSettingSection.General,
                "12h", "string", "Time format assigned to a user's preferences the first time they're created."),
            new(SystemSettingKeys.DefaultItemsPerPage, SystemSettingSection.General,
                "25", "int", "Items-per-page assigned to a user's preferences the first time they're created."),

            new(SystemSettingKeys.DefaultTaskStatus, SystemSettingSection.TaskDefaults,
                "NotStarted", "string", "Status assigned to a newly created task when none is specified."),
            new(SystemSettingKeys.DefaultTaskPriority, SystemSettingSection.TaskDefaults,
                "Medium", "string", "Priority assigned to a newly created task when none is specified."),

            new(SystemSettingKeys.RequireProjectDescription, SystemSettingSection.ProjectDefaults,
                "false", "bool", "Require a description when creating a project."),

            new(SystemSettingKeys.SessionTimeoutMinutes, SystemSettingSection.Security,
                "240", "int", "How long a login session stays valid, in minutes."),
            new(SystemSettingKeys.MinPasswordLength, SystemSettingSection.Security,
                "8", "int", "Minimum password length, enforced on signup, password change, and admin reset."),
            new(SystemSettingKeys.RequirePasswordComplexity, SystemSettingSection.Security,
                "false", "bool", "Require at least one uppercase letter, one lowercase letter, and one digit."),
            new(SystemSettingKeys.MaxFailedLoginAttempts, SystemSettingSection.Security,
                "5", "int", "Consecutive failed logins allowed before an account is temporarily locked out."),
            new(SystemSettingKeys.AccountLockoutDurationMinutes, SystemSettingSection.Security,
                "15", "int", "How long an account stays locked out after too many failed logins."),

            new(SystemSettingKeys.AllowUsersToCreateProjects, SystemSettingSection.Behavior,
                "true", "bool", "Allow any authenticated user (not just Administrators) to create projects."),
            new(SystemSettingKeys.AllowMembersToCreateTasks, SystemSettingSection.Behavior,
                "true", "bool", "Allow plain Members (not just Managers/owners) to create tasks."),
            new(SystemSettingKeys.AllowMembersToDeleteTasks, SystemSettingSection.Behavior,
                "false", "bool", "Allow plain Members (not just Managers/owners) to delete tasks."),
            new(SystemSettingKeys.AllowUsersToCreateCustomFields, SystemSettingSection.Behavior,
                "false", "bool", "Allow plain Members (not just Managers/owners) to create custom fields."),
            new(SystemSettingKeys.AllowComments, SystemSettingSection.Behavior,
                "true", "bool", "Allow new comments to be posted on tasks."),
            new(SystemSettingKeys.AllowAttachments, SystemSettingSection.Behavior,
                "true", "bool", "Allow new file attachments to be uploaded to tasks."),

            new(SystemSettingKeys.RecurringTaskGenerationWindowDays, SystemSettingSection.RecurringTasks,
                "30", "int", "How many days ahead recurring task occurrences are generated. Missed occurrences older than 7 days are skipped rather than backfilled."),

            // 25 MB preserves this app's original attachment default exactly (see
            // AttachmentService's own doc comment) — Phase 33 makes it admin-configurable without
            // silently changing behavior for existing deployments.
            new(SystemSettingKeys.MaxAttachmentSizeMb, SystemSettingSection.Attachments,
                "25", "int", "Maximum size, in MB, for a single file attachment."),
            new(SystemSettingKeys.AllowedAttachmentExtensions, SystemSettingSection.Attachments,
                ".pdf,.doc,.docx,.xls,.xlsx,.csv,.txt,.jpg,.jpeg,.png,.gif,.webp,.zip", "string",
                "Comma-separated list of allowed file extensions for attachments, including the leading dot."),

            new(SystemSettingKeys.NotificationRetentionDays, SystemSettingSection.Notifications,
                "90", "int", "Notifications older than this many days are automatically deleted."),
            // Off by default: this environment ships no SMTP configuration out of the box (see
            // Services/EmailService's own doc comment) — an Administrator turns this on only
            // after configuring real SMTP settings, so no user silently expects emails that can
            // never actually be delivered.
            new(SystemSettingKeys.EmailNotificationsEnabled, SystemSettingSection.Notifications,
                "false", "bool", "Global switch for outgoing notification emails. Off by default until SMTP is configured."),
            new(SystemSettingKeys.DailyDigestEnabled, SystemSettingSection.Notifications,
                "true", "bool", "Allow users to receive a daily email digest instead of individual emails (still gated by the switch above)."),
            new(SystemSettingKeys.MaxNotificationBatchSize, SystemSettingSection.Notifications,
                "100", "int", "Maximum notifications processed per background sweep/digest run, to bound worst-case load.")
        ];

        public static readonly IReadOnlyDictionary<string, SystemSettingDefinition> ByKey =
            All.ToDictionary(d => d.Key);
    }
}

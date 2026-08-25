namespace Silver_Task.Server.Models.Entities
{
    /// <summary>
    /// Generic key/value system configuration — one row per setting that's actually been
    /// changed from its default (see Common.SystemSettingDefinitions for the full known set
    /// and defaults; a key with no row here just means "still at its default"). Same
    /// EAV-for-extensibility reasoning as CustomFields/UserNotificationSettings: a new setting
    /// is a new entry in SystemSettingDefinitions, not a migration.
    /// </summary>
    public class SystemSetting
    {
        public Guid Id { get; set; }

        public required string Key { get; set; }

        public string? Value { get; set; }

        /// <summary>"string" | "int" | "bool" — drives how Value gets parsed/validated.</summary>
        public required string ValueType { get; set; }

        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Guid? UpdatedByUserId { get; set; }

        public User? UpdatedByUser { get; set; }
    }
}

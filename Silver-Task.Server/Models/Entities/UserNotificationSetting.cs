namespace Silver_Task.Server.Models.Entities
{
    /// <summary>One row per (user, notification type) the user has ever explicitly set — an
    /// EAV-style table, deliberately, so a new notification type (see
    /// Common.NotificationTypes) can be introduced later with zero migration, the same reason
    /// CustomFields stores FieldType as free text instead of one column per field type. A user
    /// with no row for a given type simply defaults to enabled (see
    /// UserNotificationSettingsService.GetAllAsync) rather than requiring a backfill.</summary>
    public class UserNotificationSetting
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string NotificationType { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }
    }
}

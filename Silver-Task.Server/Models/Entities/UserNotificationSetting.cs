namespace Silver_Task.Server.Models.Entities
{
    /// <summary>One row per (user, notification type) the user has ever explicitly set — an
    /// EAV-style table, deliberately, so a new notification type (see
    /// Common.NotificationTypes) can be introduced later with zero migration, the same reason
    /// CustomFields stores FieldType as free text instead of one column per field type. A user
    /// with no row for a given type simply defaults to InAppEnabled=true, EmailDeliveryMode=
    /// "Immediately" (see UserNotificationSettingsService.GetAllAsync) rather than requiring a
    /// backfill.
    ///
    /// Phase 36 split the original single IsEnabled flag into two independent channels
    /// (InAppEnabled/EmailEnabled) rather than adding a second EAV table — same row, same
    /// migration-free extensibility, just one more column, matching the spec's own "users should
    /// independently control in-app vs. email" requirement. Phase 46 widens the email channel
    /// from a bool into EmailDeliveryMode ("Immediately"/"DailyDigest"/"WeeklyDigest"/"Off") so
    /// each type can be batched into a digest instead of only ever being fully on/off — InAppEnabled
    /// itself is untouched, so this never changes Phase 44's in-app notification behavior.</summary>
    public class UserNotificationSetting
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string NotificationType { get; set; }

        public bool InAppEnabled { get; set; } = true;

        /// <summary>"Immediately" | "DailyDigest" | "WeeklyDigest" | "Off" — see
        /// Common.NotificationDeliveryModes. A type whose NotificationPriorities.For is Urgent
        /// (currently only TaskOverdue) always sends immediately regardless of what's stored here
        /// — enforced server-side in both NotificationService.MaybeSendEmailAsync and
        /// UserNotificationSettingsService.UpdateAsync (defense in depth, not just a UI lock).</summary>
        public string EmailDeliveryMode { get; set; } = "Immediately";

        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }
    }
}

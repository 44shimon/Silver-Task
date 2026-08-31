namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class NotificationSettingDto
    {
        public required string NotificationType { get; set; }

        public bool InAppEnabled { get; set; }

        /// <summary>"Immediately" | "DailyDigest" | "WeeklyDigest" | "Off" — see
        /// Common.NotificationDeliveryModes.</summary>
        public required string EmailDeliveryMode { get; set; }

        /// <summary>True for Urgent-priority types (currently only TaskOverdue) — the frontend
        /// disables the dropdown and shows a "Always immediate" note rather than letting the user
        /// pick a mode the server will silently override anyway (spec's own "make override
        /// behavior visible" requirement).</summary>
        public bool AlwaysImmediate { get; set; }
    }
}

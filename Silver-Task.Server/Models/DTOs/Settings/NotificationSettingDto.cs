namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class NotificationSettingDto
    {
        public required string NotificationType { get; set; }

        public bool IsEnabled { get; set; }
    }
}

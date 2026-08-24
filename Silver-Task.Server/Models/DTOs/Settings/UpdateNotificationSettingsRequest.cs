using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class UpdateNotificationSettingsRequest
    {
        [Required]
        public required List<NotificationSettingDto> Settings { get; set; }
    }
}

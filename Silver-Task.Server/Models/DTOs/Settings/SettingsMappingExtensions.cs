using Silver_Task.Server.Common;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Settings
{
    public static class SettingsMappingExtensions
    {
        public static SystemSettingDto ToDto(this SystemSetting setting) => new()
        {
            Key = setting.Key,
            Section = SystemSettingDefinitions.ByKey[setting.Key].Section.ToString(),
            Value = setting.Value ?? SystemSettingDefinitions.ByKey[setting.Key].DefaultValue,
            ValueType = setting.ValueType,
            Description = setting.Description,
            UpdatedAt = setting.UpdatedByUserId is null ? null : setting.UpdatedAt,
            UpdatedByName = setting.UpdatedByUser?.Name
        };

        public static UserPreferencesDto ToDto(this UserPreference preference) => new()
        {
            Theme = preference.Theme,
            DefaultProjectId = preference.DefaultProjectId,
            DefaultTaskView = preference.DefaultTaskView,
            DateFormat = preference.DateFormat,
            TimeFormat = preference.TimeFormat,
            TimeZone = preference.TimeZone,
            ItemsPerPage = preference.ItemsPerPage
        };

        public static NotificationSettingDto ToDto(this UserNotificationSetting setting) => new()
        {
            NotificationType = setting.NotificationType,
            IsEnabled = setting.IsEnabled
        };
    }
}

using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    public interface IUserNotificationSettingsService
    {
        /// <summary>Always returns one entry per NotificationTypes.All, not just the ones the
        /// user has explicitly saved — a type with no stored row defaults to enabled, so adding
        /// a new notification type later doesn't require backfilling every existing user.</summary>
        Task<IReadOnlyList<UserNotificationSetting>> GetAllAsync(Guid userId);

        Task<IReadOnlyList<UserNotificationSetting>> UpdateAsync(Guid userId, UpdateNotificationSettingsRequest request);
    }

    public class UserNotificationSettingsService(AppDbContext db) : IUserNotificationSettingsService
    {
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<UserNotificationSetting>> GetAllAsync(Guid userId)
        {
            var stored = await _db.UserNotificationSettings
                .Where(s => s.UserId == userId)
                .ToDictionaryAsync(s => s.NotificationType, StringComparer.OrdinalIgnoreCase);

            return NotificationTypes.All
                .Select(type => stored.TryGetValue(type, out var existing)
                    ? existing
                    : new UserNotificationSetting { UserId = userId, NotificationType = type, IsEnabled = true })
                .ToList();
        }

        public async Task<IReadOnlyList<UserNotificationSetting>> UpdateAsync(Guid userId, UpdateNotificationSettingsRequest request)
        {
            var knownTypes = new HashSet<string>(NotificationTypes.All, StringComparer.OrdinalIgnoreCase);
            foreach (var setting in request.Settings)
            {
                if (!knownTypes.Contains(setting.NotificationType))
                {
                    throw new ValidationException($"'{setting.NotificationType}' is not a recognized notification type.");
                }
            }

            var existing = await _db.UserNotificationSettings
                .Where(s => s.UserId == userId)
                .ToDictionaryAsync(s => s.NotificationType, StringComparer.OrdinalIgnoreCase);

            foreach (var setting in request.Settings)
            {
                if (existing.TryGetValue(setting.NotificationType, out var row))
                {
                    row.IsEnabled = setting.IsEnabled;
                    row.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.UserNotificationSettings.Add(new UserNotificationSetting
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        NotificationType = setting.NotificationType,
                        IsEnabled = setting.IsEnabled
                    });
                }
            }

            await _db.SaveChangesAsync();
            return await GetAllAsync(userId);
        }
    }
}

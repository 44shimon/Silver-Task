using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IUserNotificationSettingsService
    {
        /// <summary>Always returns one entry per NotificationTypes.All, not just the ones the
        /// user has explicitly saved — a type with no stored row defaults to both channels
        /// enabled, so adding a new notification type later doesn't require backfilling every
        /// existing user.</summary>
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
                    : new UserNotificationSetting { UserId = userId, NotificationType = type, InAppEnabled = true, EmailDeliveryMode = NotificationDeliveryModes.Immediately })
                .ToList();
        }

        public async Task<IReadOnlyList<UserNotificationSetting>> UpdateAsync(Guid userId, UpdateNotificationSettingsRequest request)
        {
            var knownTypes = new HashSet<string>(NotificationTypes.All, StringComparer.OrdinalIgnoreCase);
            var knownModes = new HashSet<string>(NotificationDeliveryModes.All, StringComparer.OrdinalIgnoreCase);
            foreach (var setting in request.Settings)
            {
                if (!knownTypes.Contains(setting.NotificationType))
                {
                    throw new ValidationException($"'{setting.NotificationType}' is not a recognized notification type.");
                }
                if (!knownModes.Contains(setting.EmailDeliveryMode))
                {
                    throw new ValidationException($"'{setting.EmailDeliveryMode}' is not a recognized email delivery mode.");
                }
            }

            var existing = await _db.UserNotificationSettings
                .Where(s => s.UserId == userId)
                .ToDictionaryAsync(s => s.NotificationType, StringComparer.OrdinalIgnoreCase);

            foreach (var setting in request.Settings)
            {
                // Defense in depth, not just a UI lock (spec's own "override behavior" rule) — an
                // Urgent-priority type (currently only TaskOverdue) always sends immediately
                // regardless of what a caller posts, matching NotificationService.MaybeSendEmailAsync's
                // own enforcement of the same rule at send time.
                var emailMode = NotificationPriorities.For(setting.NotificationType) == NotificationPriority.Urgent
                    ? NotificationDeliveryModes.Immediately
                    : setting.EmailDeliveryMode;

                if (existing.TryGetValue(setting.NotificationType, out var row))
                {
                    row.InAppEnabled = setting.InAppEnabled;
                    row.EmailDeliveryMode = emailMode;
                    row.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.UserNotificationSettings.Add(new UserNotificationSetting
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        NotificationType = setting.NotificationType,
                        InAppEnabled = setting.InAppEnabled,
                        EmailDeliveryMode = emailMode
                    });
                }
            }

            await _db.SaveChangesAsync();
            return await GetAllAsync(userId);
        }
    }
}

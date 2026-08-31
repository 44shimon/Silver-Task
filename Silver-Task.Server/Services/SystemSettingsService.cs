using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ISystemSettingsService
    {
        /// <summary>Every known setting (see SystemSettingDefinitions.All), falling back to its
        /// default for any key with no stored row yet — same lazy-default pattern as
        /// UserNotificationSettingsService, so a newly added setting doesn't need a data
        /// backfill for existing installs.</summary>
        Task<IReadOnlyList<SystemSetting>> GetAllAsync();

        Task<PublicSettingsDto> GetPublicSettingsAsync();

        Task<string> GetStringAsync(string key);

        Task<int> GetIntAsync(string key);

        Task<bool> GetBoolAsync(string key);

        Task UpdateAsync(Guid updatedByUserId, IReadOnlyDictionary<string, string> values);
    }

    /// <summary>
    /// Generic key/value settings store. Every value is validated against its declared
    /// ValueType (and, for several keys, extra semantic bounds — see ValidateValue) before
    /// being persisted, and every read goes through a typed accessor — nothing downstream ever
    /// touches a raw, unvalidated string from the frontend.
    /// </summary>
    public class SystemSettingsService(AppDbContext db) : ISystemSettingsService
    {
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<SystemSetting>> GetAllAsync()
        {
            var stored = await _db.SystemSettings.Include(s => s.UpdatedByUser).ToDictionaryAsync(s => s.Key);

            return SystemSettingDefinitions.All
                .Select(def => stored.TryGetValue(def.Key, out var existing)
                    ? existing
                    : new SystemSetting { Key = def.Key, Value = def.DefaultValue, ValueType = def.ValueType, Description = def.Description })
                .ToList();
        }

        public async Task<PublicSettingsDto> GetPublicSettingsAsync() => new()
        {
            ApplicationName = await GetStringAsync(SystemSettingKeys.ApplicationName),
            ApplicationDescription = await GetStringAsync(SystemSettingKeys.ApplicationDescription)
        };

        public async Task<string> GetStringAsync(string key) => await GetRawAsync(key);

        public async Task<int> GetIntAsync(string key) => int.Parse(await GetRawAsync(key));

        public async Task<bool> GetBoolAsync(string key) => bool.Parse(await GetRawAsync(key));

        public async Task UpdateAsync(Guid updatedByUserId, IReadOnlyDictionary<string, string> values)
        {
            foreach (var (key, value) in values)
            {
                if (!SystemSettingDefinitions.ByKey.TryGetValue(key, out var definition))
                {
                    throw new ValidationException($"'{key}' is not a recognized system setting.");
                }
                ValidateValue(definition, value);
            }

            var existing = await _db.SystemSettings.Where(s => values.Keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key);
            foreach (var (key, value) in values)
            {
                if (existing.TryGetValue(key, out var row))
                {
                    row.Value = value;
                    row.UpdatedAt = DateTime.UtcNow;
                    row.UpdatedByUserId = updatedByUserId;
                }
                else
                {
                    var definition = SystemSettingDefinitions.ByKey[key];
                    _db.SystemSettings.Add(new SystemSetting
                    {
                        Id = Guid.NewGuid(),
                        Key = key,
                        Value = value,
                        ValueType = definition.ValueType,
                        Description = definition.Description,
                        UpdatedByUserId = updatedByUserId
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        private async Task<string> GetRawAsync(string key)
        {
            var setting = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value ?? SystemSettingDefinitions.ByKey[key].DefaultValue;
        }

        private static void ValidateValue(SystemSettingDefinition definition, string value)
        {
            switch (definition.ValueType)
            {
                case "bool":
                    if (!bool.TryParse(value, out _))
                    {
                        throw new ValidationException($"'{definition.Key}' must be true or false.");
                    }
                    break;

                case "int":
                    if (!int.TryParse(value, out var intValue))
                    {
                        throw new ValidationException($"'{definition.Key}' must be a whole number.");
                    }
                    ValidateIntBounds(definition.Key, intValue);
                    break;

                case "string":
                    ValidateStringValue(definition.Key, value);
                    break;
            }
        }

        private static void ValidateIntBounds(string key, int value)
        {
            switch (key)
            {
                case SystemSettingKeys.DefaultItemsPerPage:
                    if (value is < 5 or > 200)
                    {
                        throw new ValidationException("Default items per page must be between 5 and 200.");
                    }
                    break;
                case SystemSettingKeys.SessionTimeoutMinutes:
                    if (value is < 5 or > 43200) // 30 days
                    {
                        throw new ValidationException("Session timeout must be between 5 minutes and 30 days.");
                    }
                    break;
                case SystemSettingKeys.MinPasswordLength:
                    if (value is < 6 or > 128)
                    {
                        throw new ValidationException("Minimum password length must be between 6 and 128.");
                    }
                    break;
                case SystemSettingKeys.MaxFailedLoginAttempts:
                    if (value is < 3 or > 20)
                    {
                        throw new ValidationException("Maximum failed login attempts must be between 3 and 20.");
                    }
                    break;
                case SystemSettingKeys.AccountLockoutDurationMinutes:
                    if (value is < 1 or > 1440) // 24 hours
                    {
                        throw new ValidationException("Account lockout duration must be between 1 and 1440 minutes.");
                    }
                    break;
                case SystemSettingKeys.RecurringTaskGenerationWindowDays:
                    if (value is < 7 or > 180)
                    {
                        throw new ValidationException("Recurring task generation window must be between 7 and 180 days.");
                    }
                    break;
                case SystemSettingKeys.MaxAttachmentSizeMb:
                    if (value is < 1 or > 500)
                    {
                        throw new ValidationException("Maximum attachment size must be between 1 and 500 MB.");
                    }
                    break;
                case SystemSettingKeys.NotificationRetentionDays:
                    if (value is < 7 or > 3650)
                    {
                        throw new ValidationException("Notification retention must be between 7 and 3650 days.");
                    }
                    break;
                case SystemSettingKeys.MaxNotificationBatchSize:
                    if (value is < 10 or > 10000)
                    {
                        throw new ValidationException("Maximum notification batch size must be between 10 and 10000.");
                    }
                    break;
            }
        }

        private static void ValidateStringValue(string key, string value)
        {
            if (key is SystemSettingKeys.ApplicationName && string.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException("Application name cannot be empty.");
            }

            // Reuses the exact same allow-lists UserPreferencesService validates against, and
            // .NET's real time zone database for DefaultTimeZone — a system default that
            // wouldn't validate as a *user* preference shouldn't be settable as the default
            // either.
            if (key is SystemSettingKeys.ApplicationBaseUrl && !string.IsNullOrWhiteSpace(value))
            {
                var isValidHttpUrl = Uri.TryCreate(value, UriKind.Absolute, out var baseUri) && baseUri.Scheme is "http" or "https";
                if (!isValidHttpUrl)
                {
                    throw new ValidationException("Application base URL must be empty or a valid absolute http(s) URL.");
                }
            }
            if (key is SystemSettingKeys.DefaultDateFormat &&
                value is not ("MM/dd/yyyy" or "dd/MM/yyyy" or "yyyy-MM-dd" or "dd MMM yyyy"))
            {
                throw new ValidationException($"'{value}' is not a supported date format.");
            }
            if (key is SystemSettingKeys.DefaultTimeFormat && value is not ("12h" or "24h"))
            {
                throw new ValidationException($"'{value}' is not a valid time format.");
            }
            if (key is SystemSettingKeys.DefaultTimeZone)
            {
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(value);
                }
                catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    throw new ValidationException($"'{value}' is not a recognized time zone.");
                }
            }
            if (key is SystemSettingKeys.DefaultTaskStatus && !Enum.TryParse<TaskItemStatus>(value, out _))
            {
                throw new ValidationException($"'{value}' is not a valid task status.");
            }
            if (key is SystemSettingKeys.DefaultTaskPriority && !Enum.TryParse<TaskPriority>(value, out _))
            {
                throw new ValidationException($"'{value}' is not a valid task priority.");
            }
            if (key is SystemSettingKeys.AllowedAttachmentExtensions)
            {
                var extensions = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (extensions.Length == 0)
                {
                    throw new ValidationException("At least one allowed file extension is required.");
                }
                foreach (var extension in extensions)
                {
                    if (!extension.StartsWith('.') || extension.Length is < 2 or > 10 || extension.Any(char.IsWhiteSpace))
                    {
                        throw new ValidationException($"'{extension}' is not a valid file extension (expected a leading dot, e.g. '.pdf').");
                    }
                }
            }
        }
    }
}

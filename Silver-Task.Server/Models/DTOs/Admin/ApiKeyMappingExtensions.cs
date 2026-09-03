using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Admin
{
    public static class ApiKeyMappingExtensions
    {
        private const string Active = "Active";
        private const string Revoked = "Revoked";
        private const string Expired = "Expired";

        /// <summary>Assumes User and CreatedByUser are loaded (ApiKeyService.GetAllApiKeysAsync/
        /// GetApiKeyByIdAsync both .Include them).</summary>
        public static ApiKeyDto ToDto(this ApiKey key) => new()
        {
            Id = key.Id,
            Name = key.Name,
            KeyPrefix = key.KeyPrefix,
            Status = ComputeStatus(key),
            Owner = key.User!.ToSummaryDto(),
            ExpiresAt = key.ExpiresAt,
            RevokedAt = key.RevokedAt,
            LastUsedAt = key.LastUsedAt,
            CreatedAt = key.CreatedAt,
            CreatedBy = key.CreatedByUser?.ToSummaryDto()
        };

        public static ApiKeyCreatedDto ToCreatedDto(this ApiKey key, string plaintextKey) => new()
        {
            Id = key.Id,
            Name = key.Name,
            KeyPrefix = key.KeyPrefix,
            Status = ComputeStatus(key),
            Owner = key.User!.ToSummaryDto(),
            ExpiresAt = key.ExpiresAt,
            RevokedAt = key.RevokedAt,
            LastUsedAt = key.LastUsedAt,
            CreatedAt = key.CreatedAt,
            CreatedBy = key.CreatedByUser?.ToSummaryDto(),
            Key = plaintextKey
        };

        public static ServiceAccountDto ToServiceAccountDto(this User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };

        private static string ComputeStatus(ApiKey key)
        {
            if (key.RevokedAt is not null)
            {
                return Revoked;
            }
            if (key.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
            {
                return Expired;
            }
            return Active;
        }
    }
}

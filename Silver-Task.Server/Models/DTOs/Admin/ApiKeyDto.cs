using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Admin
{
    /// <summary>Never carries the raw key or its hash — only KeyPrefix (see ApiKey.cs's own doc
    /// comment on why that's always safe to display). Status is computed, not stored — see
    /// ApiKeyMappingExtensions.</summary>
    public class ApiKeyDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string KeyPrefix { get; set; }

        public required string Status { get; set; }

        public required UserSummaryDto Owner { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public UserSummaryDto? CreatedBy { get; set; }
    }

    /// <summary>Returned only from the create/rotate endpoints — the one and only time the raw key
    /// is ever included in a response. Never returned by any GET.</summary>
    public class ApiKeyCreatedDto : ApiKeyDto
    {
        public required string Key { get; set; }
    }

    public class ServiceAccountDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Email { get; set; }

        public UserRole Role { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class CreateServiceAccountRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        public UserRole Role { get; set; } = UserRole.Member;
    }

    public class CreateApiKeyRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }
}

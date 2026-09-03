namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 62 — a credential that authenticates as UserId (a human's own key, or more
    /// typically a service account's — see User.IsServiceAccount) against Controllers/V1/* via the
    /// "ApiKey" auth scheme (X-Api-Key header). Only KeyHash (SHA-256 of the full issued key) and
    /// KeyPrefix (the first 12 characters, always safe to display) are ever persisted — the full
    /// key is generated, hashed, and returned exactly once at creation/rotation time; see
    /// ApiKeyService.GenerateKey. Status is deliberately not a stored column — it's always derived
    /// from RevokedAt/ExpiresAt (see ApiKeyMappingExtensions), so there's no way for a stored
    /// status to drift out of sync with an expiration date that simply passed.</summary>
    public class ApiKey
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Name { get; set; }

        public required string KeyPrefix { get; set; }

        public required string KeyHash { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        public Guid? RevokedByUserId { get; set; }

        /// <summary>Updated on successful API-key authentication — throttled to at most once a
        /// minute per key (see ApiKeyAuthenticationHandler) so a chatty integration doesn't turn
        /// this into a write-per-request hot path.</summary>
        public DateTime? LastUsedAt { get; set; }

        public Guid CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public User? RevokedByUser { get; set; }

        public User? CreatedByUser { get; set; }
    }
}

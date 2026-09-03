using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>Phase 62 — service-account and API-key lifecycle, owned together deliberately
    /// (they're tightly coupled: a key always belongs to a User, and a service account exists
    /// mainly to hold keys). A service account is an ordinary User row (User.IsServiceAccount) so
    /// every existing authorization check (ProjectAccessService, role checks, project membership)
    /// keeps working completely unmodified — adding one to a project uses the existing,
    /// unmodified IProjectService.AddMemberAsync by its generated email, exactly like adding a
    /// human member.</summary>
    public interface IApiKeyService
    {
        Task<User> CreateServiceAccountAsync(string name, UserRole role, Guid callerId);

        Task<IReadOnlyList<User>> GetAllServiceAccountsAsync();

        Task DeactivateServiceAccountAsync(Guid userId, Guid callerId);

        Task<(ApiKey Key, string PlaintextKey)> CreateApiKeyAsync(Guid userId, string name, DateTime? expiresAt, Guid callerId);

        Task<(ApiKey Key, string PlaintextKey)> RotateApiKeyAsync(Guid keyId, Guid callerId);

        Task RevokeApiKeyAsync(Guid keyId, Guid callerId);

        Task<IReadOnlyList<ApiKey>> GetAllApiKeysAsync();

        Task<ApiKey> GetApiKeyByIdAsync(Guid keyId);
    }

    public class ApiKeyService(AppDbContext db, IPasswordHasher<User> passwordHasher, ILogger<ApiKeyService> logger) : IApiKeyService
    {
        private const string KeyPrefixTag = "stak_";
        private const string EmailDomain = "service.invalid";
        private const int KeyPrefixDisplayLength = 12;

        private readonly AppDbContext _db = db;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly ILogger<ApiKeyService> _logger = logger;

        public async Task<User> CreateServiceAccountAsync(string name, UserRole role, Guid callerId)
        {
            var trimmedName = name.Trim();
            if (trimmedName.Length == 0)
            {
                throw new ValidationException("Service account name is required.");
            }

            var email = await GenerateUniqueServiceAccountEmailAsync(trimmedName);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                Email = email,
                PasswordHash = string.Empty,
                Role = role,
                IsActive = true,
                IsServiceAccount = true
            };
            // A random, never-disclosed, never-intended-to-be-used value — AuthService.LoginAsync
            // also unconditionally rejects IsServiceAccount at the password-login step regardless,
            // but this is defense in depth: even if that guard were ever bypassed, there is no
            // password anyone could correctly guess or have been told.
            user.PasswordHash = _passwordHasher.HashPassword(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogWarning("Admin action: service account {UserId} ({Email}) created by {CallerId}", user.Id, user.Email, callerId);
            return user;
        }

        public async Task<IReadOnlyList<User>> GetAllServiceAccountsAsync() =>
            await _db.Users.Where(u => u.IsServiceAccount).OrderBy(u => u.Name).ToListAsync();

        public async Task DeactivateServiceAccountAsync(Guid userId, Guid callerId)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsServiceAccount)
                ?? throw new NotFoundException($"Service account '{userId}' was not found.");

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            var now = DateTime.UtcNow;
            var activeKeys = await _db.ApiKeys.Where(k => k.UserId == userId && k.RevokedAt == null).ToListAsync();
            foreach (var key in activeKeys)
            {
                key.RevokedAt = now;
                key.RevokedByUserId = callerId;
            }

            await _db.SaveChangesAsync();
            _logger.LogWarning("Admin action: service account {UserId} deactivated ({KeyCount} key(s) revoked) by {CallerId}", userId, activeKeys.Count, callerId);
        }

        public async Task<(ApiKey Key, string PlaintextKey)> CreateApiKeyAsync(Guid userId, string name, DateTime? expiresAt, Guid callerId)
        {
            var trimmedName = name.Trim();
            if (trimmedName.Length == 0)
            {
                throw new ValidationException("API key name is required.");
            }
            if (expiresAt is { } exp && exp <= DateTime.UtcNow)
            {
                throw new ValidationException("expiresAt must be in the future.");
            }

            var owner = await _db.Users.FindAsync(userId)
                ?? throw new NotFoundException($"User '{userId}' was not found.");
            var caller = await _db.Users.FindAsync(callerId);

            var (plaintext, prefix, hash) = GenerateKey();

            var key = new ApiKey
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = trimmedName,
                KeyPrefix = prefix,
                KeyHash = hash,
                ExpiresAt = expiresAt,
                CreatedByUserId = callerId,
                CreatedAt = DateTime.UtcNow,
                // Set explicitly (not left to EF's change-tracker fixup, which only works if the
                // related entity happens to already be tracked) — ApiKeyMappingExtensions.ToDto/
                // ToCreatedDto both assume User is loaded, the same assumption every other
                // mapping extension in this app makes about its own entity's navigations.
                User = owner,
                CreatedByUser = caller
            };
            _db.ApiKeys.Add(key);
            await _db.SaveChangesAsync();

            _logger.LogWarning("Admin action: API key {KeyId} ({KeyPrefix}...) created for user {UserId} by {CallerId}", key.Id, prefix, userId, callerId);
            return (key, plaintext);
        }

        public async Task<(ApiKey Key, string PlaintextKey)> RotateApiKeyAsync(Guid keyId, Guid callerId)
        {
            var existing = await _db.ApiKeys.SingleOrDefaultAsync(k => k.Id == keyId)
                ?? throw new NotFoundException($"API key '{keyId}' was not found.");

            var now = DateTime.UtcNow;
            existing.RevokedAt = now;
            existing.RevokedByUserId = callerId;

            var owner = await _db.Users.FindAsync(existing.UserId);
            var caller = await _db.Users.FindAsync(callerId);

            var (plaintext, prefix, hash) = GenerateKey();
            var replacement = new ApiKey
            {
                Id = Guid.NewGuid(),
                UserId = existing.UserId,
                Name = existing.Name,
                KeyPrefix = prefix,
                KeyHash = hash,
                ExpiresAt = existing.ExpiresAt,
                CreatedByUserId = callerId,
                CreatedAt = now,
                User = owner,
                CreatedByUser = caller
            };
            _db.ApiKeys.Add(replacement);
            await _db.SaveChangesAsync();

            _logger.LogWarning("Admin action: API key {OldKeyId} rotated to {NewKeyId} ({KeyPrefix}...) by {CallerId}", existing.Id, replacement.Id, prefix, callerId);
            return (replacement, plaintext);
        }

        public async Task RevokeApiKeyAsync(Guid keyId, Guid callerId)
        {
            var key = await _db.ApiKeys.SingleOrDefaultAsync(k => k.Id == keyId)
                ?? throw new NotFoundException($"API key '{keyId}' was not found.");

            if (key.RevokedAt is not null)
            {
                return;
            }

            key.RevokedAt = DateTime.UtcNow;
            key.RevokedByUserId = callerId;
            await _db.SaveChangesAsync();

            _logger.LogWarning("Admin action: API key {KeyId} ({KeyPrefix}...) revoked by {CallerId}", key.Id, key.KeyPrefix, callerId);
        }

        public async Task<IReadOnlyList<ApiKey>> GetAllApiKeysAsync() =>
            await _db.ApiKeys.Include(k => k.User).Include(k => k.RevokedByUser).Include(k => k.CreatedByUser)
                .OrderByDescending(k => k.CreatedAt).ToListAsync();

        public async Task<ApiKey> GetApiKeyByIdAsync(Guid keyId)
        {
            var key = await _db.ApiKeys.Include(k => k.User).Include(k => k.RevokedByUser).Include(k => k.CreatedByUser)
                .SingleOrDefaultAsync(k => k.Id == keyId);
            return key ?? throw new NotFoundException($"API key '{keyId}' was not found.");
        }

        /// <summary>Generates a 256-bit random key, formatted stak_&lt;43 url-safe base64 chars&gt;.
        /// Returns the plaintext (returned to the caller exactly once, never persisted), the
        /// first 12 characters as a display-safe prefix, and the SHA-256 hex hash that's actually
        /// stored. SHA-256 (fast, deterministic) is the correct choice here — unlike a password
        /// (low entropy, needs slow hashing like PBKDF2/bcrypt to resist offline guessing), this
        /// key already has ~256 bits of randomness, so a direct indexed hash lookup is both
        /// sufficient and necessary (a slow hash would make every single API request pay a
        /// deliberate CPU cost for no security benefit against a secret this random).</summary>
        private static (string Plaintext, string Prefix, string Hash) GenerateKey()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            var urlSafe = Convert.ToBase64String(randomBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var plaintext = KeyPrefixTag + urlSafe;
            var prefix = plaintext[..KeyPrefixDisplayLength];
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
            return (plaintext, prefix, hash);
        }

        private async Task<string> GenerateUniqueServiceAccountEmailAsync(string name)
        {
            var slug = new string([.. name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')])
                .Trim('-');
            if (slug.Length == 0)
            {
                slug = "service-account";
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}@{EmailDomain}";
                var taken = await _db.Users.AnyAsync(u => u.Email.ToLower() == candidate);
                if (!taken)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Could not generate a unique service account email after 5 attempts.");
        }
    }
}

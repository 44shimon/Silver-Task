using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    public interface IAuthService
    {
        Task<(User User, string Token, DateTime ExpiresAtUtc)?> LoginAsync(string email, string password);
    }

    public class AuthService(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService,
        ISystemSettingsService systemSettings,
        ILogger<AuthService> logger) : IAuthService
    {
        private readonly AppDbContext _db = db;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly ILogger<AuthService> _logger = logger;

        public async Task<(User User, string Token, DateTime ExpiresAtUtc)?> LoginAsync(string email, string password)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user is null || !user.IsActive)
            {
                _logger.LogWarning("Failed login attempt for {Email}: account not found or inactive", normalizedEmail);
                return null;
            }

            // Phase 62 — service accounts authenticate only via API key, never a password, even
            // though PasswordHash is technically set (a random, never-disclosed value — see
            // ApiKeyService.CreateServiceAccountAsync). Defense in depth: this guard makes that
            // true regardless of whether anything upstream ever tries to attempt it. Same
            // generic outcome as every other rejection below — never reveals *why*.
            if (user.IsServiceAccount)
            {
                _logger.LogWarning("Failed login attempt for {Email}: account is a service account (password login not permitted)", normalizedEmail);
                return null;
            }

            // Deliberately the same generic "failed login" outcome as a wrong password below —
            // callers never learn whether an account exists or is locked out, only that this
            // attempt didn't work. The lockout itself is still logged distinctly server-side.
            if (user.LockedOutUntil is { } lockedOutUntil && lockedOutUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("Failed login attempt for {Email}: account is locked out until {LockedOutUntil}", normalizedEmail, lockedOutUntil);
                return null;
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                await RegisterFailedAttemptAsync(user, normalizedEmail);
                return null;
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
            }

            user.FailedLoginAttempts = 0;
            user.LockedOutUntil = null;
            await _db.SaveChangesAsync();

            var sessionMinutes = await _systemSettings.GetIntAsync(SystemSettingKeys.SessionTimeoutMinutes);
            var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(user, sessionMinutes);
            return (user, token, expiresAtUtc);
        }

        private async Task RegisterFailedAttemptAsync(User user, string normalizedEmail)
        {
            user.FailedLoginAttempts++;

            var maxAttempts = await _systemSettings.GetIntAsync(SystemSettingKeys.MaxFailedLoginAttempts);
            if (user.FailedLoginAttempts >= maxAttempts)
            {
                var lockoutMinutes = await _systemSettings.GetIntAsync(SystemSettingKeys.AccountLockoutDurationMinutes);
                user.LockedOutUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                _logger.LogWarning("Account {Email} locked out for {LockoutMinutes} minutes after {Attempts} failed login attempts",
                    normalizedEmail, lockoutMinutes, user.FailedLoginAttempts);
            }
            else
            {
                _logger.LogWarning("Failed login attempt for {Email}: incorrect password ({Attempts}/{MaxAttempts})",
                    normalizedEmail, user.FailedLoginAttempts, maxAttempts);
            }

            await _db.SaveChangesAsync();
        }
    }
}

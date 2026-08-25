using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IUserService
    {
        Task<IReadOnlyList<User>> GetAllAsync();

        Task<User> GetByIdAsync(Guid id);

        Task<bool> AnyUsersExistAsync();

        /// <param name="isBootstrap">
        /// When true (there are no users yet), the account is always created as Administrator,
        /// regardless of the requested role, so the system has an initial admin.
        /// </param>
        Task<User> CreateAsync(CreateUserRequest request, bool isBootstrap);

        Task<User> UpdateAsync(Guid id, UpdateUserRequest request, Guid callerId);

        /// <summary>Admin-only password reset — the existing IPasswordHasher is reused, so this
        /// stores a hash exactly like a normal signup/login rehash, never the plaintext.</summary>
        Task ResetPasswordAsync(Guid id, string newPassword);

        /// <summary>Self-service profile edit — Name/Email only. Unlike UpdateAsync (the
        /// Administrator-only full edit), there is no Role/IsActive parameter at all here, so
        /// there is nothing for a caller to elevate even if this method is reachable by every
        /// authenticated user regardless of role.</summary>
        Task<User> UpdateProfileAsync(Guid id, UpdateProfileRequest request);

        /// <summary>Self-service password change — unlike the admin reset, this requires proving
        /// knowledge of the current password first.</summary>
        Task ChangePasswordAsync(Guid id, ChangePasswordRequest request);

        /// <summary>Soft delete — see User.IsDeleted. Never destroys the user's tasks, comments,
        /// activity history, or project ownership/membership; those FKs keep pointing at this
        /// row.</summary>
        Task DeleteAsync(Guid id, Guid callerId);
    }

    public class UserService(AppDbContext db, IPasswordHasher<User> passwordHasher, ISystemSettingsService systemSettings) : IUserService
    {
        private readonly AppDbContext _db = db;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly ISystemSettingsService _systemSettings = systemSettings;

        // Deleted users never appear in the normal admin list — same "gone from active lists,
        // preserved in historical records" split Phase 25 already established for custom fields.
        public async Task<IReadOnlyList<User>> GetAllAsync() =>
            await _db.Users.Where(u => !u.IsDeleted).OrderBy(u => u.Name).ToListAsync();

        public async Task<User> GetByIdAsync(Guid id) =>
            await _db.Users.FindAsync(id) ?? throw new NotFoundException($"User '{id}' was not found.");

        public Task<bool> AnyUsersExistAsync() => _db.Users.AnyAsync();

        public async Task<User> CreateAsync(CreateUserRequest request, bool isBootstrap)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var emailTaken = await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
            if (emailTaken)
            {
                throw new ConflictException($"A user with email '{request.Email}' already exists.");
            }

            await ValidatePasswordAsync(request.Password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Email = normalizedEmail,
                PasswordHash = string.Empty,
                Role = isBootstrap ? UserRole.Administrator : request.Role,
                IsActive = true
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return user;
        }

        public async Task<User> UpdateAsync(Guid id, UpdateUserRequest request, Guid callerId)
        {
            var user = await GetByIdAsync(id);

            // An admin editing their own account can't disable themselves or drop their own
            // Administrator role — either would lock them out immediately, with no one left in
            // the request to undo it (same "can't cut off your own access" reasoning as the
            // project owner being unremovable from ProjectService.RemoveMemberAsync).
            if (id == callerId)
            {
                if (!request.IsActive)
                {
                    throw new ConflictException("You cannot disable your own account.");
                }
                if (user.Role == UserRole.Administrator && request.Role != UserRole.Administrator)
                {
                    throw new ConflictException("You cannot remove your own Administrator role.");
                }
            }

            user.Name = request.Name.Trim();
            user.Role = request.Role;
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return user;
        }

        public async Task ResetPasswordAsync(Guid id, string newPassword)
        {
            await ValidatePasswordAsync(newPassword);

            var user = await GetByIdAsync(id);
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<User> UpdateProfileAsync(Guid id, UpdateProfileRequest request)
        {
            var user = await GetByIdAsync(id);

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (normalizedEmail != user.Email)
            {
                var emailTaken = await _db.Users.AnyAsync(u => u.Id != id && u.Email.ToLower() == normalizedEmail);
                if (emailTaken)
                {
                    throw new ConflictException($"A user with email '{request.Email}' already exists.");
                }
            }

            user.Name = request.Name.Trim();
            user.Email = normalizedEmail;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return user;
        }

        public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request)
        {
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                throw new ValidationException("New password and confirmation do not match.");
            }

            var user = await GetByIdAsync(id);
            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                throw new ValidationException("Current password is incorrect.");
            }

            await ValidatePasswordAsync(request.NewPassword);

            user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id, Guid callerId)
        {
            var user = await GetByIdAsync(id);

            // Same "can't cut off your own access" reasoning as UpdateAsync's self-lockout
            // guard — deleting your own currently-authenticated account is never allowed,
            // regardless of how many other administrators exist.
            if (id == callerId)
            {
                throw new ConflictException("You cannot delete your own account.");
            }

            if (user.Role == UserRole.Administrator)
            {
                var otherActiveAdmins = await _db.Users.CountAsync(u =>
                    u.Id != id && u.Role == UserRole.Administrator && u.IsActive && !u.IsDeleted);
                if (otherActiveAdmins == 0)
                {
                    throw new ConflictException("Cannot delete the last active administrator.");
                }
            }

            // Soft delete only — no cascading removal of tasks/comments/activity/projects. Also
            // deactivates (blocking login through the existing IsActive check in
            // AuthService.LoginAsync) rather than introducing a second, parallel "can this user
            // log in" check.
            user.IsDeleted = true;
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedByUserId = callerId;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        /// <summary>Shared by every password-setting path (signup, admin reset, self-service
        /// change) so the configurable Security.MinPasswordLength / RequirePasswordComplexity
        /// settings are enforced consistently everywhere a password can be set, not just at
        /// signup.</summary>
        private async Task ValidatePasswordAsync(string password)
        {
            var minLength = await _systemSettings.GetIntAsync(SystemSettingKeys.MinPasswordLength);
            if (password.Length < minLength)
            {
                throw new ValidationException($"Password must be at least {minLength} characters long.");
            }

            var requireComplexity = await _systemSettings.GetBoolAsync(SystemSettingKeys.RequirePasswordComplexity);
            if (requireComplexity)
            {
                var hasUpper = password.Any(char.IsUpper);
                var hasLower = password.Any(char.IsLower);
                var hasDigit = password.Any(char.IsDigit);
                var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));
                if (!(hasUpper && hasLower && hasDigit && hasSymbol))
                {
                    throw new ValidationException("Password must contain at least one uppercase letter, one lowercase letter, one number, and one symbol.");
                }
            }
        }
    }
}

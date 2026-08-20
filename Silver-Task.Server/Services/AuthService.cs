using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        ILogger<AuthService> logger) : IAuthService
    {
        private readonly AppDbContext _db = db;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
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

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Failed login attempt for {Email}: incorrect password", normalizedEmail);
                return null;
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                await _db.SaveChangesAsync();
            }

            var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(user);
            return (user, token, expiresAtUtc);
        }
    }
}

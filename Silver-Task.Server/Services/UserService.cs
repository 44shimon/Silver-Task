using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
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

        Task<User> UpdateAsync(Guid id, UpdateUserRequest request);
    }

    public class UserService(AppDbContext db, IPasswordHasher<User> passwordHasher) : IUserService
    {
        private readonly AppDbContext _db = db;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;

        public async Task<IReadOnlyList<User>> GetAllAsync() =>
            await _db.Users.OrderBy(u => u.Name).ToListAsync();

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

        public async Task<User> UpdateAsync(Guid id, UpdateUserRequest request)
        {
            var user = await GetByIdAsync(id);

            user.Name = request.Name.Trim();
            user.Role = request.Role;
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return user;
        }
    }
}

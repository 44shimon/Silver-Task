using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Admin;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IAdminService
    {
        Task<AdminStatsDto> GetStatsAsync();

        /// <summary>Counts shown in the delete-user confirmation dialog — see UserDeletionImpactDto.</summary>
        Task<UserDeletionImpactDto> GetUserDeletionImpactAsync(Guid userId);
    }

    /// <summary>
    /// System-wide statistics for the Admin Dashboard. Unlike every other service, this
    /// deliberately queries across all users' data with no participation/ownership scoping —
    /// only reachable via AdminController's controller-level Administrator gate.
    /// </summary>
    public class AdminService(AppDbContext db) : IAdminService
    {
        private readonly AppDbContext _db = db;

        public async Task<AdminStatsDto> GetStatsAsync()
        {
            var totalUsers = await _db.Users.CountAsync();
            var activeUsers = await _db.Users.CountAsync(u => u.IsActive);
            var totalProjects = await _db.Projects.CountAsync(p => !p.IsArchived);

            var statusCounts = await _db.Tasks
                .GroupBy(t => t.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var totalTasks = statusCounts.Values.Sum();
            var completedTasks = statusCounts.GetValueOrDefault(TaskItemStatus.Complete);
            var cancelledTasks = statusCounts.GetValueOrDefault(TaskItemStatus.Cancelled);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var completedToday = await _db.Tasks.CountAsync(t =>
                t.Status == TaskItemStatus.Complete && t.CompletedAt != null &&
                t.CompletedAt >= DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) &&
                t.CompletedAt < DateTime.SpecifyKind(today.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            var overdueTasks = await _db.Tasks.CountAsync(t =>
                t.DueDate != null && t.DueDate < today && t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled);

            return new AdminStatsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                TotalProjects = totalProjects,
                TotalTasks = totalTasks,
                OpenTasks = totalTasks - completedTasks - cancelledTasks,
                CompletedTasks = completedTasks,
                CompletedToday = completedToday,
                OverdueTasks = overdueTasks
            };
        }

        public async Task<UserDeletionImpactDto> GetUserDeletionImpactAsync(Guid userId)
        {
            var user = await _db.Users.FindAsync(userId) ?? throw new NotFoundException($"User '{userId}' was not found.");

            var assignedTaskCount = await _db.Tasks.CountAsync(t => t.AssignedToUserId == userId);
            var projectCount = await _db.Projects
                .CountAsync(p => p.OwnerId == userId || p.Members.Any(m => m.UserId == userId));
            var commentCount = await _db.TaskComments.CountAsync(c => c.UserId == userId);
            var activityCount = await _db.TaskActivities.CountAsync(a => a.UserId == userId);

            return new UserDeletionImpactDto
            {
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.ToString(),
                AssignedTaskCount = assignedTaskCount,
                ProjectCount = projectCount,
                CommentCount = commentCount,
                ActivityCount = activityCount
            };
        }
    }
}

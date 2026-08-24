using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Admin;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IAdminService
    {
        Task<AdminStatsDto> GetStatsAsync();
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

            return new AdminStatsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                TotalProjects = totalProjects,
                TotalTasks = totalTasks,
                OpenTasks = totalTasks - completedTasks - cancelledTasks,
                CompletedTasks = completedTasks
            };
        }
    }
}

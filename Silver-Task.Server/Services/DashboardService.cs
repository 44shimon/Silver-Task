using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Dashboard;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IDashboardService
    {
        /// <param name="upcomingRange">"today"|"tomorrow"|"7days"|"30days", default 7days.</param>
        /// <param name="statsRange">"today"|"week"|"month", default week.</param>
        Task<DashboardDto> GetDashboardAsync(Guid callerId, UserRole callerRole, string? upcomingRange, string? statsRange);

        /// <summary>Null if the caller doesn't manage (Manager-tier or owner) any project — see
        /// its own doc comment on why this is a hard requirement, not just a UI hint.</summary>
        Task<TeamWorkloadDto?> GetTeamWorkloadAsync(Guid callerId, UserRole callerRole);

        /// <param name="mineOnly">True narrows the same feed to actions the caller themselves
        /// performed ("My Activity" — spec's own distinct widget from the general feed).</param>
        Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivityAsync(Guid callerId, UserRole callerRole, bool mineOnly, int limit);
    }

    /// <summary>
    /// Phase 37 — every query here is scoped by the *same* ownership/membership predicate
    /// TaskService.GetAssignedToUserAsync/SearchAsync already established (Administrator sees
    /// everything; everyone else only projects they own or are a member of) — never "load
    /// everything, filter in React" (the spec's own explicit "Dashboard Security" rule). Dates are
    /// always resolved in the caller's own UserPreference.TimeZone via DashboardDateHelper, not
    /// server/UTC time.
    /// </summary>
    public class DashboardService(AppDbContext db) : IDashboardService
    {
        private readonly AppDbContext _db = db;

        public async Task<DashboardDto> GetDashboardAsync(Guid callerId, UserRole callerRole, string? upcomingRange, string? statsRange)
        {
            var timeZoneId = await _db.UserPreferences.Where(p => p.UserId == callerId).Select(p => p.TimeZone).FirstOrDefaultAsync() ?? "UTC";
            var timeZone = DashboardDateHelper.ResolveTimeZone(timeZoneId);
            var today = DashboardDateHelper.TodayInZone(timeZone);
            var (weekStart, weekEnd) = DashboardDateHelper.WeekRange(today);
            var (upcomingStart, upcomingEnd) = DashboardDateHelper.UpcomingRange(today, upcomingRange);
            var (statsStart, statsEnd) = DashboardDateHelper.StatsRange(today, statsRange);
            var isAdmin = callerRole == UserRole.Administrator;

            // The one predicate every "my assigned tasks" query below shares — mirrors
            // TaskService.GetAssignedToUserAsync exactly (same non-archived + ownership/
            // membership rule), so a task never appears on the dashboard that wouldn't also
            // appear in "My Tasks" or wouldn't be independently reachable by opening its project.
            IQueryable<Models.Entities.TaskItem> MyTasksBase() => _db.Tasks
                .Include(t => t.AssignedTo).Include(t => t.Project)
                .Where(t => t.AssignedToUserId == callerId && !t.Project!.IsArchived &&
                    (isAdmin || t.Project.OwnerId == callerId || t.Project.Members.Any(m => m.UserId == callerId)));

            var openTasks = await MyTasksBase()
                .Where(t => t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled)
                .ToListAsync();

            var taskSummary = new TaskSummaryDto
            {
                MyTasksCount = openTasks.Count,
                DueTodayCount = openTasks.Count(t => t.DueDate == today),
                DueThisWeekCount = openTasks.Count(t => t.DueDate is DateOnly d && d >= weekStart && d <= weekEnd),
                OverdueCount = openTasks.Count(t => t.DueDate is DateOnly d && d < today),
                CompletedThisWeekCount = await MyTasksBase()
                    .Where(t => t.Status == TaskItemStatus.Complete && t.CompletedAt != null &&
                        t.CompletedAt >= DashboardDateHelper.StartOfDayUtc(weekStart, timeZone) &&
                        t.CompletedAt < DashboardDateHelper.StartOfDayUtc(weekEnd.AddDays(1), timeZone))
                    .CountAsync()
            };

            // "Assigned" (in the spec's own example: Assigned 14 = Completed 9 + Remaining 5) means
            // "tasks due during the selected period" — open tasks due in range (still-remaining)
            // plus completed tasks that were due in range. Deliberately NOT "every task currently
            // assigned to me" (that's TaskSummaryDto.MyTasksCount, a different, unscoped number) —
            // this way Assigned == Completed + Remaining always holds by construction, and
            // CompletionRate's denominator matches the same "due in range" population.
            var remainingInStatsRange = openTasks.Where(t => t.DueDate is DateOnly d && d >= statsStart && d <= statsEnd).ToList();
            var completedInStatsRange = await MyTasksBase()
                .Where(t => t.Status == TaskItemStatus.Complete && t.DueDate != null && t.DueDate >= statsStart && t.DueDate <= statsEnd)
                .CountAsync();
            var totalDueInStatsRange = remainingInStatsRange.Count + completedInStatsRange;
            var weekSummary = new WeekSummaryDto
            {
                AssignedCount = totalDueInStatsRange,
                CompletedCount = completedInStatsRange,
                RemainingCount = remainingInStatsRange.Count,
                // A subset of RemainingCount, not the same as TaskSummaryDto.OverdueCount (which
                // is every overdue task regardless of whether its due date falls in this range).
                OverdueCount = remainingInStatsRange.Count(t => t.DueDate < today),
                CompletionRate = totalDueInStatsRange == 0 ? 0 : (double)completedInStatsRange / totalDueInStatsRange
            };

            var overdueTasks = openTasks.Where(t => t.DueDate is DateOnly d && d < today)
                .OrderBy(t => t.DueDate).Take(10).Select(t => t.ToDto()).ToList();
            var dueTodayTasks = openTasks.Where(t => t.DueDate == today)
                .OrderBy(t => t.Title).Take(10).Select(t => t.ToDto()).ToList();
            var upcomingTasks = openTasks.Where(t => t.DueDate is DateOnly d && d >= upcomingStart && d <= upcomingEnd)
                .OrderBy(t => t.DueDate).ThenBy(t => t.Title).Take(20).Select(t => t.ToDto()).ToList();

            var recentlyCompletedTasks = await MyTasksBase()
                .Where(t => t.Status == TaskItemStatus.Complete && t.CompletedAt != null)
                .OrderByDescending(t => t.CompletedAt)
                .Take(10)
                .Select(t => t.ToDto())
                .ToListAsync();

            var priorityBreakdown = openTasks.GroupBy(t => t.Priority)
                .Select(g => new PriorityCountDto { Priority = g.Key.ToString(), Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            var statusBreakdown = openTasks.GroupBy(t => t.Status)
                .Select(g => new StatusCountDto { Status = g.Key.ToString(), Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var myProjects = await GetMyProjectsProgressAsync(callerId, isAdmin);
            var recentActivity = await GetRecentActivityAsync(callerId, callerRole, mineOnly: false, limit: 15);

            return new DashboardDto
            {
                TaskSummary = taskSummary,
                WeekSummary = weekSummary,
                OverdueTasks = overdueTasks,
                DueTodayTasks = dueTodayTasks,
                UpcomingTasks = upcomingTasks,
                RecentlyCompletedTasks = recentlyCompletedTasks,
                MyProjects = myProjects,
                PriorityBreakdown = priorityBreakdown,
                StatusBreakdown = statusBreakdown,
                RecentActivity = recentActivity.ToList()
            };
        }

        private async Task<List<ProjectProgressDto>> GetMyProjectsProgressAsync(Guid callerId, bool isAdmin)
        {
            var projects = await _db.Projects
                .Where(p => !p.IsArchived && (isAdmin || p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId)))
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name, p.IsArchived })
                .ToListAsync();

            if (projects.Count == 0)
            {
                return [];
            }

            var projectIds = projects.Select(p => p.Id).ToList();
            var statusCounts = await _db.Tasks
                .Where(t => projectIds.Contains(t.ProjectId))
                .GroupBy(t => new { t.ProjectId, t.Status })
                .Select(g => new { g.Key.ProjectId, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            var byProject = statusCounts.ToLookup(x => x.ProjectId);

            return projects.Select(p =>
            {
                var rows = byProject[p.Id];
                var completed = rows.Where(r => r.Status == TaskItemStatus.Complete).Sum(r => r.Count);
                var total = rows.Sum(r => r.Count);
                var open = total - completed;
                return new ProjectProgressDto
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    IsArchived = p.IsArchived,
                    OpenCount = open,
                    CompletedCount = completed,
                    PercentComplete = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total)
                };
            }).ToList();
        }

        public async Task<TeamWorkloadDto?> GetTeamWorkloadAsync(Guid callerId, UserRole callerRole)
        {
            var isAdmin = callerRole == UserRole.Administrator;

            // "Managed" == owner, or a project-level Manager role — the same tier
            // ProjectAccessService.EnsureCanManageAsync grants Edit/Delete/membership actions to.
            // Ordinary members/viewers get null here (never silently an empty list that could
            // read as "your team has zero tasks") — see this method's own interface doc comment.
            var managedProjectIds = await _db.Projects
                .Where(p => !p.IsArchived && (isAdmin || p.OwnerId == callerId ||
                    p.Members.Any(m => m.UserId == callerId && m.Role == ProjectRole.Manager)))
                .Select(p => p.Id)
                .ToListAsync();

            if (managedProjectIds.Count == 0)
            {
                return null;
            }

            var entries = await _db.Tasks
                .Where(t => managedProjectIds.Contains(t.ProjectId) && t.AssignedToUserId != null &&
                    t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled)
                .GroupBy(t => new { t.AssignedToUserId, t.AssignedTo!.Name })
                .Select(g => new WorkloadEntryDto
                {
                    UserId = g.Key.AssignedToUserId!.Value,
                    UserName = g.Key.Name,
                    OpenTaskCount = g.Count()
                })
                .OrderByDescending(e => e.OpenTaskCount)
                .ToListAsync();

            return new TeamWorkloadDto { Entries = entries };
        }

        public async Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivityAsync(Guid callerId, UserRole callerRole, bool mineOnly, int limit)
        {
            var isAdmin = callerRole == UserRole.Administrator;

            var query = _db.TaskActivities
                .Include(a => a.Task).ThenInclude(t => t!.Project)
                .Include(a => a.User)
                .Where(a => a.Task != null && !a.Task.Project!.IsArchived &&
                    (isAdmin || a.Task.Project.OwnerId == callerId || a.Task.Project.Members.Any(m => m.UserId == callerId)));

            if (mineOnly)
            {
                query = query.Where(a => a.UserId == callerId);
            }

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(Math.Clamp(limit, 1, 100))
                .Select(a => new ActivityFeedItemDto
                {
                    Id = a.Id,
                    TaskId = a.TaskId,
                    TaskTitle = a.Task!.Title,
                    ProjectId = a.Task.ProjectId,
                    ProjectName = a.Task.Project!.Name,
                    UserName = a.User != null ? a.User.Name : null,
                    Action = a.Action,
                    FieldName = a.FieldName,
                    OldValue = a.OldValue,
                    NewValue = a.NewValue,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();
        }
    }
}

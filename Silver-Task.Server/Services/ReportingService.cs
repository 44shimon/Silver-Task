using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Dashboard;
using Silver_Task.Server.Models.DTOs.Reports;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IReportingService
    {
        Task<TaskSummaryReportDto> GetTaskSummaryAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<TrendReportDto> GetCompletionTrendAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<TrendReportDto> GetCreationTrendAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<OverdueReportDto> GetOverdueReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<TrendReportDto> GetOverdueTrendAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<ProjectProgressReportDto> GetProjectProgressAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        /// <summary>Also serves the spec's "User Completion Report" — same underlying per-user
        /// open/completed/overdue/completion-rate shape, just labeled differently by the frontend
        /// (see this method's own doc comment on why these weren't split into two near-identical
        /// queries).</summary>
        Task<UserWorkloadReportDto> GetWorkloadAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<TaskAgeReportDto> GetTaskAgeAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<OldTaskReportDto> GetOldTasksAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter, int thresholdDays);

        Task<CompletionTimeReportDto> GetCompletionTimeAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<AutomationReportDto> GetAutomationReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        Task<NotificationReportDto> GetMyNotificationReportAsync(Guid callerId);

        Task<FileReportDto> GetFileReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter);

        /// <summary>Administrator-only (enforced by the controller, not here) — the one report
        /// with no per-caller scoping, matching AdminService.GetStatsAsync's own precedent.</summary>
        Task<AdminSystemReportDto> GetAdminSystemReportAsync();

        /// <summary>The minimal Report Builder's single execution path — GroupBy one of
        /// ReportTypes.GroupByFields, Count metric, over the same closed filter set every other
        /// report uses. Returns the same LabeledCountDto shape every other breakdown uses.</summary>
        Task<List<LabeledCountDto>> GetCustomReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter, string groupBy);
    }

    /// <summary>
    /// Phase 38 — every method here follows the exact "Authenticated user -> Authorization scope
    /// -> Database query -> Report" pattern the spec requires: ScopedTasks (below) is the single
    /// place that applies the ownership/membership predicate DashboardService already established
    /// (Administrator sees everything; everyone else only projects they own or are a member of),
    /// and every report query is built on top of it — never "load everything, filter in the
    /// frontend". An explicit ReportFilterRequest.ProjectId for a project the caller can't see
    /// simply yields zero rows (it's ANDed onto the already-scoped predicate), the same
    /// no-data-leak-either-way behavior DashboardController's own doc comment describes.
    ///
    /// No server-side response cache is added (see README/CLAUDE.md's own established "skip
    /// caching for security-sensitive per-user data" precedent from Phase 36) — every report is a
    /// live, indexed, server-aggregated query; TanStack Query's existing client-side cache is the
    /// only caching layer.
    /// </summary>
    public class ReportingService(AppDbContext db) : IReportingService
    {
        private readonly AppDbContext _db = db;

        private IQueryable<TaskItem> ScopedTasks(Guid callerId, bool isAdmin, ReportFilterRequest filter)
        {
            var query = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Where(t => !t.Project!.IsArchived &&
                    (isAdmin || t.Project.OwnerId == callerId || t.Project.Members.Any(m => m.UserId == callerId)));

            if (filter.ProjectId is Guid projectId)
            {
                query = query.Where(t => t.ProjectId == projectId);
            }
            if (filter.UserId is Guid userId)
            {
                query = query.Where(t => t.AssignedToUserId == userId);
            }
            if (filter.Status is TaskItemStatus status)
            {
                query = query.Where(t => t.Status == status);
            }
            if (filter.Priority is TaskPriority priority)
            {
                query = query.Where(t => t.Priority == priority);
            }
            if (filter.LabelId is Guid labelId)
            {
                query = query.Where(t => t.TaskTags.Any(tt => tt.TagId == labelId));
            }
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(t => t.Title.Contains(filter.Search));
            }

            return query;
        }

        private async Task<(TimeZoneInfo TimeZone, DateOnly Today, DateOnly Start, DateOnly End)> ResolveDateContextAsync(
            Guid callerId, string? dateRange, DateOnly? customStart, DateOnly? customEnd)
        {
            var timeZoneId = await _db.UserPreferences.Where(p => p.UserId == callerId).Select(p => p.TimeZone).FirstOrDefaultAsync() ?? "UTC";
            var timeZone = DashboardDateHelper.ResolveTimeZone(timeZoneId);
            var today = DashboardDateHelper.TodayInZone(timeZone);
            var (start, end) = DashboardDateHelper.ReportDateRange(today, dateRange, customStart, customEnd);
            return (timeZone, today, start, end);
        }

        private static string ChooseGranularity(DateOnly start, DateOnly end)
        {
            var days = end.DayNumber - start.DayNumber + 1;
            return days <= 31 ? "day" : days <= 120 ? "week" : "month";
        }

        private static List<(DateOnly Start, DateOnly End, string Label)> BuildBuckets(DateOnly start, DateOnly end, string granularity)
        {
            var buckets = new List<(DateOnly, DateOnly, string)>();
            if (granularity == "day")
            {
                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    buckets.Add((d, d, d.ToString("MMM d")));
                }
            }
            else if (granularity == "week")
            {
                var d = DashboardDateHelper.WeekRange(start).Start;
                while (d <= end)
                {
                    buckets.Add((d, d.AddDays(6), $"Week of {d:MMM d}"));
                    d = d.AddDays(7);
                }
            }
            else
            {
                var d = new DateOnly(start.Year, start.Month, 1);
                while (d <= end)
                {
                    buckets.Add((d, d.AddMonths(1).AddDays(-1), d.ToString("MMM yyyy")));
                    d = d.AddMonths(1);
                }
            }
            return buckets;
        }

        private static TrendReportDto BuildCountTrend(List<DateOnly> localDates, DateOnly start, DateOnly end)
        {
            var granularity = ChooseGranularity(start, end);
            var points = BuildBuckets(start, end, granularity)
                .Select(b => new TrendPointDto
                {
                    Label = b.Label,
                    PeriodStart = b.Start,
                    Count = localDates.Count(d => d >= b.Start && d <= b.End)
                })
                .ToList();
            return new TrendReportDto { Granularity = granularity, Points = points };
        }

        /// <summary>Total/Open/Overdue are scoped to tasks CREATED within the selected date range
        /// (a single, clearly-defined rule applied consistently); Completed is a subset of that
        /// same population currently in the Complete status. See ReportFilterRequest's own doc
        /// comment for the shared filter set this and every other report method applies.</summary>
        public async Task<TaskSummaryReportDto> GetTaskSummaryAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (timeZone, today, start, end) = await ResolveDateContextAsync(callerId, filter.DateRange, filter.StartDate, filter.EndDate);

            var rows = await ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.CreatedAt >= DashboardDateHelper.StartOfDayUtc(start, timeZone) &&
                    t.CreatedAt < DashboardDateHelper.StartOfDayUtc(end.AddDays(1), timeZone))
                .Select(t => new { t.Status, t.Priority, t.DueDate })
                .ToListAsync();

            var total = rows.Count;
            var completed = rows.Count(t => t.Status == TaskItemStatus.Complete);
            var cancelled = rows.Count(t => t.Status == TaskItemStatus.Cancelled);
            var open = total - completed - cancelled;
            var overdue = rows.Count(t => t.DueDate is DateOnly d && d < today &&
                t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled);

            return new TaskSummaryReportDto
            {
                Total = total,
                Completed = completed,
                Open = open,
                Overdue = overdue,
                CompletionRate = total == 0 ? 0 : (double)completed / total,
                ByStatus = rows.GroupBy(t => t.Status)
                    .Select(g => new StatusCountDto { Status = g.Key.ToString(), Count = g.Count() })
                    .OrderByDescending(x => x.Count).ToList(),
                ByPriority = rows.GroupBy(t => t.Priority)
                    .Select(g => new PriorityCountDto { Priority = g.Key.ToString(), Count = g.Count() })
                    .OrderByDescending(x => x.Count).ToList()
            };
        }

        public async Task<TrendReportDto> GetCompletionTrendAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (timeZone, _, start, end) = await ResolveDateContextAsync(callerId, filter.DateRange, filter.StartDate, filter.EndDate);

            var completedUtc = await ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.Status == TaskItemStatus.Complete && t.CompletedAt != null &&
                    t.CompletedAt >= DashboardDateHelper.StartOfDayUtc(start, timeZone) &&
                    t.CompletedAt < DashboardDateHelper.StartOfDayUtc(end.AddDays(1), timeZone))
                .Select(t => t.CompletedAt!.Value)
                .ToListAsync();

            var localDates = completedUtc.Select(d => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(d, timeZone))).ToList();
            return BuildCountTrend(localDates, start, end);
        }

        public async Task<TrendReportDto> GetCreationTrendAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (timeZone, _, start, end) = await ResolveDateContextAsync(callerId, filter.DateRange, filter.StartDate, filter.EndDate);

            var createdUtc = await ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.CreatedAt >= DashboardDateHelper.StartOfDayUtc(start, timeZone) &&
                    t.CreatedAt < DashboardDateHelper.StartOfDayUtc(end.AddDays(1), timeZone))
                .Select(t => t.CreatedAt)
                .ToListAsync();

            var localDates = createdUtc.Select(d => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(d, timeZone))).ToList();
            return BuildCountTrend(localDates, start, end);
        }

        /// <summary>A current-state report (as-of today) — deliberately ignores the DateRange
        /// filter (an "overdue tasks created last month" reading would be more confusing than
        /// useful); Project/User/Status/Priority/Label filters still apply via ScopedTasks.</summary>
        public async Task<OverdueReportDto> GetOverdueReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (_, today, _, _) = await ResolveDateContextAsync(callerId, null, null, null);

            var query = ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.DueDate != null && t.DueDate < today &&
                    t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled);

            var totalCount = await query.CountAsync();
            var page = Math.Max(filter.Page, 1);
            var pageSize = Math.Clamp(filter.PageSize, 1, 200);

            var raw = await query
                .OrderBy(t => t.DueDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.ProjectId,
                    ProjectName = t.Project!.Name,
                    AssigneeName = t.AssignedTo != null ? t.AssignedTo.Name : null,
                    DueDate = t.DueDate!.Value,
                    t.Priority
                })
                .ToListAsync();

            var items = raw.Select(r => new OverdueTaskRowDto
            {
                TaskId = r.Id,
                TaskTitle = r.Title,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                AssigneeName = r.AssigneeName,
                DueDate = r.DueDate,
                DaysOverdue = today.DayNumber - r.DueDate.DayNumber,
                Priority = r.Priority.ToString()
            }).ToList();

            return new OverdueReportDto { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
        }

        public async Task<TrendReportDto> GetOverdueTrendAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (timeZone, _, start, end) = await ResolveDateContextAsync(callerId, filter.DateRange, filter.StartDate, filter.EndDate);

            var rows = await ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.DueDate != null && t.Status != TaskItemStatus.Cancelled)
                .Select(t => new { DueDate = t.DueDate!.Value, t.CompletedAt })
                .ToListAsync();

            var granularity = ChooseGranularity(start, end);
            var points = BuildBuckets(start, end, granularity).Select(b =>
            {
                var bucketEndUtc = DashboardDateHelper.StartOfDayUtc(b.End.AddDays(1), timeZone);
                var count = rows.Count(r => r.DueDate <= b.End && (r.CompletedAt == null || r.CompletedAt >= bucketEndUtc));
                return new TrendPointDto { Label = b.Label, PeriodStart = b.Start, Count = count };
            }).ToList();

            return new TrendReportDto { Granularity = granularity, Points = points };
        }

        public async Task<ProjectProgressReportDto> GetProjectProgressAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (timeZone, today, start, end) = await ResolveDateContextAsync(callerId, filter.DateRange, filter.StartDate, filter.EndDate);

            var projectsQuery = _db.Projects
                .Where(p => !p.IsArchived && (isAdmin || p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId)));
            if (filter.ProjectId is Guid filterProjectId)
            {
                projectsQuery = projectsQuery.Where(p => p.Id == filterProjectId);
            }

            var projects = await projectsQuery.Select(p => new { p.Id, p.Name }).ToListAsync();
            if (projects.Count == 0)
            {
                return new ProjectProgressReportDto { Projects = [] };
            }

            var projectIds = projects.Select(p => p.Id).ToList();
            var taskRows = await _db.Tasks
                .Where(t => projectIds.Contains(t.ProjectId))
                .Select(t => new { t.ProjectId, t.Status, t.DueDate })
                .ToListAsync();
            var byProject = taskRows.ToLookup(t => t.ProjectId);

            var rows = projects.Select(p =>
            {
                var projectTasks = byProject[p.Id];
                var total = projectTasks.Count();
                var completed = projectTasks.Count(r => r.Status == TaskItemStatus.Complete);
                var overdue = projectTasks.Count(r => r.DueDate is DateOnly d && d < today &&
                    r.Status != TaskItemStatus.Complete && r.Status != TaskItemStatus.Cancelled);
                var dueSoon = projectTasks.Count(r => r.DueDate is DateOnly d && d >= today && d <= today.AddDays(3) &&
                    r.Status != TaskItemStatus.Complete && r.Status != TaskItemStatus.Cancelled);
                var health = overdue > 0 ? "Overdue" : dueSoon > 0 ? "AtRisk" : "Healthy";

                return new ProjectProgressReportRowDto
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    TaskCount = total,
                    CompletedCount = completed,
                    PercentComplete = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total),
                    OverdueCount = overdue,
                    Health = health
                };
            }).ToList();

            TrendReportDto? trend = null;
            if (projects.Count == 1)
            {
                trend = await GetProjectCompletionTrendAsync(projects[0].Id, timeZone, start, end);
            }

            return new ProjectProgressReportDto { Projects = rows, CompletionTrend = trend };
        }

        /// <summary>Reconstructs "% complete as of date D" live from CreatedAt/CompletedAt at each
        /// sampled bucket boundary — (tasks created by D that are also completed by D) / (tasks
        /// created by D) — rather than a stored ProjectDailySnapshot/TaskDailySnapshot. This is a
        /// genuine historical reconstruction, not a fabrication: every point uses only real,
        /// already-persisted timestamps. See IReportingService's own doc comment on why no
        /// snapshot entity or background job was introduced this phase.</summary>
        private async Task<TrendReportDto> GetProjectCompletionTrendAsync(Guid projectId, TimeZoneInfo timeZone, DateOnly start, DateOnly end)
        {
            var rows = await _db.Tasks
                .Where(t => t.ProjectId == projectId)
                .Select(t => new { t.CreatedAt, t.CompletedAt })
                .ToListAsync();

            var granularity = ChooseGranularity(start, end);
            var points = BuildBuckets(start, end, granularity).Select(b =>
            {
                var bucketEndUtc = DashboardDateHelper.StartOfDayUtc(b.End.AddDays(1), timeZone);
                var createdByEnd = rows.Count(r => r.CreatedAt < bucketEndUtc);
                var completedByEnd = rows.Count(r => r.CompletedAt != null && r.CompletedAt < bucketEndUtc);
                var percent = createdByEnd == 0 ? 0 : (int)Math.Round(completedByEnd * 100.0 / createdByEnd);
                // Count doubles as "percent complete" for this one trend — a project's completion
                // trend has no other meaningful integer to report per bucket.
                return new TrendPointDto { Label = b.Label, PeriodStart = b.Start, Count = percent };
            }).ToList();

            return new TrendReportDto { Granularity = granularity, Points = points };
        }

        /// <summary>Current-state (not date-range-scoped) per-assignee open/completed/overdue —
        /// scoped only by ScopedTasks' project ownership/membership predicate. Deliberately not
        /// restricted to "projects the caller manages" the way DashboardService.GetTeamWorkloadAsync
        /// is: a report only ever reveals assignee counts for tasks the caller could already see
        /// individually (their own accessible projects' task lists already show assignees), so the
        /// stricter Dashboard-widget-level gate isn't a security requirement here — see the
        /// Reports spec's own "Team Reports" section, which frames this as a broader visibility
        /// feature than the Dashboard's "my direct team" widget.</summary>
        public async Task<UserWorkloadReportDto> GetWorkloadAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (_, today, _, _) = await ResolveDateContextAsync(callerId, null, null, null);

            var rows = await ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.AssignedToUserId != null)
                .Select(t => new { t.AssignedToUserId, AssignedToName = t.AssignedTo!.Name, t.Status, t.DueDate })
                .ToListAsync();

            var entries = rows.GroupBy(r => new { r.AssignedToUserId, r.AssignedToName })
                .Select(g =>
                {
                    var open = g.Count(r => r.Status != TaskItemStatus.Complete && r.Status != TaskItemStatus.Cancelled);
                    var completed = g.Count(r => r.Status == TaskItemStatus.Complete);
                    var overdue = g.Count(r => r.DueDate is DateOnly d && d < today &&
                        r.Status != TaskItemStatus.Complete && r.Status != TaskItemStatus.Cancelled);
                    var total = open + completed;
                    return new UserWorkloadRowDto
                    {
                        UserId = g.Key.AssignedToUserId!.Value,
                        UserName = g.Key.AssignedToName,
                        OpenCount = open,
                        CompletedCount = completed,
                        OverdueCount = overdue,
                        CompletionRate = total == 0 ? 0 : (double)completed / total
                    };
                })
                .OrderByDescending(e => e.OpenCount)
                .ToList();

            return new UserWorkloadReportDto { Entries = entries };
        }

        /// <summary>Open tasks only, bucketed by age-since-CreatedAt — purely read-only, never
        /// writes to task data (per the spec's explicit instruction).</summary>
        public async Task<TaskAgeReportDto> GetTaskAgeAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;

            var createdDates = await ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled)
                .Select(t => t.CreatedAt)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var bucketLabels = new[] { "0-7", "8-14", "15-30", "31-60", "60+" };
            var counts = new int[5];
            foreach (var createdAt in createdDates)
            {
                var ageDays = (now - createdAt).TotalDays;
                var idx = ageDays <= 7 ? 0 : ageDays <= 14 ? 1 : ageDays <= 30 ? 2 : ageDays <= 60 ? 3 : 4;
                counts[idx]++;
            }

            return new TaskAgeReportDto
            {
                Buckets = bucketLabels.Select((b, i) => new TaskAgeBucketDto { Bucket = b, Count = counts[i] }).ToList(),
                TotalOpen = createdDates.Count
            };
        }

        public async Task<OldTaskReportDto> GetOldTasksAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter, int thresholdDays)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(thresholdDays, 1));

            var query = ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled && t.CreatedAt < cutoff);

            var totalCount = await query.CountAsync();
            var page = Math.Max(filter.Page, 1);
            var pageSize = Math.Clamp(filter.PageSize, 1, 200);

            var raw = await query
                .OrderBy(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.ProjectId,
                    ProjectName = t.Project!.Name,
                    AssigneeName = t.AssignedTo != null ? t.AssignedTo.Name : null,
                    t.CreatedAt
                })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var items = raw.Select(r => new OldTaskRowDto
            {
                TaskId = r.Id,
                TaskTitle = r.Title,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                AssigneeName = r.AssigneeName,
                CreatedAt = r.CreatedAt,
                AgeDays = (int)(now - r.CreatedAt).TotalDays
            }).ToList();

            return new OldTaskReportDto { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize, ThresholdDays = thresholdDays };
        }

        /// <summary>Created-&gt;Completed only — see CompletionTimeReportDto's own doc comment on
        /// why Cycle Time (Started-&gt;Completed) is omitted entirely rather than guessed.</summary>
        public async Task<CompletionTimeReportDto> GetCompletionTimeAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (timeZone, _, start, end) = await ResolveDateContextAsync(callerId, filter.DateRange, filter.StartDate, filter.EndDate);

            var rows = await ScopedTasks(callerId, isAdmin, filter)
                .Where(t => t.Status == TaskItemStatus.Complete && t.CompletedAt != null &&
                    t.CompletedAt >= DashboardDateHelper.StartOfDayUtc(start, timeZone) &&
                    t.CompletedAt < DashboardDateHelper.StartOfDayUtc(end.AddDays(1), timeZone))
                .Select(t => new { t.CreatedAt, CompletedAt = t.CompletedAt!.Value, t.Priority })
                .ToListAsync();

            static double? Avg(List<double> days) => days.Count == 0 ? null : days.Average();

            var allDays = rows.Select(r => (r.CompletedAt - r.CreatedAt).TotalDays).ToList();
            var byPriority = rows.GroupBy(r => r.Priority)
                .Select(g =>
                {
                    var days = g.Select(r => (r.CompletedAt - r.CreatedAt).TotalDays).ToList();
                    return new PriorityCompletionTimeDto { Priority = g.Key.ToString(), AverageDays = Avg(days), SampleSize = days.Count };
                })
                .OrderBy(x => x.Priority)
                .ToList();

            return new CompletionTimeReportDto { AverageDays = Avg(allDays), SampleSize = allDays.Count, ByPriority = byPriority };
        }

        public async Task<AutomationReportDto> GetAutomationReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var accessibleProjectIds = await _db.Projects
                .Where(p => !p.IsArchived && (isAdmin || p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId)))
                .Select(p => p.Id)
                .ToListAsync();

            var query = _db.Automations.Where(a => !a.IsDeleted &&
                ((a.ProjectId != null && accessibleProjectIds.Contains(a.ProjectId.Value)) || (a.ProjectId == null && isAdmin)));
            if (filter.ProjectId is Guid projectId)
            {
                query = query.Where(a => a.ProjectId == projectId);
            }

            var automations = await query
                .Select(a => new { a.Id, a.Name, a.TriggerType, a.IsActive, a.RunCount, a.LastRunAt })
                .ToListAsync();
            if (automations.Count == 0)
            {
                return new AutomationReportDto { Automations = [] };
            }

            var automationIds = automations.Select(a => a.Id).ToList();
            var executionCounts = await _db.AutomationExecutions
                .Where(e => automationIds.Contains(e.AutomationId))
                .GroupBy(e => new { e.AutomationId, e.Status })
                .Select(g => new { g.Key.AutomationId, g.Key.Status, Count = g.Count() })
                .ToListAsync();
            var byAutomation = executionCounts.ToLookup(x => x.AutomationId);

            var rows = automations.Select(a =>
            {
                var executions = byAutomation[a.Id];
                return new AutomationReportRowDto
                {
                    AutomationId = a.Id,
                    Name = a.Name,
                    TriggerType = a.TriggerType.ToString(),
                    IsActive = a.IsActive,
                    RunCount = a.RunCount,
                    SuccessCount = executions.Where(e => e.Status == AutomationExecutionStatus.Success).Sum(e => e.Count),
                    FailedCount = executions.Where(e => e.Status == AutomationExecutionStatus.Failed).Sum(e => e.Count),
                    LastRunAt = a.LastRunAt
                };
            }).ToList();

            return new AutomationReportDto { Automations = rows };
        }

        /// <summary>Always scoped to the caller's OWN notifications (UserId == callerId) — never a
        /// query-parameter-selectable user, so this can never be used to read another user's
        /// notification metrics regardless of role. Aggregate counts only; Title/Message/Metadata
        /// are never selected, per the spec's own "avoid exposing private notification content"
        /// instruction.</summary>
        public async Task<NotificationReportDto> GetMyNotificationReportAsync(Guid callerId)
        {
            var rows = await _db.Notifications
                .Where(n => n.UserId == callerId)
                .Select(n => new { n.Type, n.Priority, n.IsRead })
                .ToListAsync();

            return new NotificationReportDto
            {
                TotalCount = rows.Count,
                UnreadCount = rows.Count(r => !r.IsRead),
                ByType = rows.GroupBy(r => r.Type)
                    .Select(g => new LabeledCountDto { Label = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).ToList(),
                ByPriority = rows.GroupBy(r => r.Priority)
                    .Select(g => new LabeledCountDto { Label = g.Key.ToString(), Count = g.Count() })
                    .OrderByDescending(x => x.Count).ToList()
            };
        }

        /// <summary>File metadata only, never contents — every attachment is resolved back to its
        /// owning project (directly, or via its Task/Comment) and checked against the same
        /// accessible-project set every other report uses, so a file from a project the caller
        /// can't see never contributes to these counts.</summary>
        public async Task<FileReportDto> GetFileReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var (timeZone, _, start, end) = await ResolveDateContextAsync(callerId, filter.DateRange, filter.StartDate, filter.EndDate);

            var accessibleProjectIds = await _db.Projects
                .Where(p => !p.IsArchived && (isAdmin || p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId)))
                .Select(p => p.Id)
                .ToListAsync();
            if (filter.ProjectId is Guid filterProjectId)
            {
                accessibleProjectIds = accessibleProjectIds.Where(id => id == filterProjectId).ToList();
            }

            var query = _db.Attachments
                .Include(a => a.Category)
                .Where(a => !a.IsDeleted &&
                    ((a.ProjectId != null && accessibleProjectIds.Contains(a.ProjectId.Value)) ||
                     (a.TaskId != null && accessibleProjectIds.Contains(a.Task!.ProjectId)) ||
                     (a.CommentId != null && accessibleProjectIds.Contains(a.Comment!.Task!.ProjectId))));

            var rows = await query
                .Select(a => new { a.FileSize, a.CreatedAt, CategoryName = a.Category != null ? a.Category.Name : "Uncategorized" })
                .ToListAsync();

            var startUtc = DashboardDateHelper.StartOfDayUtc(start, timeZone);
            var endUtc = DashboardDateHelper.StartOfDayUtc(end.AddDays(1), timeZone);

            return new FileReportDto
            {
                TotalFiles = rows.Count,
                TotalSizeBytes = rows.Sum(r => r.FileSize),
                FilesInRange = rows.Count(r => r.CreatedAt >= startUtc && r.CreatedAt < endUtc),
                ByCategory = rows.GroupBy(r => r.CategoryName)
                    .Select(g => new LabeledCountDto { Label = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).ToList()
            };
        }

        public async Task<AdminSystemReportDto> GetAdminSystemReportAsync()
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

            // System-wide, server-UTC boundary — deliberately not per-user-timezone, same
            // established exception as AdminService.GetStatsAsync (see its own doc comment).
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var overdueTasks = await _db.Tasks.CountAsync(t =>
                t.DueDate != null && t.DueDate < today && t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled);

            var activeAutomations = await _db.Automations.CountAsync(a => a.IsActive && !a.IsDeleted);
            var totalNotifications = await _db.Notifications.CountAsync();
            var totalFiles = await _db.Attachments.CountAsync(a => !a.IsDeleted);

            return new AdminSystemReportDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                TotalProjects = totalProjects,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                OverdueTasks = overdueTasks,
                ActiveAutomations = activeAutomations,
                TotalNotifications = totalNotifications,
                TotalFiles = totalFiles
            };
        }

        public async Task<List<LabeledCountDto>> GetCustomReportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter, string groupBy)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var rows = await ScopedTasks(callerId, isAdmin, filter)
                .Select(t => new { t.ProjectId, ProjectName = t.Project!.Name, t.Status, t.Priority, AssigneeName = t.AssignedTo != null ? t.AssignedTo.Name : "Unassigned" })
                .ToListAsync();

            IEnumerable<IGrouping<string, object>> grouped = groupBy switch
            {
                "Status" => rows.GroupBy(r => r.Status.ToString(), r => (object)r),
                "Priority" => rows.GroupBy(r => r.Priority.ToString(), r => (object)r),
                "Assignee" => rows.GroupBy(r => r.AssigneeName, r => (object)r),
                _ => rows.GroupBy(r => r.ProjectName, r => (object)r)
            };

            return grouped
                .Select(g => new LabeledCountDto { Label = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
        }
    }
}

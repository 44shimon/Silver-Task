using Silver_Task.Server.Models.DTOs.Tasks;

namespace Silver_Task.Server.Models.DTOs.Dashboard
{
    /// <summary>
    /// The single aggregated payload behind GET /api/dashboard — groups everything derived from
    /// the caller's own Tasks/Projects data (the spec's own suggested response shape) so the
    /// dashboard's task-summary/overdue/due-today/upcoming/recently-completed/my-projects/
    /// priority/status/recent-activity widgets share one query round trip instead of nine
    /// separate ones, while genuinely independent concerns (Notifications, Recent Files, Team
    /// Workload, Admin Overview) keep their own existing/separate endpoints so a failure here
    /// doesn't take those widgets down too — see DashboardController's own doc comment.
    /// </summary>
    public class DashboardDto
    {
        public required TaskSummaryDto TaskSummary { get; set; }

        public required WeekSummaryDto WeekSummary { get; set; }

        public required List<TaskDto> OverdueTasks { get; set; }

        public required List<TaskDto> DueTodayTasks { get; set; }

        public required List<TaskDto> UpcomingTasks { get; set; }

        public required List<TaskDto> RecentlyCompletedTasks { get; set; }

        public required List<ProjectProgressDto> MyProjects { get; set; }

        public required List<PriorityCountDto> PriorityBreakdown { get; set; }

        public required List<StatusCountDto> StatusBreakdown { get; set; }

        public required List<ActivityFeedItemDto> RecentActivity { get; set; }
    }

    public class TaskSummaryDto
    {
        public int MyTasksCount { get; set; }

        public int DueTodayCount { get; set; }

        public int DueThisWeekCount { get; set; }

        public int OverdueCount { get; set; }

        public int CompletedThisWeekCount { get; set; }
    }

    /// <summary>"Completed / Due" within the selected statistics range (default: this week) —
    /// see DashboardService's own doc comment for the exact CompletionRate definition.</summary>
    public class WeekSummaryDto
    {
        public int AssignedCount { get; set; }

        public int CompletedCount { get; set; }

        public int RemainingCount { get; set; }

        public int OverdueCount { get; set; }

        /// <summary>Completed tasks / tasks due during the selected period, 0 when nothing was
        /// due (never a division-by-zero NaN). 0-1, not a percentage — the frontend formats it.</summary>
        public double CompletionRate { get; set; }
    }

    public class ProjectProgressDto
    {
        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        public bool IsArchived { get; set; }

        public int OpenCount { get; set; }

        public int CompletedCount { get; set; }

        /// <summary>0-100, rounded — OpenCount + CompletedCount can be 0 (empty project), in
        /// which case this is 0, not NaN/undefined.</summary>
        public int PercentComplete { get; set; }
    }

    public class PriorityCountDto
    {
        public required string Priority { get; set; }

        public int Count { get; set; }
    }

    public class StatusCountDto
    {
        public required string Status { get; set; }

        public int Count { get; set; }
    }

    public class ActivityFeedItemDto
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public required string TaskTitle { get; set; }

        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        /// <summary>Null when the acting user has since been removed — TaskActivity.UserId is
        /// nullable for exactly this reason (see its own doc comment).</summary>
        public string? UserName { get; set; }

        public required string Action { get; set; }

        public string? FieldName { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class TeamWorkloadDto
    {
        public required List<WorkloadEntryDto> Entries { get; set; }
    }

    public class WorkloadEntryDto
    {
        public Guid UserId { get; set; }

        public required string UserName { get; set; }

        public int OpenTaskCount { get; set; }
    }
}

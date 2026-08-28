using Silver_Task.Server.Models.DTOs.Dashboard;

namespace Silver_Task.Server.Models.DTOs.Reports
{
    public class TaskSummaryReportDto
    {
        public int Total { get; set; }

        public int Completed { get; set; }

        public int Open { get; set; }

        public int Overdue { get; set; }

        /// <summary>Completed / Total, 0 when Total is 0 — never a misleading 0% when there's
        /// simply no data (see EmptyReport handling on the frontend, which shows "No data" instead
        /// of rendering this value in that case).</summary>
        public double CompletionRate { get; set; }

        public required List<StatusCountDto> ByStatus { get; set; }

        public required List<PriorityCountDto> ByPriority { get; set; }
    }

    /// <summary>One bucket of a time-series report — Label is the display string ("Aug 20" /
    /// "Week of Aug 18" / "Aug 2026"), PeriodStart the underlying bucket-start date the label was
    /// derived from (for drill-down/sorting).</summary>
    public class TrendPointDto
    {
        public required string Label { get; set; }

        public DateOnly PeriodStart { get; set; }

        public int Count { get; set; }
    }

    /// <summary>Granularity is chosen from the requested date range's span — day for <= 31 days,
    /// week for <= 90 days, month otherwise — so a year-long trend isn't 365 unreadable daily
    /// points (spec's own "daily/weekly/monthly depending on range" instruction).</summary>
    public class TrendReportDto
    {
        public required string Granularity { get; set; }

        public required List<TrendPointDto> Points { get; set; }
    }

    public class OverdueTaskRowDto
    {
        public Guid TaskId { get; set; }

        public required string TaskTitle { get; set; }

        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        public string? AssigneeName { get; set; }

        public DateOnly DueDate { get; set; }

        public int DaysOverdue { get; set; }

        public required string Priority { get; set; }
    }

    public class OverdueReportDto
    {
        public required List<OverdueTaskRowDto> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    /// <summary>Health is deliberately derived from two objective, already-tracked facts (never a
    /// fabricated score): Overdue means the project has at least one open task past its due date;
    /// AtRisk means nothing is overdue yet but at least one open task is due within the next 3
    /// days; Healthy is everything else, including an empty project. This is the entire rule — no
    /// hidden weighting, and it isn't configurable in this phase (see spec's own "objective
    /// existing data... or clearly define them" allowance).</summary>
    public class ProjectProgressReportRowDto
    {
        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        public int TaskCount { get; set; }

        public int CompletedCount { get; set; }

        public int PercentComplete { get; set; }

        public int OverdueCount { get; set; }

        public required string Health { get; set; }
    }

    public class ProjectProgressReportDto
    {
        public required List<ProjectProgressReportRowDto> Projects { get; set; }

        /// <summary>Only populated when the filter narrows to a single project — reconstructed
        /// live from CreatedAt/CompletedAt (percent-complete-as-of-each-sampled-date), never a
        /// fabricated/stored snapshot. See ReportingService's own doc comment.</summary>
        public TrendReportDto? CompletionTrend { get; set; }
    }

    public class UserWorkloadRowDto
    {
        public Guid UserId { get; set; }

        public required string UserName { get; set; }

        public int OpenCount { get; set; }

        public int CompletedCount { get; set; }

        public int OverdueCount { get; set; }

        public double CompletionRate { get; set; }
    }

    public class UserWorkloadReportDto
    {
        public required List<UserWorkloadRowDto> Entries { get; set; }
    }

    public class TaskAgeBucketDto
    {
        public required string Bucket { get; set; }

        public int Count { get; set; }
    }

    /// <summary>Open-task age only, buckets fixed at 0-7/8-14/15-30/31-60/60+ days since
    /// CreatedAt — read-only, never modifies task data (spec's own explicit instruction).</summary>
    public class TaskAgeReportDto
    {
        public required List<TaskAgeBucketDto> Buckets { get; set; }

        public int TotalOpen { get; set; }
    }

    public class OldTaskRowDto
    {
        public Guid TaskId { get; set; }

        public required string TaskTitle { get; set; }

        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        public string? AssigneeName { get; set; }

        public DateTime CreatedAt { get; set; }

        public int AgeDays { get; set; }
    }

    public class OldTaskReportDto
    {
        public required List<OldTaskRowDto> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int ThresholdDays { get; set; }
    }

    /// <summary>Created-&gt;Completed only (both timestamps are always system-set and reliable) —
    /// deliberately NOT Cycle Time (Started-&gt;Completed), which this app has no reliable "started"
    /// timestamp for and is omitted entirely per the spec's own "do not guess start times"
    /// allowance. Keep these two concepts separate; nothing in this DTO is ever mixed with a
    /// started-at calculation.</summary>
    public class CompletionTimeReportDto
    {
        public double? AverageDays { get; set; }

        public int SampleSize { get; set; }

        public required List<PriorityCompletionTimeDto> ByPriority { get; set; }
    }

    public class PriorityCompletionTimeDto
    {
        public required string Priority { get; set; }

        public double? AverageDays { get; set; }

        public int SampleSize { get; set; }
    }

    /// <summary>Generic label/count pair for breakdowns that don't already have a typed DTO
    /// (notification type, file category, automation trigger type) — avoids a one-off class per
    /// breakdown for shapes that are all genuinely just "count grouped by a string".</summary>
    public class LabeledCountDto
    {
        public required string Label { get; set; }

        public int Count { get; set; }
    }

    public class AutomationReportRowDto
    {
        public Guid AutomationId { get; set; }

        public required string Name { get; set; }

        public required string TriggerType { get; set; }

        public bool IsActive { get; set; }

        public int RunCount { get; set; }

        public int SuccessCount { get; set; }

        public int FailedCount { get; set; }

        public DateTime? LastRunAt { get; set; }
    }

    /// <summary>Reuses Phase 35's own AutomationExecution history — never a second automation
    /// engine or duplicated run-tracking.</summary>
    public class AutomationReportDto
    {
        public required List<AutomationReportRowDto> Automations { get; set; }
    }

    /// <summary>Aggregate counts only — Title/Message/Metadata are never included, per the spec's
    /// own "avoid exposing private notification content" instruction.</summary>
    public class NotificationReportDto
    {
        public int TotalCount { get; set; }

        public int UnreadCount { get; set; }

        public required List<LabeledCountDto> ByType { get; set; }

        public required List<LabeledCountDto> ByPriority { get; set; }
    }

    /// <summary>File metadata only (name/size/category counts) — never file contents, and every
    /// count is already scoped by the same project ownership/membership predicate as everything
    /// else, so this can never surface a file from a project the caller can't see.</summary>
    public class FileReportDto
    {
        public int TotalFiles { get; set; }

        public long TotalSizeBytes { get; set; }

        public int FilesInRange { get; set; }

        public required List<LabeledCountDto> ByCategory { get; set; }
    }

    /// <summary>Administrator-only, system-wide — every metric here is something the application
    /// already tracks elsewhere (Users/Projects/Tasks counts, Automations, Notifications, Files);
    /// nothing new is invented for this report, per the spec's own "only include metrics already
    /// supported by the existing data model" instruction. No passwords/credentials/tokens are ever
    /// part of this shape.</summary>
    public class AdminSystemReportDto
    {
        public int TotalUsers { get; set; }

        public int ActiveUsers { get; set; }

        public int TotalProjects { get; set; }

        public int TotalTasks { get; set; }

        public int CompletedTasks { get; set; }

        public int OverdueTasks { get; set; }

        public int ActiveAutomations { get; set; }

        public int TotalNotifications { get; set; }

        public int TotalFiles { get; set; }
    }

    /// <summary>Phase 40 — integrates with the existing reporting engine rather than a second one
    /// (spec #67). Scoped to the caller's own accessible projects/templates, same "Authenticated
    /// user -> Authorization scope -> Database query -> Report" pattern every other report
    /// follows.</summary>
    public class TemplateUsageReportDto
    {
        public int ProjectsCreatedFromTemplate { get; set; }

        public required List<TemplateUsageRowDto> MostUsedTemplates { get; set; }
    }

    public class TemplateUsageRowDto
    {
        public Guid TemplateId { get; set; }

        public required string TemplateName { get; set; }

        public required string Type { get; set; }

        public int UsageCount { get; set; }

        public DateTime? LastUsedAt { get; set; }
    }

    /// <summary>Phase 39 — "Circular Dependency Attempts" from the spec's own suggested metric
    /// list is deliberately omitted: a rejected circular-dependency request fails validation
    /// before anything is written anywhere, so there is no persisted record of the attempt to
    /// count (this app has no request-level audit log, only entity-change history) — inventing one
    /// would mean fabricating data, which the existing reporting conventions explicitly avoid.</summary>
    public class DependencyReportDto
    {
        public int TotalDependencies { get; set; }

        public int BlockedTasks { get; set; }

        public int ReadyTasks { get; set; }

        public int TasksBlockingOthers { get; set; }

        public int DependencyOverrides { get; set; }
    }

    public class BlockedTaskRowDto
    {
        public Guid TaskId { get; set; }

        public required string TaskTitle { get; set; }

        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        public string? AssigneeName { get; set; }

        public required List<string> BlockedBy { get; set; }

        /// <summary>The earliest CreatedAt among this task's currently-unsatisfied dependency
        /// edges — a real, non-fabricated LOWER BOUND on how long the task has been blocked (the
        /// prerequisite could have been unsatisfied since the edge was created, or could have
        /// become unsatisfied again later after a status change — this app doesn't track that
        /// transition history, see this DTO's own doc comment on why true "Days Blocked" isn't
        /// implemented). Null only if BlockedBy is somehow empty (shouldn't happen for a row in
        /// this report).</summary>
        public DateTime? BlockedSince { get; set; }

        public required string Priority { get; set; }
    }

    /// <summary>The spec's "Blocked Time" / "Average Blocked Time" metric is deliberately omitted
    /// — computing it accurately requires recording the actual moment a task became blocked and
    /// the moment it became unblocked (TaskBlockedAt/TaskUnblockedAt), which this app does not
    /// track anywhere (TaskActivity logs dependency ADD/REMOVE events, not continuous blocked-
    /// state transitions). BlockedSince above is offered instead as an honest, clearly-labeled
    /// lower-bound proxy, per the spec's own "do not invent historical blocked timestamps"
    /// instruction.</summary>
    public class BlockedTaskReportDto
    {
        public required List<BlockedTaskRowDto> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class BottleneckRowDto
    {
        public Guid TaskId { get; set; }

        public required string TaskTitle { get; set; }

        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        public int BlocksCount { get; set; }
    }

    public class WorkflowBottlenecksReportDto
    {
        public required List<BottleneckRowDto> Items { get; set; }
    }

    public class DependencyChainNodeDto
    {
        public Guid TaskId { get; set; }

        public required string TaskTitle { get; set; }

        public required string Status { get; set; }
    }

    /// <summary>Deliberately labeled "Longest Dependency Chain", not "Critical Path" — a true
    /// Critical Path calculation needs reliable task DURATION data (start + due date) for every
    /// task along every candidate path, and StartDate/DueDate are optional, frequently-null fields
    /// in this app; computing a duration-weighted path over partially-missing data would produce a
    /// misleading result (per the spec's own explicit "do not implement misleading critical-path
    /// results" instruction). This is instead an honest, purely graph-topological longest path
    /// (by number of tasks, not calendar time) through the project's real dependency edges — still
    /// genuinely useful for spotting the deepest workflow chain, just not a schedule-based CPM.</summary>
    public class LongestDependencyChainReportDto
    {
        public Guid ProjectId { get; set; }

        public required string ProjectName { get; set; }

        public required List<DependencyChainNodeDto> Chain { get; set; }
    }
}

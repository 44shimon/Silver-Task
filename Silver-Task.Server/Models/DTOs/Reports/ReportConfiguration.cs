namespace Silver_Task.Server.Models.DTOs.Reports
{
    /// <summary>The ONLY shape a SavedReport.Configuration JSON blob is ever deserialized into —
    /// every field is a closed, validated value (a report-type key checked against
    /// ReportTypes.All, a date-range key, ids, and enum-backed status/priority strings); there is
    /// no field capable of holding executable code or an arbitrary query, so "never allow the
    /// report builder to accept arbitrary SQL/C#/JavaScript" is satisfied structurally, not just
    /// by convention. GroupBy is only meaningful for ReportType "Custom" (the minimal report
    /// builder — Data source is always Tasks, Metric is always Count, per the spec's own "keep
    /// this manageable, do not build a full BI platform" instruction).</summary>
    public class ReportConfiguration
    {
        public required string ReportType { get; set; }

        public string? DateRange { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public Guid? ProjectId { get; set; }

        public Guid? UserId { get; set; }

        public string? Status { get; set; }

        public string? Priority { get; set; }

        public Guid? LabelId { get; set; }

        /// <summary>Custom report builder only — "Project"/"Status"/"Priority"/"Assignee".</summary>
        public string? GroupBy { get; set; }
    }

    /// <summary>The fixed whitelist of report types the builder/saved-report execution path will
    /// ever recognize — anything else is rejected at save and at execute time.</summary>
    public static class ReportTypes
    {
        public const string TaskSummary = "TaskSummary";
        public const string CompletionTrend = "CompletionTrend";
        public const string CreationTrend = "CreationTrend";
        public const string Overdue = "Overdue";
        public const string OverdueTrend = "OverdueTrend";
        public const string ProjectProgress = "ProjectProgress";
        public const string Workload = "Workload";
        public const string UserCompletion = "UserCompletion";
        public const string TaskAge = "TaskAge";
        public const string OldTasks = "OldTasks";
        public const string CompletionTime = "CompletionTime";
        public const string Custom = "Custom";

        public static readonly IReadOnlyCollection<string> All =
        [
            TaskSummary, CompletionTrend, CreationTrend, Overdue, OverdueTrend,
            ProjectProgress, Workload, UserCompletion, TaskAge, OldTasks, CompletionTime, Custom
        ];

        public static readonly IReadOnlyCollection<string> GroupByFields = ["Project", "Status", "Priority", "Assignee"];
    }
}

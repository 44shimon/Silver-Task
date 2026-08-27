using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Reports
{
    /// <summary>The one closed filter set every report endpoint binds from the query string
    /// (Phase 38) — Date Range/Project/User/Status/Priority/Label, plus paging for the detailed
    /// (row-level) reports. Kept as a single shared shape rather than one bespoke filter class per
    /// endpoint so ReportingService's authorization-then-query pattern (see its own doc comment)
    /// never has to be re-derived per report. DateRange follows
    /// DashboardDateHelper.ReportDateRange's own key convention
    /// ("today"/"yesterday"/"thisWeek"/"lastWeek"/"thisMonth"/"lastMonth"/"thisQuarter"/
    /// "thisYear"/"custom").</summary>
    public class ReportFilterRequest
    {
        public string? DateRange { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public Guid? ProjectId { get; set; }

        /// <summary>Assignee filter — never used to scope which projects are visible (that's
        /// always the caller's own ownership/membership, see ReportingService), only to narrow
        /// results to a specific assignee within whatever the caller can already see.</summary>
        public Guid? UserId { get; set; }

        public TaskItemStatus? Status { get; set; }

        public TaskPriority? Priority { get; set; }

        public Guid? LabelId { get; set; }

        public string? Search { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 25;
    }
}

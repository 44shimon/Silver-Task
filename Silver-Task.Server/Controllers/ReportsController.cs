using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Reports;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Phase 38 — every action derives the caller from User.GetUserId()/User.GetRole(), never a
    /// query parameter (same IDOR-safe pattern DashboardController established); every underlying
    /// query is additionally scoped by project ownership/membership inside ReportingService. The
    /// feature itself is gated by Permissions.ReportsView/Export (checked here, via
    /// IPermissionService, so this never drifts out of sync with PermissionService.SystemMatrix)
    /// — but that gate only controls whether the *endpoints* are reachable at all; the *data* any
    /// reachable endpoint returns is always separately re-scoped by ReportingService regardless of
    /// what the caller's permission set says, per the spec's own "Authenticated user -> Authorization
    /// scope -> Database query -> Report" requirement.
    /// </summary>
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController(IReportingService reportingService, IReportExportService exportService, IPermissionService permissionService) : ControllerBase
    {
        private readonly IReportingService _reportingService = reportingService;
        private readonly IReportExportService _exportService = exportService;
        private readonly IPermissionService _permissionService = permissionService;

        private async Task EnsureCanViewReportsAsync()
        {
            var permissions = await _permissionService.GetSystemPermissionsAsync(User.GetRole());
            if (!permissions.Contains(Permissions.ReportsView))
            {
                throw new ForbiddenException("You do not have permission to view reports.");
            }
        }

        private async Task EnsureCanExportReportsAsync()
        {
            var permissions = await _permissionService.GetSystemPermissionsAsync(User.GetRole());
            if (!permissions.Contains(Permissions.ReportsExport))
            {
                throw new ForbiddenException("You do not have permission to export reports.");
            }
        }

        [HttpGet("task-summary")]
        public async Task<ActionResult<TaskSummaryReportDto>> GetTaskSummary([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetTaskSummaryAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("completion-trend")]
        public async Task<ActionResult<TrendReportDto>> GetCompletionTrend([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetCompletionTrendAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("creation-trend")]
        public async Task<ActionResult<TrendReportDto>> GetCreationTrend([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetCreationTrendAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("overdue")]
        public async Task<ActionResult<OverdueReportDto>> GetOverdue([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetOverdueReportAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("overdue-trend")]
        public async Task<ActionResult<TrendReportDto>> GetOverdueTrend([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetOverdueTrendAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("project-progress")]
        public async Task<ActionResult<ProjectProgressReportDto>> GetProjectProgress([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetProjectProgressAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("workload")]
        public async Task<ActionResult<UserWorkloadReportDto>> GetWorkload([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetWorkloadAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("task-age")]
        public async Task<ActionResult<TaskAgeReportDto>> GetTaskAge([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetTaskAgeAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("old-tasks")]
        public async Task<ActionResult<OldTaskReportDto>> GetOldTasks([FromQuery] ReportFilterRequest filter, [FromQuery] int thresholdDays = 30)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetOldTasksAsync(User.GetUserId(), User.GetRole(), filter, thresholdDays));
        }

        [HttpGet("completion-time")]
        public async Task<ActionResult<CompletionTimeReportDto>> GetCompletionTime([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetCompletionTimeAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("automations")]
        public async Task<ActionResult<AutomationReportDto>> GetAutomationReport([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetAutomationReportAsync(User.GetUserId(), User.GetRole(), filter));
        }

        /// <summary>Always the caller's OWN notifications — see
        /// ReportingService.GetMyNotificationReportAsync's own doc comment; there is no
        /// userId-selectable variant of this endpoint.</summary>
        [HttpGet("notifications")]
        public async Task<ActionResult<NotificationReportDto>> GetNotificationReport()
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetMyNotificationReportAsync(User.GetUserId()));
        }

        [HttpGet("files")]
        public async Task<ActionResult<FileReportDto>> GetFileReport([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetFileReportAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("admin-system")]
        [Authorize(Roles = nameof(UserRole.Administrator))]
        public async Task<ActionResult<AdminSystemReportDto>> GetAdminSystemReport()
        {
            return Ok(await _reportingService.GetAdminSystemReportAsync());
        }

        [HttpGet("custom")]
        public async Task<ActionResult<List<LabeledCountDto>>> GetCustomReport([FromQuery] ReportFilterRequest filter, [FromQuery] string groupBy = "Project")
        {
            await EnsureCanViewReportsAsync();
            if (!ReportTypes.GroupByFields.Contains(groupBy))
            {
                throw new ValidationException("Unrecognized Group By field.");
            }
            return Ok(await _reportingService.GetCustomReportAsync(User.GetUserId(), User.GetRole(), filter, groupBy));
        }

        [HttpGet("dependencies")]
        public async Task<ActionResult<DependencyReportDto>> GetDependencyReport([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetDependencyReportAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("blocked-tasks")]
        public async Task<ActionResult<BlockedTaskReportDto>> GetBlockedTaskReport([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetBlockedTaskReportAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("bottlenecks")]
        public async Task<ActionResult<WorkflowBottlenecksReportDto>> GetWorkflowBottlenecks([FromQuery] ReportFilterRequest filter)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetWorkflowBottlenecksAsync(User.GetUserId(), User.GetRole(), filter));
        }

        [HttpGet("dependency-chain")]
        public async Task<ActionResult<LongestDependencyChainReportDto>> GetLongestDependencyChain([FromQuery] Guid projectId)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetLongestDependencyChainAsync(User.GetUserId(), User.GetRole(), projectId));
        }

        [HttpGet("template-usage")]
        public async Task<ActionResult<TemplateUsageReportDto>> GetTemplateUsageReport()
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetTemplateUsageReportAsync(User.GetUserId(), User.GetRole()));
        }

        [HttpGet("custom-field-summary")]
        public async Task<ActionResult<CustomFieldSummaryReportDto>> GetCustomFieldSummaryReport([FromQuery] Guid customFieldId)
        {
            await EnsureCanViewReportsAsync();
            return Ok(await _reportingService.GetCustomFieldSummaryAsync(User.GetUserId(), User.GetRole(), customFieldId));
        }

        /// <summary>Export applies EXACTLY the same authorization + query path as the matching
        /// live report endpoint above — never a separate, weaker code path (the spec's own
        /// explicit "export endpoints must apply exactly the same authorization rules" rule).
        /// Detail reports (Overdue/OldTasks) are capped at 5000 rows for export, matching this
        /// app's "never load unbounded data into memory" performance rule.</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] ReportFilterRequest filter, [FromQuery] string reportType, [FromQuery] string format = "csv", [FromQuery] string? groupBy = null, [FromQuery] int thresholdDays = 30)
        {
            await EnsureCanViewReportsAsync();
            await EnsureCanExportReportsAsync();

            if (!Enum.TryParse<ReportExportFormat>(format, ignoreCase: true, out var exportFormat))
            {
                throw new ValidationException("Unrecognized export format — use csv, excel, or pdf.");
            }

            var callerId = User.GetUserId();
            var callerRole = User.GetRole();

            var (title, headers, rows) = reportType switch
            {
                ReportTypes.TaskSummary => await BuildTaskSummaryExportAsync(callerId, callerRole, filter),
                ReportTypes.Overdue => await BuildOverdueExportAsync(callerId, callerRole, filter),
                ReportTypes.ProjectProgress => await BuildProjectProgressExportAsync(callerId, callerRole, filter),
                ReportTypes.Workload => await BuildWorkloadExportAsync(callerId, callerRole, filter),
                ReportTypes.TaskAge => await BuildTaskAgeExportAsync(callerId, callerRole, filter),
                ReportTypes.OldTasks => await BuildOldTasksExportAsync(callerId, callerRole, filter, thresholdDays),
                ReportTypes.CompletionTime => await BuildCompletionTimeExportAsync(callerId, callerRole, filter),
                ReportTypes.Custom => await BuildCustomExportAsync(callerId, callerRole, filter, groupBy ?? "Project"),
                ReportTypes.BlockedTasks => await BuildBlockedTasksExportAsync(callerId, callerRole, filter),
                ReportTypes.Bottlenecks => await BuildBottlenecksExportAsync(callerId, callerRole, filter),
                _ => throw new ValidationException("Unrecognized or non-exportable report type.")
            };

            var bytes = _exportService.Export(exportFormat, title, headers, rows);
            var fileName = $"{title.Replace(' ', '-')}.{_exportService.GetFileExtension(exportFormat)}";
            return File(bytes, _exportService.GetContentType(exportFormat), fileName);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildTaskSummaryExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var summary = await _reportingService.GetTaskSummaryAsync(callerId, callerRole, filter);
            var headers = new[] { "Metric", "Value" };
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Total", summary.Total.ToString() },
                new[] { "Completed", summary.Completed.ToString() },
                new[] { "Open", summary.Open.ToString() },
                new[] { "Overdue", summary.Overdue.ToString() },
                new[] { "Completion Rate", summary.CompletionRate.ToString("P0") }
            };
            return ("Task Summary Report", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildOverdueExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var exportFilter = new ReportFilterRequest
            {
                ProjectId = filter.ProjectId, UserId = filter.UserId, Status = filter.Status, Priority = filter.Priority,
                LabelId = filter.LabelId, Search = filter.Search, Page = 1, PageSize = 5000
            };
            var report = await _reportingService.GetOverdueReportAsync(callerId, callerRole, exportFilter);
            var headers = new[] { "Task", "Project", "Assignee", "Due Date", "Days Overdue", "Priority" };
            var rows = report.Items.Select(r => (IReadOnlyList<string>)new[]
            {
                r.TaskTitle, r.ProjectName, r.AssigneeName ?? "Unassigned", r.DueDate.ToString("yyyy-MM-dd"), r.DaysOverdue.ToString(), r.Priority
            }).ToList();
            return ("Overdue Tasks Report", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildProjectProgressExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var report = await _reportingService.GetProjectProgressAsync(callerId, callerRole, filter);
            var headers = new[] { "Project", "Tasks", "Completed", "Progress %", "Overdue", "Health" };
            var rows = report.Projects.Select(p => (IReadOnlyList<string>)new[]
            {
                p.ProjectName, p.TaskCount.ToString(), p.CompletedCount.ToString(), $"{p.PercentComplete}%", p.OverdueCount.ToString(), p.Health
            }).ToList();
            return ("Project Progress Report", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildWorkloadExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var report = await _reportingService.GetWorkloadAsync(callerId, callerRole, filter);
            var headers = new[] { "User", "Open", "Completed", "Overdue", "Completion Rate" };
            var rows = report.Entries.Select(e => (IReadOnlyList<string>)new[]
            {
                e.UserName, e.OpenCount.ToString(), e.CompletedCount.ToString(), e.OverdueCount.ToString(), e.CompletionRate.ToString("P0")
            }).ToList();
            return ("User Workload Report", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildTaskAgeExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var report = await _reportingService.GetTaskAgeAsync(callerId, callerRole, filter);
            var headers = new[] { "Age Bucket (days)", "Count" };
            var rows = report.Buckets.Select(b => (IReadOnlyList<string>)new[] { b.Bucket, b.Count.ToString() }).ToList();
            return ("Task Age Report", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildOldTasksExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter, int thresholdDays)
        {
            var exportFilter = new ReportFilterRequest
            {
                ProjectId = filter.ProjectId, UserId = filter.UserId, Status = filter.Status, Priority = filter.Priority,
                LabelId = filter.LabelId, Search = filter.Search, Page = 1, PageSize = 5000
            };
            var report = await _reportingService.GetOldTasksAsync(callerId, callerRole, exportFilter, thresholdDays);
            var headers = new[] { "Task", "Project", "Assignee", "Created", "Age (days)" };
            var rows = report.Items.Select(r => (IReadOnlyList<string>)new[]
            {
                r.TaskTitle, r.ProjectName, r.AssigneeName ?? "Unassigned", r.CreatedAt.ToString("yyyy-MM-dd"), r.AgeDays.ToString()
            }).ToList();
            return ($"Old Tasks Report (over {thresholdDays} days)", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildCompletionTimeExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var report = await _reportingService.GetCompletionTimeAsync(callerId, callerRole, filter);
            var headers = new[] { "Priority", "Average Days to Complete", "Sample Size" };
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Overall", report.AverageDays?.ToString("F1") ?? "No data", report.SampleSize.ToString() }
            };
            rows.AddRange(report.ByPriority.Select(p => (IReadOnlyList<string>)new[] { p.Priority, p.AverageDays?.ToString("F1") ?? "No data", p.SampleSize.ToString() }));
            return ("Completion Time Report", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildCustomExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter, string groupBy)
        {
            if (!ReportTypes.GroupByFields.Contains(groupBy))
            {
                throw new ValidationException("Unrecognized Group By field.");
            }
            var rows = await _reportingService.GetCustomReportAsync(callerId, callerRole, filter, groupBy);
            var headers = new[] { groupBy, "Count" };
            var exportRows = rows.Select(r => (IReadOnlyList<string>)new[] { r.Label, r.Count.ToString() }).ToList();
            return ($"Custom Report (grouped by {groupBy})", headers, exportRows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildBlockedTasksExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var exportFilter = new ReportFilterRequest
            {
                ProjectId = filter.ProjectId, UserId = filter.UserId, Status = filter.Status, Priority = filter.Priority,
                LabelId = filter.LabelId, Search = filter.Search, Page = 1, PageSize = 5000
            };
            var report = await _reportingService.GetBlockedTaskReportAsync(callerId, callerRole, exportFilter);
            var headers = new[] { "Task", "Project", "Assignee", "Blocked By", "Blocked Since", "Priority" };
            var rows = report.Items.Select(r => (IReadOnlyList<string>)new[]
            {
                r.TaskTitle, r.ProjectName, r.AssigneeName ?? "Unassigned", string.Join("; ", r.BlockedBy),
                r.BlockedSince?.ToString("yyyy-MM-dd") ?? "", r.Priority
            }).ToList();
            return ("Blocked Tasks Report", headers, rows);
        }

        private async Task<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> BuildBottlenecksExportAsync(Guid callerId, UserRole callerRole, ReportFilterRequest filter)
        {
            var report = await _reportingService.GetWorkflowBottlenecksAsync(callerId, callerRole, filter);
            var headers = new[] { "Task", "Project", "Blocks" };
            var rows = report.Items.Select(r => (IReadOnlyList<string>)new[] { r.TaskTitle, r.ProjectName, r.BlocksCount.ToString() }).ToList();
            return ("Workflow Bottlenecks Report", headers, rows);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Reports;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Phase 38 — CRUD/share/favorite/execute for SavedReport. Every action derives the caller
    /// from User.GetUserId()/User.GetRole(); ownership and share-visibility checks happen inside
    /// ISavedReportService, and Execute in particular re-validates the caller's LIVE project
    /// access every single time (see ISavedReportService.PrepareExecutionAsync's own doc comment)
    /// — a report shared or created when the caller had access does not remain runnable after
    /// that access is revoked.
    /// </summary>
    [ApiController]
    [Route("api/saved-reports")]
    [Authorize]
    public class SavedReportsController(ISavedReportService savedReportService, IReportingService reportingService) : ControllerBase
    {
        private readonly ISavedReportService _savedReportService = savedReportService;
        private readonly IReportingService _reportingService = reportingService;

        [HttpGet]
        public async Task<ActionResult<List<SavedReportDto>>> List()
        {
            return Ok(await _savedReportService.ListForCallerAsync(User.GetUserId(), User.GetRole()));
        }

        [HttpPost]
        public async Task<ActionResult<SavedReportDto>> Create([FromBody] SaveReportRequest request)
        {
            var report = await _savedReportService.CreateAsync(User.GetUserId(), User.GetRole(), request);
            return CreatedAtAction(nameof(List), new { }, report);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<SavedReportDto>> Update(Guid id, [FromBody] SaveReportRequest request)
        {
            return Ok(await _savedReportService.UpdateAsync(id, User.GetUserId(), User.GetRole(), request));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _savedReportService.DeleteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/duplicate")]
        public async Task<ActionResult<SavedReportDto>> Duplicate(Guid id)
        {
            return Ok(await _savedReportService.DuplicateAsync(id, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/share")]
        public async Task<IActionResult> Share(Guid id, [FromBody] ShareReportRequest request)
        {
            var found = await _savedReportService.ShareAsync(id, User.GetUserId(), User.GetRole(), request.Email);
            if (!found)
            {
                return NotFound(new { message = $"No user found with email '{request.Email}'." });
            }
            return NoContent();
        }

        [HttpDelete("{id:guid}/share/{userId:guid}")]
        public async Task<IActionResult> Unshare(Guid id, Guid userId)
        {
            await _savedReportService.UnshareAsync(id, User.GetUserId(), User.GetRole(), userId);
            return NoContent();
        }

        [HttpPost("{id:guid}/favorite")]
        public async Task<IActionResult> Favorite(Guid id)
        {
            await _savedReportService.FavoriteAsync(id, User.GetUserId());
            return NoContent();
        }

        [HttpDelete("{id:guid}/favorite")]
        public async Task<IActionResult> Unfavorite(Guid id)
        {
            await _savedReportService.UnfavoriteAsync(id, User.GetUserId());
            return NoContent();
        }

        /// <summary>Resolves the saved, validated configuration and runs it through the exact
        /// same ReportingService methods the live report endpoints use — never a second/duplicate
        /// query path. Returned as a plain object since each report type's result shape differs;
        /// the frontend already knows which shape to expect from the report's own ReportType.</summary>
        [HttpGet("{id:guid}/execute")]
        public async Task<IActionResult> Execute(Guid id)
        {
            var callerId = User.GetUserId();
            var callerRole = User.GetRole();
            var config = await _savedReportService.PrepareExecutionAsync(id, callerId, callerRole);

            var filter = new ReportFilterRequest
            {
                DateRange = config.DateRange,
                StartDate = config.StartDate,
                EndDate = config.EndDate,
                ProjectId = config.ProjectId,
                UserId = config.UserId,
                Status = Enum.TryParse<Models.Entities.Enums.TaskItemStatus>(config.Status, out var status) ? status : null,
                Priority = Enum.TryParse<Models.Entities.Enums.TaskPriority>(config.Priority, out var priority) ? priority : null,
                LabelId = config.LabelId
            };

            object result = config.ReportType switch
            {
                ReportTypes.TaskSummary => await _reportingService.GetTaskSummaryAsync(callerId, callerRole, filter),
                ReportTypes.CompletionTrend => await _reportingService.GetCompletionTrendAsync(callerId, callerRole, filter),
                ReportTypes.CreationTrend => await _reportingService.GetCreationTrendAsync(callerId, callerRole, filter),
                ReportTypes.Overdue => await _reportingService.GetOverdueReportAsync(callerId, callerRole, filter),
                ReportTypes.OverdueTrend => await _reportingService.GetOverdueTrendAsync(callerId, callerRole, filter),
                ReportTypes.ProjectProgress => await _reportingService.GetProjectProgressAsync(callerId, callerRole, filter),
                ReportTypes.Workload => await _reportingService.GetWorkloadAsync(callerId, callerRole, filter),
                ReportTypes.UserCompletion => await _reportingService.GetWorkloadAsync(callerId, callerRole, filter),
                ReportTypes.TaskAge => await _reportingService.GetTaskAgeAsync(callerId, callerRole, filter),
                ReportTypes.OldTasks => await _reportingService.GetOldTasksAsync(callerId, callerRole, filter, 30),
                ReportTypes.CompletionTime => await _reportingService.GetCompletionTimeAsync(callerId, callerRole, filter),
                ReportTypes.Custom => await _reportingService.GetCustomReportAsync(callerId, callerRole, filter, config.GroupBy ?? "Project"),
                _ => throw new Common.Exceptions.ValidationException("Unrecognized report type.")
            };

            return Ok(result);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Dashboard;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Phase 37 — every action derives the caller from User.GetUserId()/User.GetRole() (the
    /// authenticated identity), never from a query parameter — there is no way to request another
    /// user's dashboard, team workload, or activity feed through this controller (see the spec's
    /// own explicit "GET /api/dashboard?userId=another-user must not work" IDOR test). Every
    /// underlying query is additionally scoped by project ownership/membership in
    /// DashboardService — this controller never fetches broadly and filters afterward.
    /// </summary>
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController(IDashboardService dashboardService) : ControllerBase
    {
        private readonly IDashboardService _dashboardService = dashboardService;

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> GetDashboard(
            [FromQuery] string? upcomingRange = null, [FromQuery] string? statsRange = null)
        {
            var dashboard = await _dashboardService.GetDashboardAsync(User.GetUserId(), User.GetRole(), upcomingRange, statsRange);
            return Ok(dashboard);
        }

        /// <summary>Null (204) when the caller doesn't manage any project — the frontend treats
        /// that as "hide this widget entirely", not an empty-but-present team.</summary>
        [HttpGet("team-workload")]
        public async Task<ActionResult<TeamWorkloadDto>> GetTeamWorkload()
        {
            var workload = await _dashboardService.GetTeamWorkloadAsync(User.GetUserId(), User.GetRole());
            return workload is null ? NoContent() : Ok(workload);
        }

        [HttpGet("activity")]
        public async Task<ActionResult<IReadOnlyList<ActivityFeedItemDto>>> GetActivity(
            [FromQuery] bool mineOnly = false, [FromQuery] int limit = 15)
        {
            var activity = await _dashboardService.GetRecentActivityAsync(User.GetUserId(), User.GetRole(), mineOnly, limit);
            return Ok(activity);
        }

        [HttpGet("workflow")]
        public async Task<ActionResult<WorkflowSummaryDto>> GetWorkflowSummary()
        {
            return Ok(await _dashboardService.GetWorkflowSummaryAsync(User.GetUserId(), User.GetRole()));
        }
    }
}

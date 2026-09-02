using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.Diagnostics;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Phase 58 — Administrator-only production diagnostics. Matches
    /// AdminEmailController/AdminSettingsController's own [Authorize(Roles=Administrator)]
    /// pattern; nothing here is reachable by an ordinary Member/Manager. Deliberately separate
    /// from the existing anonymous HealthController (api/health, api/health/ready), which stays
    /// exactly as it is for external uptime monitors — this endpoint can safely return more detail
    /// (exact storage path, per-worker names/timestamps) precisely because it isn't anonymous.</summary>
    [ApiController]
    [Route("api/admin/diagnostics")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminDiagnosticsController(IDiagnosticsService diagnosticsService) : ControllerBase
    {
        private readonly IDiagnosticsService _diagnosticsService = diagnosticsService;

        [HttpGet]
        public async Task<ActionResult<DiagnosticsDto>> Get() => Ok(await _diagnosticsService.GetDiagnosticsAsync());
    }
}

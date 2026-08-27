using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Automations;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Admin -> Automations — global (system-wide, ProjectId-null) automations only.
    /// Editing/enabling/disabling/duplicating/deleting/testing a global automation, and viewing
    /// its run history, all reuse AutomationsController's existing single-item endpoints
    /// unchanged, since AutomationService already authorizes those per-automation regardless of
    /// which controller called in (same pattern as AdminCustomFieldsController).</summary>
    [ApiController]
    [Route("api/admin/automations")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminAutomationsController(IAutomationService automationService) : ControllerBase
    {
        private readonly IAutomationService _automationService = automationService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AutomationDto>>> GetAll(
            [FromQuery] string? search = null, [FromQuery] AutomationTriggerType? triggerType = null,
            [FromQuery] bool? isActive = null, [FromQuery] Guid? createdByUserId = null)
        {
            var automations = await _automationService.GetAllGlobalAsync(
                User.GetUserId(), User.GetRole(), search, triggerType, isActive, createdByUserId);
            return Ok(automations.Select(a => a.ToDto()));
        }

        [HttpPost]
        public async Task<ActionResult<AutomationDto>> Create([FromBody] SaveAutomationRequest request)
        {
            request.ProjectId = null;
            var automation = await _automationService.CreateAsync(request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(AutomationsController.GetById), "Automations", new { id = automation.Id }, automation.ToDto());
        }
    }
}

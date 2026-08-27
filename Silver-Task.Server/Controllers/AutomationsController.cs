using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Automations;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/automations")]
    public class AutomationsController(IAutomationService automationService) : ControllerBase
    {
        private readonly IAutomationService _automationService = automationService;

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AutomationDto>> GetById(Guid id)
        {
            var automation = await _automationService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            return Ok(automation.ToDto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AutomationDto>> Update(Guid id, [FromBody] SaveAutomationRequest request)
        {
            var automation = await _automationService.UpdateAsync(id, request, User.GetUserId(), User.GetRole());
            return Ok(automation.ToDto());
        }

        /// <summary>Soft delete — execution history (Runs) is always retained. See
        /// Automation.IsDeleted's own doc comment.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _automationService.DeleteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/enable")]
        public async Task<ActionResult<AutomationDto>> Enable(Guid id)
        {
            var automation = await _automationService.SetActiveAsync(id, true, User.GetUserId(), User.GetRole());
            return Ok(automation.ToDto());
        }

        [HttpPost("{id:guid}/disable")]
        public async Task<ActionResult<AutomationDto>> Disable(Guid id)
        {
            var automation = await _automationService.SetActiveAsync(id, false, User.GetUserId(), User.GetRole());
            return Ok(automation.ToDto());
        }

        /// <summary>Duplicates start disabled — see AutomationService.DuplicateAsync's own doc
        /// comment.</summary>
        [HttpPost("{id:guid}/duplicate")]
        public async Task<ActionResult<AutomationDto>> Duplicate(Guid id)
        {
            var copy = await _automationService.DuplicateAsync(id, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetById), new { id = copy.Id }, copy.ToDto());
        }

        [HttpGet("{id:guid}/runs")]
        public async Task<ActionResult<AutomationExecutionListDto>> GetRuns(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            var (items, totalCount) = await _automationService.GetRunsAsync(id, User.GetUserId(), User.GetRole(), page, pageSize);
            return Ok(new AutomationExecutionListDto { Items = [.. items.Select(e => e.ToDto())], TotalCount = totalCount, Page = page, PageSize = pageSize });
        }

        /// <summary>The {id} segment identifies which automation's run history this belongs to
        /// for URL clarity/consistency with GetRuns above — RetryAsync itself resolves the
        /// authoritative automation from the execution row, so a mismatched {id} here is
        /// harmless, never a permission bypass.</summary>
        [HttpPost("{id:guid}/runs/{runId:guid}/retry")]
        public async Task<ActionResult<AutomationExecutionDto>> RetryRun(Guid id, Guid runId)
        {
            var retried = await _automationService.RetryAsync(runId, User.GetUserId(), User.GetRole());
            return Ok(retried.ToDto());
        }

        /// <summary>Dry run only — never executes an action or writes to the database beyond the
        /// test itself. sampleEntityId is the task/file/project id to evaluate conditions against
        /// (whichever type the automation's own TriggerType implies).</summary>
        [HttpPost("{id:guid}/test")]
        public async Task<ActionResult<AutomationTestResultDto>> Test(Guid id, [FromBody] TestAutomationRequest request)
        {
            var result = await _automationService.TestAsync(id, request.SampleEntityId, User.GetUserId(), User.GetRole());
            return Ok(result);
        }
    }
}

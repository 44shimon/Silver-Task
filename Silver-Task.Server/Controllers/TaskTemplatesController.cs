using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.DTOs.Templates;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/task-templates")]
    [Authorize]
    public class TaskTemplatesController(
        ITemplateService templateService,
        ITemplateInstantiationService instantiationService,
        IPermissionService permissionService) : ControllerBase
    {
        private readonly ITemplateService _templateService = templateService;
        private readonly ITemplateInstantiationService _instantiationService = instantiationService;
        private readonly IPermissionService _permissionService = permissionService;

        private async Task EnsurePermissionAsync(string permission, string message)
        {
            var permissions = await _permissionService.GetSystemPermissionsAsync(User.GetRole());
            if (!permissions.Contains(permission))
            {
                throw new ForbiddenException(message);
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TaskTemplateDto>> Get(Guid id)
        {
            await EnsurePermissionAsync(Permissions.TemplatesView, "You do not have permission to view templates.");
            return Ok(await _templateService.GetTaskTemplateAsync(id, User.GetUserId(), User.GetRole()));
        }

        [HttpPost]
        public async Task<ActionResult<TaskTemplateDto>> Create([FromBody] SaveTaskTemplateRequest request)
        {
            await EnsurePermissionAsync(Permissions.TemplatesCreate, "You do not have permission to create templates.");
            var result = await _templateService.SaveTaskTemplateAsync(null, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<TaskTemplateDto>> Update(Guid id, [FromBody] SaveTaskTemplateRequest request)
        {
            return Ok(await _templateService.SaveTaskTemplateAsync(id, request, User.GetUserId(), User.GetRole()));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _templateService.DeleteTaskTemplateAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/archive")]
        public async Task<ActionResult<TaskTemplateDto>> Archive(Guid id)
        {
            return Ok(await _templateService.SetTaskTemplateArchivedAsync(id, true, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/restore")]
        public async Task<ActionResult<TaskTemplateDto>> Restore(Guid id)
        {
            return Ok(await _templateService.SetTaskTemplateArchivedAsync(id, false, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/duplicate")]
        public async Task<ActionResult<TaskTemplateDto>> Duplicate(Guid id)
        {
            await EnsurePermissionAsync(Permissions.TemplatesCreate, "You do not have permission to create templates.");
            return Ok(await _templateService.DuplicateTaskTemplateAsync(id, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/share")]
        public async Task<IActionResult> Share(Guid id, [FromBody] ShareTemplateRequest request)
        {
            await _templateService.ShareTaskTemplateAsync(id, User.GetUserId(), User.GetRole(), request.Email);
            return NoContent();
        }

        [HttpDelete("{id:guid}/share/{userId:guid}")]
        public async Task<IActionResult> Unshare(Guid id, Guid userId)
        {
            await _templateService.UnshareTaskTemplateAsync(id, User.GetUserId(), User.GetRole(), userId);
            return NoContent();
        }

        [HttpPost("{id:guid}/favorite")]
        public async Task<IActionResult> Favorite(Guid id)
        {
            await _templateService.FavoriteTaskTemplateAsync(id, User.GetUserId(), true);
            return NoContent();
        }

        [HttpDelete("{id:guid}/favorite")]
        public async Task<IActionResult> Unfavorite(Guid id)
        {
            await _templateService.FavoriteTaskTemplateAsync(id, User.GetUserId(), false);
            return NoContent();
        }

        [HttpPost("instantiate")]
        public async Task<ActionResult<TaskDto>> Instantiate([FromBody] CreateTaskFromTemplateRequest request)
        {
            await EnsurePermissionAsync(Permissions.TemplatesUse, "You do not have permission to use templates.");
            return Ok(await _instantiationService.CreateTaskFromTemplateAsync(request, User.GetUserId(), User.GetRole()));
        }
    }
}

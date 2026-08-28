using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.DTOs.Templates;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Phase 40 — every action derives the caller from User.GetUserId()/User.GetRole(), never a
    /// query/body-supplied identity (same IDOR-safe convention every other Phase 32+ controller
    /// uses). Feature-level gates (Permissions.TemplatesCreate/Use) are checked here via
    /// IPermissionService; RESOURCE-level authorization (can THIS caller edit/delete/share THIS
    /// specific template) is enforced inside ITemplateService/ITemplateInstantiationService, never
    /// trusted from the request alone — see TemplateService's own doc comment.
    /// </summary>
    [ApiController]
    [Route("api/project-templates")]
    [Authorize]
    public class ProjectTemplatesController(
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
        public async Task<ActionResult<ProjectTemplateDto>> Get(Guid id)
        {
            await EnsurePermissionAsync(Permissions.TemplatesView, "You do not have permission to view templates.");
            return Ok(await _templateService.GetProjectTemplateAsync(id, User.GetUserId(), User.GetRole()));
        }

        [HttpPost]
        public async Task<ActionResult<ProjectTemplateDto>> Create([FromBody] SaveProjectTemplateRequest request)
        {
            await EnsurePermissionAsync(Permissions.TemplatesCreate, "You do not have permission to create templates.");
            var result = await _templateService.SaveProjectTemplateAsync(null, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }

        /// <summary>No feature-level permission gate — SaveProjectTemplateAsync's own
        /// EnsureCanModify (owner or Administrator) is the real authorization boundary for editing
        /// an existing template, same as SavedReportService's own precedent (Phase 38).</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ProjectTemplateDto>> Update(Guid id, [FromBody] SaveProjectTemplateRequest request)
        {
            return Ok(await _templateService.SaveProjectTemplateAsync(id, request, User.GetUserId(), User.GetRole()));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _templateService.DeleteProjectTemplateAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/archive")]
        public async Task<ActionResult<ProjectTemplateDto>> Archive(Guid id)
        {
            return Ok(await _templateService.SetProjectTemplateArchivedAsync(id, true, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/restore")]
        public async Task<ActionResult<ProjectTemplateDto>> Restore(Guid id)
        {
            return Ok(await _templateService.SetProjectTemplateArchivedAsync(id, false, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/duplicate")]
        public async Task<ActionResult<ProjectTemplateDto>> Duplicate(Guid id)
        {
            await EnsurePermissionAsync(Permissions.TemplatesCreate, "You do not have permission to create templates.");
            return Ok(await _templateService.DuplicateProjectTemplateAsync(id, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/share")]
        public async Task<IActionResult> Share(Guid id, [FromBody] ShareTemplateRequest request)
        {
            await _templateService.ShareProjectTemplateAsync(id, User.GetUserId(), User.GetRole(), request.Email);
            return NoContent();
        }

        [HttpDelete("{id:guid}/share/{userId:guid}")]
        public async Task<IActionResult> Unshare(Guid id, Guid userId)
        {
            await _templateService.UnshareProjectTemplateAsync(id, User.GetUserId(), User.GetRole(), userId);
            return NoContent();
        }

        [HttpPost("{id:guid}/favorite")]
        public async Task<IActionResult> Favorite(Guid id)
        {
            await _templateService.FavoriteProjectTemplateAsync(id, User.GetUserId(), true);
            return NoContent();
        }

        [HttpDelete("{id:guid}/favorite")]
        public async Task<IActionResult> Unfavorite(Guid id)
        {
            await _templateService.FavoriteProjectTemplateAsync(id, User.GetUserId(), false);
            return NoContent();
        }

        /// <summary>JSON only (spec's own stated preference) — the exported DTO is the exact same
        /// read model the UI already renders, so it can never contain a password/token/credential
        /// (spec #53) without also being a bug in the template detail view itself.</summary>
        [HttpGet("{id:guid}/export")]
        public async Task<IActionResult> Export(Guid id)
        {
            await EnsurePermissionAsync(Permissions.TemplatesView, "You do not have permission to view templates.");
            var json = await _templateService.ExportProjectTemplateJsonAsync(id, User.GetUserId(), User.GetRole());
            return File(Encoding.UTF8.GetBytes(json), "application/json", "project-template.json");
        }

        [HttpGet("{id:guid}/preview")]
        public async Task<ActionResult<ProjectTemplatePreviewDto>> Preview(Guid id, [FromQuery] DateOnly startDate)
        {
            await EnsurePermissionAsync(Permissions.TemplatesUse, "You do not have permission to use templates.");
            return Ok(await _instantiationService.PreviewProjectTemplateAsync(id, startDate, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("instantiate")]
        public async Task<ActionResult<ProjectDto>> Instantiate([FromBody] CreateProjectFromTemplateRequest request)
        {
            await EnsurePermissionAsync(Permissions.TemplatesUse, "You do not have permission to use templates.");
            return Ok(await _instantiationService.CreateProjectFromTemplateAsync(request, User.GetUserId(), User.GetRole()));
        }
    }
}

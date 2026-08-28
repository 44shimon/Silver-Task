using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Templates;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Phase 40 — the Template Home's unified list (both ProjectTemplate and
    /// TaskTemplate, one flat list matching the spec's own mockup). Per-type CRUD/instantiation
    /// lives on ProjectTemplatesController/TaskTemplatesController; this controller is
    /// deliberately just the combined read.</summary>
    [ApiController]
    [Route("api/templates")]
    [Authorize]
    public class TemplatesController(ITemplateService templateService, IPermissionService permissionService) : ControllerBase
    {
        private readonly ITemplateService _templateService = templateService;
        private readonly IPermissionService _permissionService = permissionService;

        [HttpGet]
        public async Task<ActionResult<List<TemplateSummaryDto>>> List()
        {
            var permissions = await _permissionService.GetSystemPermissionsAsync(User.GetRole());
            if (!permissions.Contains(Permissions.TemplatesView))
            {
                throw new ForbiddenException("You do not have permission to view templates.");
            }
            return Ok(await _templateService.ListForCallerAsync(User.GetUserId(), User.GetRole()));
        }
    }
}

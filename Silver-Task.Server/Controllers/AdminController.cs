using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.Admin;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Groups admin-only concerns that don't cleanly belong to a single existing
    /// resource controller (cross-entity statistics, permanent project deletion). Everything
    /// else the Admin area needs (users, project archive/restore/members) reuses the existing
    /// UsersController/ProjectsController endpoints rather than duplicating them here.</summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminController(IAdminService adminService, IProjectService projectService) : ControllerBase
    {
        private readonly IAdminService _adminService = adminService;
        private readonly IProjectService _projectService = projectService;

        [HttpGet("stats")]
        public async Task<ActionResult<AdminStatsDto>> GetStats()
        {
            return Ok(await _adminService.GetStatsAsync());
        }

        /// <summary>Permanent delete, distinct from the regular archive-only DELETE on
        /// ProjectsController — an Administrator-only capability, not exposed to project
        /// owners/managers, per the existing "archive, don't hard-delete" design for everyone else.</summary>
        [HttpDelete("projects/{id:guid}")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            await _projectService.DeleteAsync(id);
            return NoContent();
        }
    }
}

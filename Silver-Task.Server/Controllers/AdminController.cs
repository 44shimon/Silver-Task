using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Admin;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Groups admin-only concerns that don't cleanly belong to a single existing
    /// resource controller (cross-entity statistics, permanent project deletion, user deletion
    /// impact/deletion). Everything else the Admin area needs (users, project archive/restore/
    /// members) reuses the existing UsersController/ProjectsController endpoints rather than
    /// duplicating them here.</summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminController(IAdminService adminService, IProjectService projectService, IUserService userService) : ControllerBase
    {
        private readonly IAdminService _adminService = adminService;
        private readonly IProjectService _projectService = projectService;
        private readonly IUserService _userService = userService;

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

        /// <summary>Fetched by the delete-user confirmation dialog before the admin can even
        /// enable the Delete button — see UserDeletionImpactDto.</summary>
        [HttpGet("users/{id:guid}/deletion-impact")]
        public async Task<ActionResult<UserDeletionImpactDto>> GetUserDeletionImpact(Guid id)
        {
            return Ok(await _adminService.GetUserDeletionImpactAsync(id));
        }

        /// <summary>Soft delete — see User.IsDeleted / UserService.DeleteAsync. Never trusts the
        /// frontend's confirmation UI for authorization; the [Authorize(Roles=Administrator)]
        /// class attribute and the self/last-administrator guards inside UserService.DeleteAsync
        /// are what actually enforce this.</summary>
        [HttpDelete("users/{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            await _userService.DeleteAsync(id, User.GetUserId());
            return NoContent();
        }
    }
}

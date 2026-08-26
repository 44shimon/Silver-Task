using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Admin;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Read-only "who can do what" viewer for Admin -> Roles & Permissions. Backs the
    /// spec's role/permission/assigned-users matrix display. There is deliberately no POST/PUT/
    /// DELETE here — the permission matrix itself is fixed, code-defined configuration (see
    /// PermissionService's doc comment); what's admin-editable is which role a user or project
    /// membership has (UsersController.Update / ProjectsController.SetMemberRole), not what each
    /// role means.</summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminRolesController(IPermissionService permissionService, AppDbContext db) : ControllerBase
    {
        private readonly IPermissionService _permissionService = permissionService;
        private readonly AppDbContext _db = db;

        [HttpGet("roles")]
        public async Task<ActionResult<IReadOnlyList<RoleInfoDto>>> GetSystemRoles()
        {
            var counts = await _db.Users
                .Where(u => !u.IsDeleted)
                .GroupBy(u => u.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Role, x => x.Count);

            var roles = new List<RoleInfoDto>();
            foreach (var role in Enum.GetValues<UserRole>())
            {
                var permissions = await _permissionService.GetSystemPermissionsAsync(role);
                roles.Add(new RoleInfoDto
                {
                    Name = role.ToString(),
                    Permissions = [.. permissions],
                    UserCount = counts.GetValueOrDefault(role)
                });
            }

            return Ok(roles);
        }

        [HttpGet("project-roles")]
        public async Task<ActionResult<IReadOnlyList<RoleInfoDto>>> GetProjectRoles()
        {
            var counts = await _db.ProjectMembers
                .GroupBy(m => m.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Role, x => x.Count);

            var roles = Enum.GetValues<ProjectRole>().Select(role => new RoleInfoDto
            {
                Name = role.ToString(),
                Permissions = [.. _permissionService.ProjectMatrix[role]],
                UserCount = counts.GetValueOrDefault(role)
            }).ToList();

            return Ok(roles);
        }
    }
}

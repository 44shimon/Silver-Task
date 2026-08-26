using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Shared project-membership authorization rules — the single place every other service goes
    /// through so project access can never drift out of sync between resources. Three tiers
    /// (Phase 32, was two before Project Roles existed):
    ///
    ///   View   (EnsureCanParticipateAsync) — Administrator, owner, or any member including a
    ///           Viewer. Read-only actions only.
    ///   Edit    (EnsureCanEditAsync)         — Administrator, owner, or a member whose
    ///           ProjectMember.Role is Manager or Member (not Viewer). Ordinary create/update
    ///           actions (tasks, comments, dependencies, recurring tasks, attachments).
    ///   Manage (EnsureCanManageAsync)       — Administrator, owner, or a member whose
    ///           ProjectMember.Role is specifically Manager. Project settings, membership,
    ///           custom field definitions, and (by system-setting default) task deletion.
    ///
    /// Before Phase 32, "Manage" was driven by the caller's *system-wide* UserRole == Manager —
    /// now it's driven by the caller's *project-scoped* ProjectRole, so a global Manager isn't
    /// automatically able to manage every project they're added to, and a global Member can be
    /// made a specific project's Manager. See ProjectMember.Role / ProjectRole's doc comments.
    /// </summary>
    public interface IProjectAccessService
    {
        Task<bool> IsMemberAsync(Guid projectId, Guid userId);

        /// <summary>Null if the user isn't a member of the project at all (and isn't the owner —
        /// callers that also need to treat the owner as an implicit Manager should check
        /// ownership separately, as EnsureCanEditAsync/EnsureCanManageAsync do).</summary>
        Task<ProjectRole?> GetProjectRoleAsync(Guid projectId, Guid userId);

        /// <summary>View tier — Administrators, the project owner, or any member (including a
        /// Viewer) can view the project and its tasks. Never permits a write.</summary>
        Task EnsureCanParticipateAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole);

        /// <summary>Edit tier — ordinary create/update actions. Administrators and the owner
        /// always pass; a member passes only if their ProjectRole is Manager or Member (a Viewer
        /// never does, regardless of any "allow members to..." system setting, which only ever
        /// widens who among Manager/Member tiers can act — it never grants Viewers write access).</summary>
        Task EnsureCanEditAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole);

        /// <summary>Manage tier — project settings, membership, custom field definitions, and
        /// (by default) task deletion. Administrators and the owner always pass; a member passes
        /// only if their ProjectRole is specifically Manager.</summary>
        Task EnsureCanManageAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole);
    }

    public class ProjectAccessService(AppDbContext db) : IProjectAccessService
    {
        private readonly AppDbContext _db = db;

        public Task<bool> IsMemberAsync(Guid projectId, Guid userId) =>
            _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        public Task<ProjectRole?> GetProjectRoleAsync(Guid projectId, Guid userId) =>
            _db.ProjectMembers
                .Where(m => m.ProjectId == projectId && m.UserId == userId)
                .Select(m => (ProjectRole?)m.Role)
                .FirstOrDefaultAsync();

        public async Task EnsureCanParticipateAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || projectOwnerId == callerId)
            {
                return;
            }

            if (await IsMemberAsync(projectId, callerId))
            {
                return;
            }

            throw new ForbiddenException("You do not have access to this project.");
        }

        public async Task EnsureCanEditAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || projectOwnerId == callerId)
            {
                return;
            }

            var role = await GetProjectRoleAsync(projectId, callerId);
            if (role is ProjectRole.Manager or ProjectRole.Member)
            {
                return;
            }

            throw new ForbiddenException(role is null
                ? "You do not have access to this project."
                : "Viewers cannot make changes to this project.");
        }

        public async Task EnsureCanManageAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || projectOwnerId == callerId)
            {
                return;
            }

            var role = await GetProjectRoleAsync(projectId, callerId);
            if (role == ProjectRole.Manager)
            {
                return;
            }

            throw new ForbiddenException("You do not have permission to manage this project.");
        }
    }
}

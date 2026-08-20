using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Shared project-membership authorization rules, used by both ProjectService and
    /// TaskService so the two never drift out of sync on who's allowed to do what.
    /// </summary>
    public interface IProjectAccessService
    {
        Task<bool> IsMemberAsync(Guid projectId, Guid userId);

        /// <summary>Administrators, the project owner, or any member can view a project and create/edit its tasks.</summary>
        Task EnsureCanParticipateAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole);

        /// <summary>
        /// Administrators and the owner can always manage a project (rename/archive/members) or
        /// perform destructive task actions (delete). A Manager can do the same only while
        /// they're a member of that project — plain Members never can.
        /// </summary>
        Task EnsureCanManageAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole);
    }

    public class ProjectAccessService(AppDbContext db) : IProjectAccessService
    {
        private readonly AppDbContext _db = db;

        public Task<bool> IsMemberAsync(Guid projectId, Guid userId) =>
            _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

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

        public async Task EnsureCanManageAsync(Guid projectId, Guid projectOwnerId, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || projectOwnerId == callerId)
            {
                return;
            }

            if (callerRole == UserRole.Manager && await IsMemberAsync(projectId, callerId))
            {
                return;
            }

            throw new ForbiddenException("You do not have permission to manage this project.");
        }
    }
}

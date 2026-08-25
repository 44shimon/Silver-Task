using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IProjectService
    {
        Task<IReadOnlyList<Project>> GetAllForUserAsync(Guid callerId, UserRole callerRole, bool includeArchived = false);

        Task<Project> GetByIdAsync(Guid projectId, Guid callerId, UserRole callerRole);

        Task<Project> CreateAsync(CreateProjectRequest request, Guid ownerId, UserRole ownerRole);

        Task<Project> UpdateAsync(Guid projectId, UpdateProjectRequest request, Guid callerId, UserRole callerRole);

        Task ArchiveAsync(Guid projectId, Guid callerId, UserRole callerRole);

        Task<Project> RestoreAsync(Guid projectId, Guid callerId, UserRole callerRole);

        /// <summary>Permanent delete — unlike ArchiveAsync (soft delete), this is only ever
        /// reachable from the Administrator-gated AdminController, so it takes no caller/role
        /// params and trusts the controller-level [Authorize(Roles = Administrator)] instead.</summary>
        Task DeleteAsync(Guid projectId);

        Task<IReadOnlyList<ProjectMember>> GetMembersAsync(Guid projectId, Guid callerId, UserRole callerRole);

        /// <summary>Null means no user has that email — a routine, expected outcome of typing an
        /// email that hasn't signed up yet (the caller/UI is responsible for explaining that),
        /// not an application error. Deliberately doesn't throw NotFoundException the way every
        /// other "not found" case in this app does, since this one is normal user input rather
        /// than a bug/bad-state signal (a stale id, a tampered request, etc.).</summary>
        Task<ProjectMember?> AddMemberAsync(Guid projectId, string email, Guid callerId, UserRole callerRole);

        Task RemoveMemberAsync(Guid projectId, Guid targetUserId, Guid callerId, UserRole callerRole);
    }

    public class ProjectService(
        AppDbContext db,
        IProjectAccessService projectAccess,
        ISystemSettingsService systemSettings,
        INotificationService notificationService) : IProjectService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly INotificationService _notificationService = notificationService;

        public async Task<IReadOnlyList<Project>> GetAllForUserAsync(Guid callerId, UserRole callerRole, bool includeArchived = false)
        {
            var query = _db.Projects.Include(p => p.Owner).Include(p => p.Members).AsQueryable();

            if (!includeArchived)
            {
                query = query.Where(p => !p.IsArchived);
            }

            if (callerRole != UserRole.Administrator)
            {
                query = query.Where(p => p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId));
            }

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<Project> GetByIdAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);
            return project;
        }

        public async Task<Project> CreateAsync(CreateProjectRequest request, Guid ownerId, UserRole ownerRole)
        {
            if (ownerRole != UserRole.Administrator && !await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowUsersToCreateProjects))
            {
                throw new ForbiddenException("Project creation is currently disabled by an Administrator.");
            }
            if (await _systemSettings.GetBoolAsync(SystemSettingKeys.RequireProjectDescription) && string.IsNullOrWhiteSpace(request.Description))
            {
                throw new ValidationException("A project description is required.");
            }

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = NormalizeDescription(request.Description),
                OwnerId = ownerId
            };
            _db.Projects.Add(project);

            // The owner is always implicitly a member, so they show up in member lists
            // and pass the "is a project member" checks like anyone else. Also appended to
            // project.Members directly (not just _db.ProjectMembers) so the in-memory MemberCount
            // on the DTO returned from this call is correct without a re-query.
            var ownerMembership = new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = ownerId
            };
            _db.ProjectMembers.Add(ownerMembership);
            project.Members.Add(ownerMembership);

            await _db.SaveChangesAsync();

            project.Owner = await _db.Users.FindAsync(ownerId);
            return project;
        }

        public async Task<Project> UpdateAsync(Guid projectId, UpdateProjectRequest request, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            project.Name = request.Name.Trim();
            project.Description = NormalizeDescription(request.Description);
            project.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return project;
        }

        public async Task ArchiveAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            if (project.IsArchived)
            {
                return;
            }

            project.IsArchived = true;
            project.ArchivedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task<Project> RestoreAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            if (!project.IsArchived)
            {
                return project;
            }

            project.IsArchived = false;
            project.ArchivedAt = null;
            project.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return project;
        }

        public async Task DeleteAsync(Guid projectId)
        {
            var project = await LoadProjectAsync(projectId);
            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<ProjectMember>> GetMembersAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            return await _db.ProjectMembers
                .Include(m => m.User)
                .Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.User!.Name)
                .ToListAsync();
        }

        public async Task<ProjectMember?> AddMemberAsync(Guid projectId, string email, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user is null)
            {
                return null;
            }

            var alreadyMember = await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == user.Id);
            if (alreadyMember)
            {
                throw new ConflictException($"'{user.Email}' is already a member of this project.");
            }

            var member = new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = user.Id
            };
            _db.ProjectMembers.Add(member);

            await _notificationService.NotifyAsync(
                user.Id, callerId, NotificationTypes.UserAddedToProject, "Added to project",
                $"You were added to \"{project.Name}\".", null, project.Id);

            await _db.SaveChangesAsync();

            member.User = user;
            return member;
        }

        public async Task RemoveMemberAsync(Guid projectId, Guid targetUserId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            if (targetUserId == project.OwnerId)
            {
                throw new ConflictException("The project owner cannot be removed from the project.");
            }

            var member = await _db.ProjectMembers.SingleOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == targetUserId)
                ?? throw new NotFoundException("That user is not a member of this project.");

            _db.ProjectMembers.Remove(member);

            await _notificationService.NotifyAsync(
                targetUserId, callerId, NotificationTypes.UserRemovedFromProject, "Removed from project",
                $"You were removed from \"{project.Name}\".", null, project.Id);

            await _db.SaveChangesAsync();
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.Include(p => p.Owner).Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        private static string? NormalizeDescription(string? description) =>
            string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}

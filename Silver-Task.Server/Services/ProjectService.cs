using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IProjectService
    {
        Task<IReadOnlyList<Project>> GetAllForUserAsync(Guid callerId, UserRole callerRole);

        Task<Project> GetByIdAsync(Guid projectId, Guid callerId, UserRole callerRole);

        Task<Project> CreateAsync(CreateProjectRequest request, Guid ownerId);

        Task<Project> UpdateAsync(Guid projectId, UpdateProjectRequest request, Guid callerId, UserRole callerRole);

        Task ArchiveAsync(Guid projectId, Guid callerId, UserRole callerRole);

        Task<IReadOnlyList<ProjectMember>> GetMembersAsync(Guid projectId, Guid callerId, UserRole callerRole);

        Task<ProjectMember> AddMemberAsync(Guid projectId, string email, Guid callerId, UserRole callerRole);

        Task RemoveMemberAsync(Guid projectId, Guid targetUserId, Guid callerId, UserRole callerRole);
    }

    public class ProjectService(AppDbContext db) : IProjectService
    {
        private readonly AppDbContext _db = db;

        public async Task<IReadOnlyList<Project>> GetAllForUserAsync(Guid callerId, UserRole callerRole)
        {
            var query = _db.Projects.Include(p => p.Owner).Where(p => !p.IsArchived);

            if (callerRole != UserRole.Administrator)
            {
                query = query.Where(p => p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId));
            }

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<Project> GetByIdAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await EnsureCanViewAsync(project, callerId, callerRole);
            return project;
        }

        public async Task<Project> CreateAsync(CreateProjectRequest request, Guid ownerId)
        {
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = NormalizeDescription(request.Description),
                OwnerId = ownerId
            };
            _db.Projects.Add(project);

            // The owner is always implicitly a member, so they show up in member lists
            // and pass the "is a project member" checks like anyone else.
            _db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = ownerId
            });

            await _db.SaveChangesAsync();

            project.Owner = await _db.Users.FindAsync(ownerId);
            return project;
        }

        public async Task<Project> UpdateAsync(Guid projectId, UpdateProjectRequest request, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await EnsureCanManageAsync(project, callerId, callerRole);

            project.Name = request.Name.Trim();
            project.Description = NormalizeDescription(request.Description);
            project.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return project;
        }

        public async Task ArchiveAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await EnsureCanManageAsync(project, callerId, callerRole);

            if (project.IsArchived)
            {
                return;
            }

            project.IsArchived = true;
            project.ArchivedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<ProjectMember>> GetMembersAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await EnsureCanViewAsync(project, callerId, callerRole);

            return await _db.ProjectMembers
                .Include(m => m.User)
                .Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.User!.Name)
                .ToListAsync();
        }

        public async Task<ProjectMember> AddMemberAsync(Guid projectId, string email, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await EnsureCanManageAsync(project, callerId, callerRole);

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail)
                ?? throw new NotFoundException($"No user found with email '{email}'.");

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
            await _db.SaveChangesAsync();

            member.User = user;
            return member;
        }

        public async Task RemoveMemberAsync(Guid projectId, Guid targetUserId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await EnsureCanManageAsync(project, callerId, callerRole);

            if (targetUserId == project.OwnerId)
            {
                throw new ConflictException("The project owner cannot be removed from the project.");
            }

            var member = await _db.ProjectMembers.SingleOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == targetUserId)
                ?? throw new NotFoundException("That user is not a member of this project.");

            _db.ProjectMembers.Remove(member);
            await _db.SaveChangesAsync();
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        private async Task<bool> IsMemberAsync(Guid projectId, Guid userId) =>
            await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        /// <summary>Administrators, the owner, or any member can view a project.</summary>
        private async Task EnsureCanViewAsync(Project project, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || project.OwnerId == callerId)
            {
                return;
            }

            if (await IsMemberAsync(project.Id, callerId))
            {
                return;
            }

            throw new ForbiddenException("You do not have access to this project.");
        }

        /// <summary>
        /// Administrators and the owner can always manage a project. A Manager can manage it
        /// only while they're a member of it — plain Members never can.
        /// </summary>
        private async Task EnsureCanManageAsync(Project project, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || project.OwnerId == callerId)
            {
                return;
            }

            if (callerRole == UserRole.Manager && await IsMemberAsync(project.Id, callerId))
            {
                return;
            }

            throw new ForbiddenException("You do not have permission to manage this project.");
        }

        private static string? NormalizeDescription(string? description) =>
            string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}

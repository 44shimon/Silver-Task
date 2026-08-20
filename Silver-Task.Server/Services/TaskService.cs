using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ITaskService
    {
        Task<IReadOnlyList<TaskItem>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole);

        Task<TaskItem> GetByIdAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskItem> CreateAsync(Guid projectId, CreateTaskRequest request, Guid callerId, UserRole callerRole);

        Task<TaskItem> UpdateAsync(Guid taskId, UpdateTaskRequest request, Guid callerId, UserRole callerRole);

        Task DeleteAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskItem> DuplicateAsync(Guid taskId, Guid callerId, UserRole callerRole);
    }

    public class TaskService(AppDbContext db, IProjectAccessService projectAccess) : ITaskService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;

        public async Task<IReadOnlyList<TaskItem>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            return await _db.Tasks
                .Include(t => t.AssignedTo)
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();
        }

        public async Task<TaskItem> GetByIdAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            return task;
        }

        public async Task<TaskItem> CreateAsync(Guid projectId, CreateTaskRequest request, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            if (request.AssignedToUserId is Guid assigneeId)
            {
                await EnsureAssigneeIsMemberAsync(projectId, assigneeId);
            }

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = request.Title.Trim(),
                Description = NormalizeText(request.Description),
                Status = request.Status,
                Priority = request.Priority,
                AssignedToUserId = request.AssignedToUserId,
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                CompletedAt = request.Status == TaskItemStatus.Complete ? DateTime.UtcNow : null,
                SortOrder = await GetNextSortOrderAsync(projectId)
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            return task;
        }

        public async Task<TaskItem> UpdateAsync(Guid taskId, UpdateTaskRequest request, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            if (request.AssignedToUserId is Guid assigneeId)
            {
                await EnsureAssigneeIsMemberAsync(task.ProjectId, assigneeId);
            }

            var wasComplete = task.Status == TaskItemStatus.Complete;
            var willBeComplete = request.Status == TaskItemStatus.Complete;

            task.Title = request.Title.Trim();
            task.Description = NormalizeText(request.Description);
            task.Status = request.Status;
            task.Priority = request.Priority;
            task.AssignedToUserId = request.AssignedToUserId;
            task.StartDate = request.StartDate;
            task.DueDate = request.DueDate;
            task.SortOrder = request.SortOrder;
            task.UpdatedAt = DateTime.UtcNow;

            if (willBeComplete && !wasComplete)
            {
                task.CompletedAt = DateTime.UtcNow;
            }
            else if (!willBeComplete && wasComplete)
            {
                task.CompletedAt = null;
            }

            await _db.SaveChangesAsync();

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            return task;
        }

        public async Task DeleteAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanManageAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync();
        }

        public async Task<TaskItem> DuplicateAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var original = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(original.ProjectId, original.Project!.OwnerId, callerId, callerRole);

            var copy = new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = original.ProjectId,
                Title = $"{original.Title} (Copy)",
                Description = original.Description,
                Status = original.Status,
                Priority = original.Priority,
                AssignedToUserId = original.AssignedToUserId,
                StartDate = original.StartDate,
                DueDate = original.DueDate,
                CompletedAt = null,
                SortOrder = await GetSortOrderAfterAsync(original)
            };

            _db.Tasks.Add(copy);
            await _db.SaveChangesAsync();

            copy.AssignedTo = original.AssignedTo;
            return copy;
        }

        private async Task<TaskItem> LoadTaskAsync(Guid taskId)
        {
            var task = await _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId);
            return task ?? throw new NotFoundException($"Task '{taskId}' was not found.");
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        private async Task EnsureAssigneeIsMemberAsync(Guid projectId, Guid assigneeId)
        {
            if (!await _projectAccess.IsMemberAsync(projectId, assigneeId))
            {
                throw new ValidationException("The assigned user must be a member of this project.");
            }
        }

        private async Task<double> GetNextSortOrderAsync(Guid projectId)
        {
            var maxSortOrder = await _db.Tasks
                .Where(t => t.ProjectId == projectId)
                .Select(t => (double?)t.SortOrder)
                .MaxAsync();
            return (maxSortOrder ?? 0) + 1;
        }

        /// <summary>Fractional-index insertion point immediately after <paramref name="task"/>, so a duplicate lands next to its source instead of at the bottom of the list.</summary>
        private async Task<double> GetSortOrderAfterAsync(TaskItem task)
        {
            var nextSortOrder = await _db.Tasks
                .Where(t => t.ProjectId == task.ProjectId && t.SortOrder > task.SortOrder)
                .OrderBy(t => t.SortOrder)
                .Select(t => (double?)t.SortOrder)
                .FirstOrDefaultAsync();

            return nextSortOrder is double next ? (task.SortOrder + next) / 2 : task.SortOrder + 1;
        }

        private static string? NormalizeText(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}

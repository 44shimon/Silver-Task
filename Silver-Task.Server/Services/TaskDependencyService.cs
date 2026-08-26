using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ITaskDependencyService
    {
        /// <summary>The "Depends On" list — prerequisites of taskId.</summary>
        Task<IReadOnlyList<TaskDependency>> GetDependenciesAsync(Guid taskId, Guid callerId, UserRole callerRole);

        /// <summary>The "Blocking" list — tasks that depend on taskId.</summary>
        Task<IReadOnlyList<TaskDependency>> GetDependentsAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskDependency> CreateAsync(Guid taskId, Guid dependsOnTaskId, Guid callerId, UserRole callerRole);

        Task DeleteAsync(Guid taskId, Guid dependencyId, Guid callerId, UserRole callerRole);

        /// <summary>Every dependency edge in a project as bare (TaskId, DependsOnTaskId) pairs —
        /// backs the Gantt/Timeline dependency-line rendering, which already has full Task data
        /// for every visible row and only needs to know which pairs are connected. One query for
        /// the whole project instead of one per visible bar.</summary>
        Task<IReadOnlyList<(Guid TaskId, Guid DependsOnTaskId)>> GetProjectEdgesAsync(Guid projectId, Guid callerId, UserRole callerRole);
    }

    /// <summary>
    /// Same participate-tier permission every other task edit uses (TaskService.UpdateAsync) —
    /// a dependency is just another property of the task relationship, not a more privileged
    /// concept, so anyone who can edit the task can manage its dependencies.
    /// </summary>
    public class TaskDependencyService(
        AppDbContext db,
        IProjectAccessService projectAccess,
        INotificationService notificationService) : ITaskDependencyService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly INotificationService _notificationService = notificationService;

        public async Task<IReadOnlyList<TaskDependency>> GetDependenciesAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            return await _db.TaskDependencies
                .Include(d => d.DependsOnTask).ThenInclude(t => t!.AssignedTo)
                .Where(d => d.TaskId == taskId)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TaskDependency>> GetDependentsAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            return await _db.TaskDependencies
                .Include(d => d.Task).ThenInclude(t => t!.AssignedTo)
                .Where(d => d.DependsOnTaskId == taskId)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<TaskDependency> CreateAsync(Guid taskId, Guid dependsOnTaskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            if (taskId == dependsOnTaskId)
            {
                throw new ValidationException("A task cannot depend on itself.");
            }

            var prerequisite = await LoadTaskAsync(dependsOnTaskId);

            if (prerequisite.ProjectId != task.ProjectId)
            {
                throw new ValidationException("Tasks must belong to the same project.");
            }

            var alreadyExists = await _db.TaskDependencies
                .AnyAsync(d => d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId);
            if (alreadyExists)
            {
                throw new ConflictException("This dependency already exists.");
            }

            if (await WouldCreateCycleAsync(task.ProjectId, taskId, dependsOnTaskId))
            {
                throw new ValidationException("Cannot create dependency because it would create a circular dependency.");
            }

            var dependency = new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                DependsOnTaskId = dependsOnTaskId,
                DependencyType = DependencyTypes.FinishToStart,
                CreatedByUserId = callerId
            };
            _db.TaskDependencies.Add(dependency);

            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "DependencyAdded",
                NewValue = prerequisite.Title
            });

            await _db.SaveChangesAsync();

            dependency.DependsOnTask = prerequisite;
            return dependency;
        }

        public async Task DeleteAsync(Guid taskId, Guid dependencyId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            var dependency = await _db.TaskDependencies
                .Include(d => d.DependsOnTask)
                .FirstOrDefaultAsync(d => d.Id == dependencyId && d.TaskId == taskId)
                ?? throw new NotFoundException($"Dependency '{dependencyId}' was not found.");

            _db.TaskDependencies.Remove(dependency);

            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "DependencyRemoved",
                OldValue = dependency.DependsOnTask?.Title
            });

            await _db.SaveChangesAsync();
        }

        /// <summary>Would adding an edge (taskId depends on dependsOnTaskId) close a cycle? True
        /// exactly when dependsOnTaskId can already (transitively) reach taskId by following
        /// existing "depends on" edges — i.e. dependsOnTaskId already, directly or indirectly,
        /// depends on taskId. Loads the whole project's dependency graph in one query (not
        /// per-node), since dependencies are same-project-only — cheap even for a project with
        /// hundreds of tasks, and avoids N+1 entirely.</summary>
        private async Task<bool> WouldCreateCycleAsync(Guid projectId, Guid taskId, Guid dependsOnTaskId)
        {
            var edges = await _db.TaskDependencies
                .Where(d => d.Task!.ProjectId == projectId)
                .Select(d => new { d.TaskId, d.DependsOnTaskId })
                .ToListAsync();

            var adjacency = edges
                .GroupBy(e => e.TaskId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.DependsOnTaskId).ToList());

            var visited = new HashSet<Guid>();
            var stack = new Stack<Guid>();
            stack.Push(dependsOnTaskId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current == taskId)
                {
                    return true;
                }
                if (!visited.Add(current))
                {
                    continue;
                }
                if (adjacency.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        stack.Push(neighbor);
                    }
                }
            }

            return false;
        }

        public async Task<IReadOnlyList<(Guid TaskId, Guid DependsOnTaskId)>> GetProjectEdgesAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
                ?? throw new NotFoundException($"Project '{projectId}' was not found.");
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            var edges = await _db.TaskDependencies
                .Where(d => d.Task!.ProjectId == projectId)
                .Select(d => new { d.TaskId, d.DependsOnTaskId })
                .ToListAsync();

            return edges.Select(e => (e.TaskId, e.DependsOnTaskId)).ToList();
        }

        private async Task<TaskItem> LoadTaskAsync(Guid taskId)
        {
            var task = await _db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
            return task ?? throw new NotFoundException($"Task '{taskId}' was not found.");
        }
    }
}

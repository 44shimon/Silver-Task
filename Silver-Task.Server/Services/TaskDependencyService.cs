using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Automation;
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

        Task<TaskDependency> CreateAsync(Guid taskId, Guid dependsOnTaskId, string dependencyType, Guid callerId, UserRole callerRole);

        Task DeleteAsync(Guid taskId, Guid dependencyId, Guid callerId, UserRole callerRole);

        /// <summary>Every dependency edge in a project as bare (TaskId, DependsOnTaskId) pairs —
        /// backs the Gantt/Timeline dependency-line rendering, which already has full Task data
        /// for every visible row and only needs to know which pairs are connected. One query for
        /// the whole project instead of one per visible bar.</summary>
        Task<IReadOnlyList<(Guid TaskId, Guid DependsOnTaskId)>> GetProjectEdgesAsync(Guid projectId, Guid callerId, UserRole callerRole);

        /// <summary>Prerequisites of taskId whose relationship rule currently blocks taskId from
        /// STARTING (see TaskDependencyService's own doc comment for the exact per-type rule).
        /// Empty means taskId is Ready. Used both for the computed Blocked badge/count and for
        /// TaskService.UpdateAsync's backend enforcement when a task leaves NotStarted.</summary>
        Task<IReadOnlyList<TaskDependency>> GetStartBlockersAsync(Guid taskId);

        /// <summary>Prerequisites of taskId whose relationship rule currently blocks taskId from
        /// being marked COMPLETE — checked in addition to (not instead of) start-blockers, since a
        /// Finish-to-Finish/Start-to-Finish dependency only ever gates completion, never starting.</summary>
        Task<IReadOnlyList<TaskDependency>> GetCompletionBlockersAsync(Guid taskId);
    }

    /// <summary>
    /// Same participate-tier permission every other task edit uses (TaskService.UpdateAsync) —
    /// a dependency is just another property of the task relationship, not a more privileged
    /// concept, so anyone who can edit the task can manage its dependencies. Bypassing an actual
    /// dependency BLOCK (once one exists) is a separate, more privileged action gated by
    /// Permissions.DependenciesOverride — see TaskService.UpdateAsync's own doc comment.
    ///
    /// Phase 39 — the exact satisfaction rule per DependencyType, applied uniformly everywhere a
    /// task's blocked/ready state is computed (this service's own blocker queries, and
    /// TaskService.AttachDependencySummaryAsync's bulk BlockedByCount aggregate):
    ///
    ///   Type            | Blocks STARTING when...        | Blocks COMPLETING when...
    ///   FinishToStart    | prerequisite != Complete        | prerequisite != Complete
    ///   StartToStart     | prerequisite hasn't started      | never
    ///   FinishToFinish   | never                            | prerequisite != Complete
    ///   StartToFinish    | never                            | prerequisite hasn't started
    ///
    /// "Started" = prerequisite.Status is anything other than NotStarted or Cancelled — an
    /// objective, current-state fact, never an invented historical timestamp (this app has no
    /// reliable "started at" moment recorded anywhere; see DependencyTypes' own doc comment).
    /// </summary>
    public class TaskDependencyService(
        AppDbContext db,
        IProjectAccessService projectAccess,
        INotificationService notificationService,
        IAutomationDispatcher automationDispatcher) : ITaskDependencyService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly INotificationService _notificationService = notificationService;
        private readonly IAutomationDispatcher _automationDispatcher = automationDispatcher;

        public static bool IsSatisfiedForStart(string dependencyType, TaskItemStatus prerequisiteStatus) => dependencyType switch
        {
            DependencyTypes.FinishToStart => prerequisiteStatus == TaskItemStatus.Complete,
            DependencyTypes.StartToStart => HasStarted(prerequisiteStatus),
            _ => true
        };

        public static bool IsSatisfiedForCompletion(string dependencyType, TaskItemStatus prerequisiteStatus) => dependencyType switch
        {
            DependencyTypes.FinishToStart => prerequisiteStatus == TaskItemStatus.Complete,
            DependencyTypes.FinishToFinish => prerequisiteStatus == TaskItemStatus.Complete,
            DependencyTypes.StartToFinish => HasStarted(prerequisiteStatus),
            _ => true
        };

        /// <summary>Whether THIS relationship's own defining condition currently holds —
        /// independent of whether that condition gates starting or completing (see
        /// IsSatisfiedForStart/IsSatisfiedForCompletion, which are direction-specific and return
        /// `true` for a type that simply doesn't gate that direction). This is what the Task
        /// Detail dependency panel's per-row satisfied/unsatisfied indicator reflects.</summary>
        public static bool IsRelationshipSatisfied(string dependencyType, TaskItemStatus prerequisiteStatus) => dependencyType switch
        {
            DependencyTypes.FinishToStart or DependencyTypes.FinishToFinish => prerequisiteStatus == TaskItemStatus.Complete,
            DependencyTypes.StartToStart or DependencyTypes.StartToFinish => HasStarted(prerequisiteStatus),
            _ => true
        };

        private static bool HasStarted(TaskItemStatus status) => status != TaskItemStatus.NotStarted && status != TaskItemStatus.Cancelled;

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

            // DependsOnTask is included too (not just Task) — it's always taskId itself here, but
            // the mapping extension (ToDependentDto) needs its current Status to compute whether
            // the dependent's own relationship is currently satisfied, symmetric to how
            // GetDependenciesAsync already gives ToDependsOnDto everything it needs.
            return await _db.TaskDependencies
                .Include(d => d.Task).ThenInclude(t => t!.AssignedTo)
                .Include(d => d.DependsOnTask)
                .Where(d => d.DependsOnTaskId == taskId)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<TaskDependency> CreateAsync(Guid taskId, Guid dependsOnTaskId, string dependencyType, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            if (taskId == dependsOnTaskId)
            {
                throw new ValidationException("A task cannot depend on itself.");
            }

            if (!DependencyTypes.All.Contains(dependencyType))
            {
                throw new ValidationException("Unrecognized dependency type.");
            }

            var prerequisite = await LoadTaskAsync(dependsOnTaskId);

            if (prerequisite.ProjectId != task.ProjectId)
            {
                throw new ValidationException("Tasks must belong to the same project — cross-project dependencies are not supported.");
            }

            var alreadyExists = await _db.TaskDependencies
                .AnyAsync(d => d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId && d.DependencyType == dependencyType);
            if (alreadyExists)
            {
                throw new ConflictException("This dependency already exists.");
            }

            if (await WouldCreateCycleAsync(task.ProjectId, taskId, dependsOnTaskId))
            {
                throw new ValidationException("This dependency would create a circular workflow.");
            }

            var dependency = new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                DependsOnTaskId = dependsOnTaskId,
                DependencyType = dependencyType,
                CreatedByUserId = callerId
            };
            _db.TaskDependencies.Add(dependency);

            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "DependencyAdded",
                NewValue = $"{prerequisite.Title} ({dependencyType})"
            });

            await _db.SaveChangesAsync();

            dependency.DependsOnTask = prerequisite;

            await _automationDispatcher.DispatchAsync(new DependencyAddedEvent(taskId, dependsOnTaskId, task.ProjectId, callerId, DateTime.UtcNow));

            // If this new edge immediately blocks the dependent task from starting (the
            // prerequisite doesn't yet satisfy the relationship), tell the dependent's assignee
            // right away — the spec's own "Final Inspection is blocked by Electrical Inspection"
            // example — rather than leaving them to discover it later.
            if (!IsSatisfiedForStart(dependencyType, prerequisite.Status) && task.AssignedToUserId is Guid assigneeId)
            {
                await _notificationService.NotifyAsync(
                    assigneeId, callerId, NotificationTypes.TaskBecameBlocked, "Task blocked",
                    $"\"{task.Title}\" is blocked by \"{prerequisite.Title}\".", task.Id, task.ProjectId);
                await _automationDispatcher.DispatchAsync(new TaskBecameBlockedEvent(taskId, task.ProjectId, callerId, DateTime.UtcNow));
            }

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

            var dependsOnTaskId = dependency.DependsOnTaskId;
            var wasSatisfiedForStart = dependency.DependsOnTask is null ||
                IsSatisfiedForStart(dependency.DependencyType, dependency.DependsOnTask.Status);

            _db.TaskDependencies.Remove(dependency);

            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "DependencyRemoved",
                OldValue = $"{dependency.DependsOnTask?.Title} ({dependency.DependencyType})"
            });

            await _db.SaveChangesAsync();

            await _automationDispatcher.DispatchAsync(new DependencyRemovedEvent(taskId, dependsOnTaskId, task.ProjectId, callerId, DateTime.UtcNow));

            // Removing an edge that was itself the last thing blocking this task from starting
            // means the task just became Ready — same "tell the assignee" treatment as completing
            // the prerequisite normally would (see TaskService.NotifyReadyDependentsAsync), so
            // this doesn't become a silent way to unblock a task without anyone finding out.
            if (!wasSatisfiedForStart)
            {
                var remainingBlockers = await GetStartBlockersAsync(taskId);
                if (remainingBlockers.Count == 0)
                {
                    await _automationDispatcher.DispatchAsync(new TaskBecameReadyEvent(taskId, task.ProjectId, callerId, DateTime.UtcNow));
                    if (task.AssignedToUserId is Guid assigneeId)
                    {
                        await _notificationService.NotifyAsync(
                            assigneeId, callerId, NotificationTypes.TaskDependencyCompleted, "Task ready",
                            $"\"{task.Title}\" is no longer blocked.", task.Id, task.ProjectId);
                    }
                }
            }
        }

        /// <summary>Would adding an edge (taskId depends on dependsOnTaskId) close a cycle? True
        /// exactly when dependsOnTaskId can already (transitively) reach taskId by following
        /// existing "depends on" edges — i.e. dependsOnTaskId already, directly or indirectly,
        /// depends on taskId. Loads the whole project's dependency graph in one query (not
        /// per-node), since dependencies are same-project-only — cheap even for a project with
        /// hundreds of tasks, and avoids N+1 entirely. Every DependencyType edge participates in
        /// this check identically — a cycle is a cycle regardless of which relationship types form
        /// it.</summary>
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

        public async Task<IReadOnlyList<TaskDependency>> GetStartBlockersAsync(Guid taskId)
        {
            var rows = await _db.TaskDependencies
                .Include(d => d.DependsOnTask)
                .Where(d => d.TaskId == taskId)
                .ToListAsync();

            return rows.Where(d => !IsSatisfiedForStart(d.DependencyType, d.DependsOnTask!.Status)).ToList();
        }

        public async Task<IReadOnlyList<TaskDependency>> GetCompletionBlockersAsync(Guid taskId)
        {
            var rows = await _db.TaskDependencies
                .Include(d => d.DependsOnTask)
                .Where(d => d.TaskId == taskId)
                .ToListAsync();

            return rows.Where(d => !IsSatisfiedForCompletion(d.DependencyType, d.DependsOnTask!.Status)).ToList();
        }

        private async Task<TaskItem> LoadTaskAsync(Guid taskId)
        {
            var task = await _db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
            return task ?? throw new NotFoundException($"Task '{taskId}' was not found.");
        }
    }
}

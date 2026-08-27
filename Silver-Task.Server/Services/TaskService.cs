using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Automation;
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

        /// <summary>Task count per project, for the Admin Projects list — a single grouped
        /// aggregate query rather than one count query per project.</summary>
        Task<Dictionary<Guid, int>> GetTaskCountsByProjectAsync(IEnumerable<Guid> projectIds);

        /// <summary>Global search across every project the caller can access — backs the
        /// Topbar search. Runs entirely in PostgreSQL (case-insensitive ILIKE across title,
        /// description, project name, assignee name, and Text/LongText custom values) and
        /// returns at most <paramref name="limit"/> rows, so this never pulls a whole task
        /// table's worth of data to the browser just to filter it client-side.</summary>
        Task<IReadOnlyList<TaskItem>> SearchAsync(string query, Guid callerId, UserRole callerRole, int limit = 25);

        /// <summary>Every task assigned to the caller across all of their (non-archived) projects —
        /// backs the "My Tasks" dashboard. A single indexed query, not one call per project.</summary>
        Task<IReadOnlyList<TaskItem>> GetAssignedToUserAsync(Guid callerId, UserRole callerRole);

        Task<TaskItem> GetByIdAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskItem> CreateAsync(Guid projectId, CreateTaskRequest request, Guid callerId, UserRole callerRole);

        Task<TaskItem> UpdateAsync(Guid taskId, UpdateTaskRequest request, Guid callerId, UserRole callerRole);

        /// <param name="deleteSubtasks">False (default): direct children are reparented to this
        /// task's own parent (grandparent) and preserved — "delete task only". True: the entire
        /// subtree is removed in the same transaction — "delete task and all subtasks". Never a
        /// silent, unconfirmed cascade; the caller (controller) always gets this from an explicit
        /// query parameter the frontend only sets after the user picks one of the two options.</param>
        Task DeleteAsync(Guid taskId, bool deleteSubtasks, Guid callerId, UserRole callerRole);

        Task<TaskItem> DuplicateAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskItem> SetCustomValueAsync(Guid taskId, Guid customFieldId, string? value, Guid callerId, UserRole callerRole);

        Task<IReadOnlyList<TaskActivity>> GetActivitiesForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole);

        /// <summary>Direct children only (not the full recursive subtree) — matches
        /// GET .../subtasks. Mainly useful outside a project page (e.g. My Tasks), which doesn't
        /// already have the parent's sibling set loaded the way ProjectPage does.</summary>
        Task<IReadOnlyList<TaskItem>> GetSubtasksAsync(Guid taskId, Guid callerId, UserRole callerRole);

        /// <summary>ProjectId and ParentTaskId are always resolved from the parent task server-side
        /// — never trusted from the request body, same "don't trust the frontend for ownership"
        /// rule CreateAsync already follows for ProjectId.</summary>
        Task<TaskItem> CreateSubtaskAsync(Guid parentTaskId, CreateTaskRequest request, Guid callerId, UserRole callerRole);

        /// <summary>The "Move Task" action — null parentTaskId moves the task to top level.
        /// Validates same-project, no self-parent, no circular hierarchy, and the nesting depth
        /// limit; never actually creates or removes a TaskDependency row (hierarchy and
        /// dependencies are deliberately separate concepts).</summary>
        Task<TaskItem> SetParentAsync(Guid taskId, Guid? parentTaskId, Guid callerId, UserRole callerRole);

        Task<TaskItem> SetSortOrderAsync(Guid taskId, double sortOrder, Guid callerId, UserRole callerRole);

        /// <summary>"Labels" (Phase 35) — reuses the same global Tag vocabulary Phase 34
        /// introduced for files (see TaskTag's own doc comment), rather than a second label
        /// system. Get-or-create by name, same as AttachmentService.AddTagAsync.</summary>
        Task<IReadOnlyList<Tag>> GetLabelsAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<Tag> AddLabelAsync(Guid taskId, string tagName, Guid callerId, UserRole callerRole);

        Task RemoveLabelAsync(Guid taskId, Guid tagId, Guid callerId, UserRole callerRole);
    }

    public class TaskService(
        AppDbContext db,
        IProjectAccessService projectAccess,
        ISystemSettingsService systemSettings,
        INotificationService notificationService,
        ITagService tagService,
        IAutomationDispatcher automationDispatcher,
        ITaskDependencyService dependencyService) : ITaskService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly INotificationService _notificationService = notificationService;
        private readonly ITagService _tagService = tagService;
        private readonly IAutomationDispatcher _automationDispatcher = automationDispatcher;
        private readonly ITaskDependencyService _dependencyService = dependencyService;

        public async Task<IReadOnlyList<TaskItem>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            var tasks = await _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.CustomValues)
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            await AttachTaskSummariesAsync(tasks);
            return tasks;
        }

        public async Task<Dictionary<Guid, int>> GetTaskCountsByProjectAsync(IEnumerable<Guid> projectIds)
        {
            var ids = projectIds.ToList();
            return await _db.Tasks
                .Where(t => ids.Contains(t.ProjectId))
                .GroupBy(t => t.ProjectId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
        }

        public async Task<IReadOnlyList<TaskItem>> SearchAsync(string query, Guid callerId, UserRole callerRole, int limit = 25)
        {
            var trimmed = query.Trim();
            if (trimmed.Length == 0)
            {
                return [];
            }

            var pattern = $"%{trimmed}%";

            var tasksQuery = _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Where(t =>
                    EF.Functions.ILike(t.Title, pattern) ||
                    (t.Description != null && EF.Functions.ILike(t.Description, pattern)) ||
                    EF.Functions.ILike(t.Project!.Name, pattern) ||
                    (t.AssignedTo != null && EF.Functions.ILike(t.AssignedTo.Name, pattern)) ||
                    t.CustomValues.Any(v =>
                        v.Value != null &&
                        (v.CustomField!.FieldType == CustomFieldType.Text || v.CustomField.FieldType == CustomFieldType.LongText) &&
                        EF.Functions.ILike(v.Value, pattern)));

            // Same "Administrator sees everything, everyone else only their own
            // owned/member projects" scoping as GetAssignedToUserAsync/ProjectService — a
            // global search still can't leak tasks from projects the caller can't see.
            if (callerRole != UserRole.Administrator)
            {
                tasksQuery = tasksQuery.Where(t =>
                    t.Project!.OwnerId == callerId || t.Project.Members.Any(m => m.UserId == callerId));
            }

            var results = await tasksQuery
                .OrderByDescending(t => t.UpdatedAt)
                .Take(limit)
                .ToListAsync();

            await AttachTaskSummariesAsync(results);
            return results;
        }

        public async Task<IReadOnlyList<TaskItem>> GetAssignedToUserAsync(Guid callerId, UserRole callerRole)
        {
            // AssignedToUserId is already indexed (TaskItemConfiguration), and this stays a single
            // query translated to one SQL join — no per-project round trips.
            var query = _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Include(t => t.CustomValues)
                .Where(t => t.AssignedToUserId == callerId && !t.Project!.IsArchived);

            // Mirrors ProjectService.GetAllForUserAsync: an Administrator sees everything, everyone
            // else only sees assignments in projects they still own or are a member of — a task
            // assigned to the caller in a project they've since been removed from shouldn't leak in.
            if (callerRole != UserRole.Administrator)
            {
                query = query.Where(t =>
                    t.Project!.OwnerId == callerId || t.Project.Members.Any(m => m.UserId == callerId));
            }

            var tasks = await query
                .OrderBy(t => t.DueDate == null)
                .ThenBy(t => t.DueDate)
                .ThenBy(t => t.Title)
                .ToListAsync();

            await AttachTaskSummariesAsync(tasks);
            return tasks;
        }

        public async Task<TaskItem> GetByIdAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            await AttachTaskSummariesAsync([task]);
            return task;
        }

        public async Task<IReadOnlyList<TaskItem>> GetSubtasksAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            var subtasks = await _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.CustomValues)
                .Where(t => t.ParentTaskId == taskId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            await AttachTaskSummariesAsync(subtasks);
            return subtasks;
        }

        public async Task<TaskItem> CreateAsync(Guid projectId, CreateTaskRequest request, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanEditAsync(project.Id, project.OwnerId, callerId, callerRole);
            await EnsureCanCreateTasksAsync(project, callerId, callerRole);

            var task = await BuildNewTaskAsync(project, parentTaskId: null, request, callerId);
            await _db.SaveChangesAsync();

            await _automationDispatcher.DispatchAsync(new TaskCreatedEvent(task.Id, project.Id, callerId, DateTime.UtcNow));

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            return task;
        }

        public async Task<TaskItem> CreateSubtaskAsync(Guid parentTaskId, CreateTaskRequest request, Guid callerId, UserRole callerRole)
        {
            var parent = await LoadTaskAsync(parentTaskId);
            await _projectAccess.EnsureCanEditAsync(parent.ProjectId, parent.Project!.OwnerId, callerId, callerRole);
            await EnsureCanCreateTasksAsync(parent.Project!, callerId, callerRole);
            await EnsureDepthWithinLimitAsync(parentTaskId);

            var subtask = await BuildNewTaskAsync(parent.Project!, parentTaskId, request, callerId);

            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = parentTaskId,
                UserId = callerId,
                Action = "SubtaskAdded",
                NewValue = subtask.Title
            });

            await _db.SaveChangesAsync();

            await _automationDispatcher.DispatchAsync(new TaskCreatedEvent(subtask.Id, parent.ProjectId, callerId, DateTime.UtcNow));

            subtask.AssignedTo = subtask.AssignedToUserId is null ? null : await _db.Users.FindAsync(subtask.AssignedToUserId);
            return subtask;
        }

        /// <summary>Shared by CreateAsync (parentTaskId: null) and CreateSubtaskAsync — builds and
        /// tracks the new task, its "Created" activity, and the assignment notification, but
        /// deliberately does not call SaveChangesAsync so a caller adding one more entity (like
        /// CreateSubtaskAsync's "SubtaskAdded" activity on the parent) still persists everything
        /// in a single unit of work.</summary>
        private async Task<TaskItem> BuildNewTaskAsync(Project project, Guid? parentTaskId, CreateTaskRequest request, Guid callerId)
        {
            if (request.AssignedToUserId is Guid assigneeId)
            {
                await EnsureAssigneeIsMemberAsync(project.Id, assigneeId);
            }

            var status = request.Status ?? await ResolveDefaultStatusAsync();
            var priority = request.Priority ?? await ResolveDefaultPriorityAsync();

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentTaskId = parentTaskId,
                Title = request.Title.Trim(),
                Description = NormalizeText(request.Description),
                Status = status,
                Priority = priority,
                AssignedToUserId = request.AssignedToUserId,
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                CompletedAt = status == TaskItemStatus.Complete ? DateTime.UtcNow : null,
                SortOrder = await GetNextSortOrderAsync(project.Id, parentTaskId)
            };

            _db.Tasks.Add(task);
            _db.TaskActivities.Add(BuildActivity(task.Id, callerId, "Created", null, null, null));

            if (task.AssignedToUserId is Guid assignedOnCreateId)
            {
                var actorName = await _db.Users.Where(u => u.Id == callerId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Someone";
                var taskNoun = parentTaskId is null ? "Task" : "Subtask";
                await _notificationService.NotifyAsync(
                    assignedOnCreateId, callerId, NotificationTypes.TaskAssigned,
                    $"{taskNoun} assigned to you",
                    $"{actorName} assigned you \"{task.Title}\" in \"{project.Name}\".",
                    task.Id, project.Id);
            }

            return task;
        }

        // Plain project Members pass EnsureCanEditAsync the same as project Managers/the owner —
        // this extra check is the only place task creation can be narrowed further, to just
        // Manager-tier, via the "Allow members to create tasks" system setting. Applies
        // identically to subtasks. Driven by the caller's *project* role (Phase 32), not their
        // system-wide UserRole — a global Manager who's only a project Member for THIS project is
        // still subject to this setting the same as anyone else at project-Member tier.
        private async Task EnsureCanCreateTasksAsync(Project project, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || callerId == project.OwnerId)
            {
                return;
            }

            var projectRole = await _projectAccess.GetProjectRoleAsync(project.Id, callerId);
            if (projectRole == ProjectRole.Member &&
                !await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowMembersToCreateTasks))
            {
                throw new ForbiddenException("Members are currently not allowed to create tasks.");
            }
        }

        public async Task<TaskItem> UpdateAsync(Guid taskId, UpdateTaskRequest request, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            // Only validate when the assignee is actually changing — UpdateTaskRequest is a
            // full-resource replace (per the app's established PUT convention), so an edit to
            // any other field resends the current AssignedToUserId unchanged. Re-validating that
            // every time would break editing a task whose assignee has since been deactivated,
            // even when the edit has nothing to do with assignment.
            if (request.AssignedToUserId is Guid assigneeId && assigneeId != task.AssignedToUserId)
            {
                await EnsureAssigneeIsMemberAsync(task.ProjectId, assigneeId);
            }

            var wasComplete = task.Status == TaskItemStatus.Complete;
            var willBeComplete = request.Status == TaskItemStatus.Complete;
            var willStart = task.Status == TaskItemStatus.NotStarted && request.Status != TaskItemStatus.NotStarted;
            var isCompleting = willBeComplete && !wasComplete;

            // SortOrder deliberately isn't diffed here — reordering isn't a meaningful
            // change for a human reading the activity feed, unlike every field below.
            var activities = new List<TaskActivity>();

            // Phase 39 — backend enforcement of dependency blocking (never relies on the frontend
            // alone). Only checked on the two transitions that actually matter (leaving
            // NotStarted, and newly reaching Complete) — every other status change (e.g.
            // InProgress -> Waiting) is unaffected by dependencies entirely. Throws
            // DependencyBlockedException unless the caller both requested and is authorized for
            // an override, in which case the override is recorded into `activities` here and the
            // corresponding automation event dispatched after SaveChangesAsync below (never
            // before — a dispatched event must always reflect committed state).
            string? dependencyOverrideReason = null;
            if (willStart || isCompleting)
            {
                dependencyOverrideReason = await EnsureNotBlockedByDependenciesAsync(
                    task, willStart, isCompleting, request, callerId, callerRole, activities);
            }

            // Resolved once and reused for every notification this single edit might raise below
            // (a PUT can change several fields at once) rather than a fresh lookup per
            // notification — one extra indexed lookup per task update, not per notification.
            var actorName = await _db.Users.Where(u => u.Id == callerId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Someone";
            var taskNoun = task.ParentTaskId is null ? "Task" : "Subtask";

            var trimmedTitle = request.Title.Trim();
            var newDescription = NormalizeText(request.Description);

            LogFieldChange(activities, taskId, callerId, "Title", task.Title, trimmedTitle);
            LogFieldChange(activities, taskId, callerId, "Description", task.Description, newDescription);
            LogFieldChange(activities, taskId, callerId, "Status", task.Status.ToString(), request.Status.ToString());
            LogFieldChange(activities, taskId, callerId, "Priority", task.Priority.ToString(), request.Priority.ToString());
            LogFieldChange(
                activities, taskId, callerId, "Start Date",
                DateLabel(task.StartDate), DateLabel(request.StartDate));
            LogFieldChange(
                activities, taskId, callerId, "Due Date",
                DateLabel(task.DueDate), DateLabel(request.DueDate));

            var previousAssigneeId = task.AssignedToUserId;
            var previousStatus = task.Status;
            var previousPriority = task.Priority;
            var previousDueDate = task.DueDate;

            if (previousAssigneeId != request.AssignedToUserId)
            {
                var newAssigneeName = request.AssignedToUserId is Guid newAssigneeId
                    ? (await _db.Users.FindAsync(newAssigneeId))?.Name
                    : null;
                activities.Add(BuildActivity(taskId, callerId, "Assigned", "Assigned To", task.AssignedTo?.Name, newAssigneeName));

                // TaskAssigned when there was no previous assignee, TaskReassigned when there
                // was a *different* one, TaskUnassigned (to the *previous* assignee) when the new
                // value is null — each of the three is a distinct audience/message, not one type
                // with three phrasings.
                if (request.AssignedToUserId is Guid newlyAssignedId)
                {
                    var (type, notifTitle) = previousAssigneeId is null
                        ? (NotificationTypes.TaskAssigned, $"{taskNoun} assigned to you")
                        : (NotificationTypes.TaskReassigned, $"{taskNoun} reassigned to you");
                    await _notificationService.NotifyAsync(
                        newlyAssignedId, callerId, type, notifTitle,
                        $"{actorName} assigned you \"{trimmedTitle}\" in \"{task.Project!.Name}\".",
                        task.Id, task.ProjectId);
                }
                else if (previousAssigneeId is Guid removedAssigneeId)
                {
                    await _notificationService.NotifyAsync(
                        removedAssigneeId, callerId, NotificationTypes.TaskUnassigned, $"Removed from {taskNoun.ToLowerInvariant()}",
                        $"You were removed from \"{trimmedTitle}\" in \"{task.Project!.Name}\".",
                        task.Id, task.ProjectId);
                }
            }

            if (previousAssigneeId is Guid currentAssigneeId)
            {
                if (previousStatus != request.Status)
                {
                    await _notificationService.NotifyAsync(
                        currentAssigneeId, callerId, NotificationTypes.TaskStatusChanged, $"{taskNoun} status changed",
                        $"\"{trimmedTitle}\" changed to {request.Status}.",
                        task.Id, task.ProjectId);
                }

                if (previousPriority != request.Priority)
                {
                    await _notificationService.NotifyAsync(
                        currentAssigneeId, callerId, NotificationTypes.TaskPriorityChanged, $"{taskNoun} priority changed",
                        $"\"{trimmedTitle}\" priority changed to {request.Priority}.",
                        task.Id, task.ProjectId);
                }

                if (previousDueDate != request.DueDate)
                {
                    var dueDateMessage = request.DueDate is DateOnly newDueDate
                        ? $"\"{trimmedTitle}\" is now due {DateLabel(newDueDate)}."
                        : $"\"{trimmedTitle}\" no longer has a due date.";
                    await _notificationService.NotifyAsync(
                        currentAssigneeId, callerId, NotificationTypes.TaskDueDateChanged, $"{taskNoun} due date changed",
                        dueDateMessage, task.Id, task.ProjectId);
                }
            }

            task.Title = trimmedTitle;
            task.Description = newDescription;
            task.Status = request.Status;
            task.Priority = request.Priority;
            task.AssignedToUserId = request.AssignedToUserId;
            task.StartDate = request.StartDate;
            task.DueDate = request.DueDate;
            task.SortOrder = request.SortOrder;
            task.UpdatedAt = DateTime.UtcNow;

            // A changed due date always clears the overdue-automation guard (Phase 35) — whether
            // it moved into the future (should be able to become overdue again later) or just
            // changed to a different past/near date (the sweep re-evaluates it fresh next tick).
            if (previousDueDate != request.DueDate)
            {
                task.OverdueAutomationProcessedAt = null;
            }

            if (isCompleting)
            {
                task.CompletedAt = DateTime.UtcNow;

                // Two distinct audiences, independently controllable — the project owner (always,
                // pre-existing ProjectTaskCompleted behavior) and, separately, the task's own
                // assignee (new in Phase 36; a no-op if they're the same person as the owner or
                // the one completing it, since NotifyAsync already never notifies the actor).
                await _notificationService.NotifyAsync(
                    task.Project!.OwnerId, callerId, NotificationTypes.ProjectTaskCompleted, "Task completed",
                    $"{actorName} marked \"{trimmedTitle}\" complete in \"{task.Project.Name}\".",
                    task.Id, task.ProjectId);
                if (task.AssignedToUserId is Guid completedAssigneeId)
                {
                    await _notificationService.NotifyAsync(
                        completedAssigneeId, callerId, NotificationTypes.TaskCompleted, $"{taskNoun} completed",
                        $"\"{trimmedTitle}\" was marked complete.", task.Id, task.ProjectId);
                }

                await NotifyDependentsOfCompletionAsync(task, callerId);
            }
            else if (!willBeComplete && wasComplete)
            {
                task.CompletedAt = null;

                if (task.AssignedToUserId is Guid reopenedAssigneeId)
                {
                    await _notificationService.NotifyAsync(
                        reopenedAssigneeId, callerId, NotificationTypes.TaskReopened, $"{taskNoun} reopened",
                        $"\"{trimmedTitle}\" was reopened.", task.Id, task.ProjectId);
                }
            }

            _db.TaskActivities.AddRange(activities);
            await _db.SaveChangesAsync();

            await DispatchUpdateEventsAsync(
                task, previousStatus, request.Status, previousAssigneeId, request.AssignedToUserId,
                wasComplete, willBeComplete, callerId);

            if (dependencyOverrideReason is not null)
            {
                await _automationDispatcher.DispatchAsync(
                    new DependencyOverriddenEvent(task.Id, task.ProjectId, callerId, dependencyOverrideReason, DateTime.UtcNow));
            }

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            await AttachTaskSummariesAsync([task]);
            return task;
        }

        /// <summary>Fires every automation event UpdateAsync's own diff (computed above, not
        /// re-derived here) makes applicable — a single PUT can raise several at once (e.g.
        /// status AND assignee both changed), which is fine since each automation subscribes to
        /// exactly one AutomationTriggerType and simply ignores events of any other type. Called
        /// only after SaveChangesAsync succeeds, so a dispatched event always reflects committed
        /// state.</summary>
        private async Task DispatchUpdateEventsAsync(
            TaskItem task, TaskItemStatus previousStatus, TaskItemStatus newStatus,
            Guid? previousAssigneeId, Guid? newAssigneeId, bool wasComplete, bool willBeComplete, Guid callerId)
        {
            var now = DateTime.UtcNow;

            // Status changes are covered by the unconditional TaskUpdatedEvent above, not a
            // separate dispatch — there is no distinct AutomationTriggerType for "status changed"
            // (the spec's trigger list has no such entry), so a second event here would just
            // collide with TaskUpdatedEvent under the same trigger type and double-execute every
            // TaskUpdated automation whenever status changes. "Task.Status" is still available as
            // a condition field for automations that want to react specifically to it.
            await _automationDispatcher.DispatchAsync(new TaskUpdatedEvent(task.Id, task.ProjectId, callerId, now));

            if (previousAssigneeId != newAssigneeId)
            {
                await _automationDispatcher.DispatchAsync(
                    new TaskAssignedEvent(task.Id, task.ProjectId, previousAssigneeId, newAssigneeId, callerId, now));
            }

            if (willBeComplete && !wasComplete)
            {
                await _automationDispatcher.DispatchAsync(new TaskCompletedEvent(task.Id, task.ProjectId, callerId, now));

                if (task.ParentTaskId is Guid parentTaskId)
                {
                    await _automationDispatcher.DispatchAsync(
                        new SubtaskCompletedEvent(task.Id, parentTaskId, task.ProjectId, callerId, now));
                }
            }
            else if (!willBeComplete && wasComplete)
            {
                await _automationDispatcher.DispatchAsync(new TaskReopenedEvent(task.Id, task.ProjectId, callerId, now));
            }
        }

        public async Task<TaskItem> SetParentAsync(Guid taskId, Guid? parentTaskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            if (parentTaskId == task.ParentTaskId)
            {
                task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
                await AttachTaskSummariesAsync([task]);
                return task;
            }

            string? newParentTitle = null;
            if (parentTaskId is Guid newParentId)
            {
                if (newParentId == taskId)
                {
                    throw new ValidationException("Cannot move this task because it would create a circular hierarchy.");
                }

                var newParent = await LoadTaskAsync(newParentId);

                if (newParent.ProjectId != task.ProjectId)
                {
                    throw new ValidationException("Parent and child tasks must belong to the same project.");
                }

                // Would newParentId end up *below* task in the tree? Walking up from newParentId
                // and finding task means task is already an ancestor of newParentId — assigning
                // task -> newParentId would close that loop.
                if (await IsDescendantOfAsync(newParentId, taskId))
                {
                    throw new ValidationException("Cannot move this task because it would create a circular hierarchy.");
                }

                await EnsureDepthWithinLimitAsync(newParentId);

                newParentTitle = newParent.Title;
            }

            var oldParentTitle = task.ParentTaskId is Guid oldParentId
                ? await _db.Tasks.Where(t => t.Id == oldParentId).Select(t => t.Title).FirstOrDefaultAsync()
                : null;

            task.ParentTaskId = parentTaskId;
            task.SortOrder = await GetNextSortOrderAsync(task.ProjectId, parentTaskId);
            task.UpdatedAt = DateTime.UtcNow;

            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "Moved",
                OldValue = oldParentTitle ?? "Top Level",
                NewValue = newParentTitle ?? "Top Level"
            });

            await _db.SaveChangesAsync();

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            await AttachTaskSummariesAsync([task]);
            return task;
        }

        public async Task<TaskItem> SetSortOrderAsync(Guid taskId, double sortOrder, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            task.SortOrder = sortOrder;
            task.UpdatedAt = DateTime.UtcNow;

            _db.TaskActivities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Action = "Reordered"
            });

            await _db.SaveChangesAsync();

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            await AttachTaskSummariesAsync([task]);
            return task;
        }

        public async Task<IReadOnlyList<Tag>> GetLabelsAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            return await _db.TaskTags.Where(tt => tt.TaskId == taskId).Select(tt => tt.Tag!).OrderBy(t => t.Name).ToListAsync();
        }

        public async Task<Tag> AddLabelAsync(Guid taskId, string tagName, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            var tag = await _tagService.GetOrCreateAsync(tagName, callerId);

            var alreadyLinked = await _db.TaskTags.AnyAsync(tt => tt.TaskId == taskId && tt.TagId == tag.Id);
            if (alreadyLinked)
            {
                return tag;
            }

            _db.TaskTags.Add(new TaskTag { Id = Guid.NewGuid(), TaskId = taskId, TagId = tag.Id });
            task.UpdatedAt = DateTime.UtcNow;
            _db.TaskActivities.Add(BuildActivity(taskId, callerId, "Labeled", null, null, tag.Name));

            await _db.SaveChangesAsync();
            return tag;
        }

        public async Task RemoveLabelAsync(Guid taskId, Guid tagId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            var link = await _db.TaskTags.Include(tt => tt.Tag).FirstOrDefaultAsync(tt => tt.TaskId == taskId && tt.TagId == tagId);
            if (link is null)
            {
                return;
            }

            _db.TaskTags.Remove(link);
            task.UpdatedAt = DateTime.UtcNow;
            _db.TaskActivities.Add(BuildActivity(taskId, callerId, "Unlabeled", null, link.Tag?.Name, null));

            await _db.SaveChangesAsync();
        }

        private const int MaxNestingDepth = 10;

        /// <summary>Walks up from <paramref name="candidateParentId"/> and rejects once the chain
        /// would put a new subtask more than MaxNestingDepth levels deep. Bounded by
        /// MaxNestingDepth itself (never an unbounded loop) since it throws the moment the count
        /// is exceeded.</summary>
        private async Task EnsureDepthWithinLimitAsync(Guid candidateParentId)
        {
            var depth = 1;
            var currentId = candidateParentId;
            while (true)
            {
                var nextParentId = await _db.Tasks.Where(t => t.Id == currentId).Select(t => t.ParentTaskId).FirstOrDefaultAsync();
                if (nextParentId is not Guid next)
                {
                    return;
                }
                depth++;
                if (depth >= MaxNestingDepth)
                {
                    throw new ValidationException($"Tasks cannot be nested more than {MaxNestingDepth} levels deep.");
                }
                currentId = next;
            }
        }

        /// <summary>True if walking up from <paramref name="startTaskId"/> via ParentTaskId ever
        /// reaches <paramref name="candidateAncestorId"/> (or startTaskId *is*
        /// candidateAncestorId) — i.e. whether candidateAncestorId is an ancestor of (or the same
        /// task as) startTaskId.</summary>
        private async Task<bool> IsDescendantOfAsync(Guid startTaskId, Guid candidateAncestorId)
        {
            var currentId = (Guid?)startTaskId;
            var guard = 0;
            while (currentId is Guid id)
            {
                if (id == candidateAncestorId)
                {
                    return true;
                }
                // A safety valve, not an expected path — the depth limit already keeps real
                // chains far shorter than this, so only a corrupted chain would ever reach it,
                // and treating that as "yes, this would be circular" is the safe failure mode.
                if (++guard > 1000)
                {
                    return true;
                }
                currentId = await _db.Tasks.Where(t => t.Id == id).Select(t => t.ParentTaskId).FirstOrDefaultAsync();
            }
            return false;
        }

        /// <summary>Phase 39 backend enforcement — never trusts the frontend alone (per the
        /// spec's own explicit requirement). Checks GetStartBlockersAsync/GetCompletionBlockersAsync
        /// (whichever apply to the transition in progress) and either lets the caller through, lets
        /// them through via a recorded override, or throws DependencyBlockedException listing the
        /// blockers by title. Returns the override reason (to dispatch DependencyOverriddenEvent
        /// after the caller's own SaveChangesAsync) or null if there was nothing to override.</summary>
        private async Task<string?> EnsureNotBlockedByDependenciesAsync(
            TaskItem task, bool isStarting, bool isCompleting, UpdateTaskRequest request,
            Guid callerId, UserRole callerRole, List<TaskActivity> activities)
        {
            var blockers = new List<TaskDependency>();
            if (isStarting)
            {
                blockers.AddRange(await _dependencyService.GetStartBlockersAsync(task.Id));
            }
            if (isCompleting)
            {
                foreach (var blocker in await _dependencyService.GetCompletionBlockersAsync(task.Id))
                {
                    if (blockers.All(existing => existing.Id != blocker.Id))
                    {
                        blockers.Add(blocker);
                    }
                }
            }

            if (blockers.Count == 0)
            {
                return null;
            }

            var blockerTitles = blockers.Select(b => b.DependsOnTask!.Title).Distinct().ToList();

            if (!request.OverrideDependencyBlock)
            {
                var action = isCompleting ? "completed" : "started";
                throw new DependencyBlockedException(
                    $"Task cannot be {action} because it is blocked by: {string.Join(", ", blockerTitles)}", blockerTitles);
            }

            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new ValidationException("An override reason is required.");
            }

            // Only a project Manager (or Administrator/owner) may bypass a dependency block —
            // Permissions.DependenciesOverride (see PermissionService.ProjectMatrix) maps to
            // exactly this tier, so this is the same check the frontend's own `can()` gate mirrors.
            // Not every user can override, per the spec's own explicit requirement.
            await _projectAccess.EnsureCanManageAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            activities.Add(new TaskActivity
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = callerId,
                Action = "DependencyOverridden",
                FieldName = isCompleting ? "Complete" : "Start",
                OldValue = string.Join(", ", blockerTitles),
                NewValue = request.OverrideReason!.Trim()
            });

            return request.OverrideReason.Trim();
        }

        /// <summary>Called right after a task is marked Complete — dispatches DependencyCompleted
        /// (once per FinishToStart/FinishToFinish dependent — the two types whose own condition
        /// is specifically "prerequisite reached Complete") and, for every dependent now fully
        /// unblocked from starting, dispatches TaskBecameReady and notifies its assignee. A
        /// dependent with other still-unsatisfied prerequisites is deliberately not notified yet —
        /// the spec's example is "no longer blocked", not "one of several prerequisites finished".</summary>
        private async Task NotifyDependentsOfCompletionAsync(TaskItem completedTask, Guid actorId)
        {
            var dependentEdges = await _db.TaskDependencies
                .Where(d => d.DependsOnTaskId == completedTask.Id)
                .Select(d => new { d.TaskId, d.DependencyType })
                .ToListAsync();

            foreach (var edge in dependentEdges.Where(e => e.DependencyType is DependencyTypes.FinishToStart or DependencyTypes.FinishToFinish))
            {
                await _automationDispatcher.DispatchAsync(
                    new DependencyCompletedEvent(edge.TaskId, completedTask.Id, completedTask.ProjectId, actorId, DateTime.UtcNow));
            }

            foreach (var dependentTaskId in dependentEdges.Select(e => e.TaskId).Distinct())
            {
                var remainingBlockers = await _dependencyService.GetStartBlockersAsync(dependentTaskId);
                if (remainingBlockers.Count > 0)
                {
                    continue;
                }

                await _automationDispatcher.DispatchAsync(
                    new TaskBecameReadyEvent(dependentTaskId, completedTask.ProjectId, actorId, DateTime.UtcNow));

                var dependent = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == dependentTaskId);
                if (dependent?.AssignedToUserId is Guid assigneeId)
                {
                    await _notificationService.NotifyAsync(
                        assigneeId, actorId, NotificationTypes.TaskDependencyCompleted, "Task dependency completed",
                        $"\"{completedTask.Title}\" is complete. \"{dependent.Title}\" is no longer blocked.",
                        dependent.Id, dependent.ProjectId);
                }
            }
        }

        public async Task DeleteAsync(Guid taskId, bool deleteSubtasks, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);

            // "Allow members to delete tasks" relaxes the tier from manage (Administrator/owner/
            // project Manager) down to edit (also project Members) — the inverse of most
            // Behavior settings, which narrow an otherwise-open action instead. Still never a
            // Viewer, regardless of this setting — EnsureCanEditAsync excludes them unconditionally.
            var allowMembersToDelete = await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowMembersToDeleteTasks);
            if (allowMembersToDelete)
            {
                await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            }
            else
            {
                await _projectAccess.EnsureCanManageAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            }

            var children = await _db.Tasks.Where(t => t.ParentTaskId == taskId).ToListAsync();

            if (children.Count > 0 && deleteSubtasks)
            {
                // "Delete task and all subtasks" — the whole subtree removed in the same
                // SaveChangesAsync as the task itself, so this can never partially delete a
                // hierarchy (a failed save leaves everything untouched, not half-gone).
                var descendants = await CollectDescendantsAsync(taskId);
                _db.Tasks.RemoveRange(descendants);
            }
            else if (children.Count > 0)
            {
                // "Delete task only" (the default/safe option) — direct children move up to this
                // task's own parent, same as removing one link from the middle of a chain. Their
                // own descendants are untouched (only child.ParentTaskId changes here), so nothing
                // deeper in the tree needs to move at all.
                foreach (var child in children)
                {
                    child.ParentTaskId = task.ParentTaskId;
                    child.SortOrder = await GetNextSortOrderAsync(task.ProjectId, task.ParentTaskId);
                    child.UpdatedAt = DateTime.UtcNow;
                }
            }

            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync();
        }

        /// <summary>Breadth-first walk collecting every descendant of <paramref name="rootTaskId"/>
        /// (not including the root itself) — used only for the transactional "delete task and all
        /// subtasks" path. One query per depth level, not one query per node.</summary>
        private async Task<List<TaskItem>> CollectDescendantsAsync(Guid rootTaskId)
        {
            var all = new List<TaskItem>();
            var frontier = new List<Guid> { rootTaskId };

            while (frontier.Count > 0)
            {
                var children = await _db.Tasks
                    .Where(t => t.ParentTaskId != null && frontier.Contains(t.ParentTaskId!.Value))
                    .ToListAsync();
                if (children.Count == 0)
                {
                    break;
                }
                all.AddRange(children);
                frontier = children.Select(c => c.Id).ToList();
            }

            return all;
        }

        public async Task<TaskItem> DuplicateAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var original = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(original.ProjectId, original.Project!.OwnerId, callerId, callerRole);

            var copy = new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = original.ProjectId,
                // A duplicate of a subtask stays a subtask under the same parent — "duplicate"
                // shouldn't silently relocate it to top level.
                ParentTaskId = original.ParentTaskId,
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
            _db.TaskActivities.Add(BuildActivity(copy.Id, callerId, "Created", null, null, null));

            foreach (var value in original.CustomValues)
            {
                _db.TaskCustomValues.Add(new TaskCustomValue
                {
                    Id = Guid.NewGuid(),
                    TaskId = copy.Id,
                    CustomFieldId = value.CustomFieldId,
                    Value = value.Value
                });
            }

            await _db.SaveChangesAsync();

            copy.AssignedTo = original.AssignedTo;
            copy.CustomValues = original.CustomValues
                .Select(v => new TaskCustomValue { CustomFieldId = v.CustomFieldId, Value = v.Value })
                .ToList();
            return copy;
        }

        private async Task<TaskItem> LoadTaskAsync(Guid taskId)
        {
            var task = await _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Include(t => t.CustomValues)
                .FirstOrDefaultAsync(t => t.Id == taskId);
            return task ?? throw new NotFoundException($"Task '{taskId}' was not found.");
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        /// <summary>Populates TaskItem.DependsOnCount/BlockedByCount/DependentCount for every task
        /// in <paramref name="tasks"/> with exactly two aggregate queries total, regardless of how
        /// many tasks are passed in — not one query per task — so list endpoints (including a
        /// 1,000-task project) stay N+1-free. Called by every method that returns tasks to a
        /// client (list, search, my-tasks, get-by-id, update).</summary>
        private async Task AttachDependencySummaryAsync(IReadOnlyList<TaskItem> tasks)
        {
            if (tasks.Count == 0)
            {
                return;
            }

            var ids = tasks.Select(t => t.Id).ToList();

            var dependsOnRows = await _db.TaskDependencies
                .Where(d => ids.Contains(d.TaskId))
                .Select(d => new { d.TaskId, d.DependencyType, PrerequisiteStatus = d.DependsOnTask!.Status })
                .ToListAsync();

            var dependentCounts = await _db.TaskDependencies
                .Where(d => ids.Contains(d.DependsOnTaskId))
                .GroupBy(d => d.DependsOnTaskId)
                .Select(g => new { TaskId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TaskId, x => x.Count);

            var dependsOnGroups = dependsOnRows.GroupBy(r => r.TaskId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var task in tasks)
            {
                if (dependsOnGroups.TryGetValue(task.Id, out var rows))
                {
                    task.DependsOnCount = rows.Count;
                    // Phase 39 — type-aware: a Finish-to-Finish/Start-to-Finish dependency never
                    // blocks STARTING (only completing), so it no longer inflates this "blocked
                    // from starting" count the way a blanket "prerequisite != Complete" check
                    // would. See TaskDependencyService's own doc comment for the full rule table.
                    task.BlockedByCount = rows.Count(r => !TaskDependencyService.IsSatisfiedForStart(r.DependencyType, r.PrerequisiteStatus));
                }
                task.DependentCount = dependentCounts.GetValueOrDefault(task.Id);
            }
        }

        /// <summary>Every bulk-populated, non-persisted field TaskDto exposes (dependency counts,
        /// subtask counts, parent title) in one call — the single place every task-returning
        /// method in this service goes through, so a future addition to this family only needs to
        /// be wired in once.</summary>
        private async Task AttachTaskSummariesAsync(IReadOnlyList<TaskItem> tasks)
        {
            await AttachDependencySummaryAsync(tasks);
            await AttachSubtaskSummaryAsync(tasks);
        }

        /// <summary>Populates TaskItem.SubtaskCount/CompletedSubtaskCount (direct children only,
        /// not the full recursive subtree) and ParentTaskTitle — three aggregate queries total
        /// regardless of how many tasks are passed in, not one per task.</summary>
        private async Task AttachSubtaskSummaryAsync(IReadOnlyList<TaskItem> tasks)
        {
            if (tasks.Count == 0)
            {
                return;
            }

            var ids = tasks.Select(t => t.Id).ToList();

            var childRows = await _db.Tasks
                .Where(t => t.ParentTaskId != null && ids.Contains(t.ParentTaskId!.Value))
                .Select(t => new { ParentTaskId = t.ParentTaskId!.Value, t.Status })
                .ToListAsync();
            var childGroups = childRows.GroupBy(r => r.ParentTaskId).ToDictionary(g => g.Key, g => g.ToList());

            var parentIds = tasks.Where(t => t.ParentTaskId != null).Select(t => t.ParentTaskId!.Value).Distinct().ToList();
            var parentTitles = parentIds.Count == 0
                ? []
                : await _db.Tasks.Where(t => parentIds.Contains(t.Id)).Select(t => new { t.Id, t.Title }).ToDictionaryAsync(x => x.Id, x => x.Title);

            foreach (var task in tasks)
            {
                if (childGroups.TryGetValue(task.Id, out var rows))
                {
                    task.SubtaskCount = rows.Count;
                    task.CompletedSubtaskCount = rows.Count(r => r.Status == TaskItemStatus.Complete);
                }
                if (task.ParentTaskId is Guid parentId)
                {
                    task.ParentTaskTitle = parentTitles.GetValueOrDefault(parentId);
                }
            }
        }

        public async Task<IReadOnlyList<TaskActivity>> GetActivitiesForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            return await _db.TaskActivities
                .Include(a => a.User)
                .Where(a => a.TaskId == taskId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        private static TaskActivity BuildActivity(Guid taskId, Guid userId, string action, string? fieldName, string? oldValue, string? newValue) =>
            new()
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = userId,
                Action = action,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue
            };

        /// <summary>Appends a "FieldChanged" activity only if the value actually changed — avoids logging no-op edits.</summary>
        private static void LogFieldChange(List<TaskActivity> activities, Guid taskId, Guid userId, string fieldName, string? oldValue, string? newValue)
        {
            if (oldValue != newValue)
            {
                activities.Add(BuildActivity(taskId, userId, "FieldChanged", fieldName, oldValue, newValue));
            }
        }

        private async Task EnsureAssigneeIsMemberAsync(Guid projectId, Guid assigneeId)
        {
            if (!await _projectAccess.IsMemberAsync(projectId, assigneeId))
            {
                throw new ValidationException("The assigned user must be a member of this project.");
            }

            // A deactivated or deleted member stays visible on tasks they were already assigned
            // to (nothing here touches an existing AssignedToUserId), but can't be picked as a
            // *new* assignment — deletion always sets IsActive=false too, so this one check
            // covers both cases per Phase 26.
            var isActive = await _db.Users.Where(u => u.Id == assigneeId).Select(u => u.IsActive).FirstOrDefaultAsync();
            if (!isActive)
            {
                throw new ValidationException("The assigned user is not active.");
            }
        }

        /// <summary>Used only when the client omits Status entirely (CreateTaskRequest.Status is
        /// nullable specifically for this) — an explicit client-chosen status is never
        /// overridden by the configured default.</summary>
        private async Task<TaskItemStatus> ResolveDefaultStatusAsync()
        {
            var value = await _systemSettings.GetStringAsync(SystemSettingKeys.DefaultTaskStatus);
            return Enum.TryParse<TaskItemStatus>(value, out var status) ? status : TaskItemStatus.NotStarted;
        }

        private async Task<TaskPriority> ResolveDefaultPriorityAsync()
        {
            var value = await _systemSettings.GetStringAsync(SystemSettingKeys.DefaultTaskPriority);
            return Enum.TryParse<TaskPriority>(value, out var priority) ? priority : TaskPriority.Medium;
        }

        /// <summary>Scoped to siblings (same ProjectId + ParentTaskId, where ParentTaskId=null is
        /// the project's top-level group) rather than the whole project — every existing task has
        /// ParentTaskId=null, so top-level ordering is unaffected; subtasks simply get their own
        /// independent sequence under their parent (Phase 30).</summary>
        private async Task<double> GetNextSortOrderAsync(Guid projectId, Guid? parentTaskId)
        {
            var maxSortOrder = await _db.Tasks
                .Where(t => t.ProjectId == projectId && t.ParentTaskId == parentTaskId)
                .Select(t => (double?)t.SortOrder)
                .MaxAsync();
            return (maxSortOrder ?? 0) + 1;
        }

        /// <summary>Fractional-index insertion point immediately after <paramref name="task"/>
        /// among its own siblings, so a duplicate lands next to its source instead of at the
        /// bottom of the (whole project's, pre-Phase-30) list.</summary>
        private async Task<double> GetSortOrderAfterAsync(TaskItem task)
        {
            var nextSortOrder = await _db.Tasks
                .Where(t => t.ProjectId == task.ProjectId && t.ParentTaskId == task.ParentTaskId && t.SortOrder > task.SortOrder)
                .OrderBy(t => t.SortOrder)
                .Select(t => (double?)t.SortOrder)
                .FirstOrDefaultAsync();

            return nextSortOrder is double next ? (task.SortOrder + next) / 2 : task.SortOrder + 1;
        }

        private static string? NormalizeText(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        private static string? DateLabel(DateOnly? date) =>
            date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public async Task<TaskItem> SetCustomValueAsync(Guid taskId, Guid customFieldId, string? value, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanEditAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            var field = await _db.CustomFields
                .Include(f => f.Options)
                .FirstOrDefaultAsync(f => f.Id == customFieldId)
                ?? throw new NotFoundException($"Custom field '{customFieldId}' was not found.");

            if (field.ProjectId is Guid fieldProjectId && fieldProjectId != task.ProjectId)
            {
                throw new ValidationException("That custom field does not belong to this task's project.");
            }

            var normalizedValue = await ValidateAndNormalizeCustomValueAsync(field, value, task.ProjectId);
            var existing = task.CustomValues.FirstOrDefault(v => v.CustomFieldId == customFieldId);
            var oldValue = existing?.Value;

            if (normalizedValue is null)
            {
                if (existing is not null)
                {
                    task.CustomValues.Remove(existing);
                    _db.TaskCustomValues.Remove(existing);
                }
            }
            else if (existing is null)
            {
                var newValue = new TaskCustomValue
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskId,
                    CustomFieldId = customFieldId,
                    Value = normalizedValue
                };
                task.CustomValues.Add(newValue);
                _db.TaskCustomValues.Add(newValue);
            }
            else
            {
                existing.Value = normalizedValue;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            if (oldValue != normalizedValue)
            {
                _db.TaskActivities.Add(BuildActivity(taskId, callerId, "FieldChanged", field.Name, oldValue, normalizedValue));
            }

            await _db.SaveChangesAsync();
            return task;
        }

        private async Task<string?> ValidateAndNormalizeCustomValueAsync(CustomField field, string? value, Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.IsRequired)
                {
                    throw new ValidationException($"'{field.Name}' is required and cannot be cleared.");
                }
                return null;
            }

            // A deactivated field keeps its existing values readable (never force-cleared) but
            // can't be given a new value — the admin-facing "prefer deactivation over deletion"
            // path from Phase 25 would be pointless if the field stayed fully writable anyway.
            if (!field.IsActive)
            {
                throw new ValidationException($"'{field.Name}' has been disabled and can no longer be set.");
            }

            switch (field.FieldType)
            {
                case CustomFieldType.Text:
                case CustomFieldType.LongText:
                    return value.Trim();

                case CustomFieldType.Number:
                case CustomFieldType.Currency:
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    {
                        throw new ValidationException($"'{value}' is not a valid number for field '{field.Name}'.");
                    }
                    return value.Trim();

                case CustomFieldType.Date:
                    if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        throw new ValidationException($"'{value}' is not a valid date (expected YYYY-MM-DD) for field '{field.Name}'.");
                    }
                    return value.Trim();

                case CustomFieldType.DateTime:
                    if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsedDateTime))
                    {
                        throw new ValidationException($"'{value}' is not a valid date/time for field '{field.Name}'.");
                    }
                    return parsedDateTime.ToString("O", CultureInfo.InvariantCulture);

                case CustomFieldType.Checkbox:
                    if (value is not ("true" or "false"))
                    {
                        throw new ValidationException($"Checkbox field '{field.Name}' must be 'true' or 'false'.");
                    }
                    return value;

                case CustomFieldType.Dropdown:
                    if (!field.Options.Any(o => o.Id.ToString() == value))
                    {
                        throw new ValidationException($"'{value}' is not a valid option for field '{field.Name}'.");
                    }
                    return value;

                case CustomFieldType.MultiSelect:
                    var optionIds = ParseMultiSelectValue(value, field.Name);
                    var validIds = field.Options.Select(o => o.Id).ToHashSet();
                    if (optionIds.Any(id => !validIds.Contains(id)))
                    {
                        throw new ValidationException($"One or more selected options are not valid for field '{field.Name}'.");
                    }
                    return JsonSerializer.Serialize(optionIds);

                case CustomFieldType.User:
                    if (!Guid.TryParse(value, out var userId) || !await _projectAccess.IsMemberAsync(projectId, userId))
                    {
                        throw new ValidationException($"'{value}' is not a valid project member for field '{field.Name}'.");
                    }
                    return value;

                case CustomFieldType.Link:
                    return NormalizeLinkValue(value, field.Name);

                default:
                    throw new ValidationException($"Unsupported field type for '{field.Name}'.");
            }
        }

        private static string NormalizeLinkValue(string value, string fieldName)
        {
            LinkValue? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<LinkValue>(value);
            }
            catch (JsonException)
            {
                throw new ValidationException($"'{value}' is not a valid link value for field '{fieldName}'.");
            }

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Url))
            {
                throw new ValidationException($"A URL is required for link field '{fieldName}'.");
            }

            var originalUrl = parsed.Url.Trim();
            var url = originalUrl;

            // Users naturally type "google.com" without a scheme — treat that like a
            // browser address bar would, rather than rejecting it.
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host))
            {
                throw new ValidationException($"'{originalUrl}' is not a valid URL for field '{fieldName}'.");
            }

            return JsonSerializer.Serialize(new LinkValue { Label = parsed.Label?.Trim() ?? string.Empty, Url = url });
        }

        private class LinkValue
        {
            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
        }

        private static List<Guid> ParseMultiSelectValue(string value, string fieldName)
        {
            try
            {
                return JsonSerializer.Deserialize<List<Guid>>(value) ?? [];
            }
            catch (JsonException)
            {
                throw new ValidationException($"'{value}' is not a valid selection list for field '{fieldName}'.");
            }
        }
    }
}

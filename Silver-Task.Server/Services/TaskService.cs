using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
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

        Task DeleteAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskItem> DuplicateAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskItem> SetCustomValueAsync(Guid taskId, Guid customFieldId, string? value, Guid callerId, UserRole callerRole);

        Task<IReadOnlyList<TaskActivity>> GetActivitiesForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole);
    }

    public class TaskService(
        AppDbContext db,
        IProjectAccessService projectAccess,
        ISystemSettingsService systemSettings,
        INotificationService notificationService) : ITaskService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly INotificationService _notificationService = notificationService;

        public async Task<IReadOnlyList<TaskItem>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            return await _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.CustomValues)
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();
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

            return await tasksQuery
                .OrderByDescending(t => t.UpdatedAt)
                .Take(limit)
                .ToListAsync();
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

            return await query
                .OrderBy(t => t.DueDate == null)
                .ThenBy(t => t.DueDate)
                .ThenBy(t => t.Title)
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

            // Plain Members pass EnsureCanParticipateAsync (same as Managers/the owner) — this
            // extra check is the only place task creation can be narrowed further, to just
            // Manager-tier, via the "Allow members to create tasks" system setting.
            if (callerRole == UserRole.Member && callerId != project.OwnerId &&
                !await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowMembersToCreateTasks))
            {
                throw new ForbiddenException("Members are currently not allowed to create tasks.");
            }

            if (request.AssignedToUserId is Guid assigneeId)
            {
                await EnsureAssigneeIsMemberAsync(projectId, assigneeId);
            }

            var status = request.Status ?? await ResolveDefaultStatusAsync();
            var priority = request.Priority ?? await ResolveDefaultPriorityAsync();

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = request.Title.Trim(),
                Description = NormalizeText(request.Description),
                Status = status,
                Priority = priority,
                AssignedToUserId = request.AssignedToUserId,
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                CompletedAt = status == TaskItemStatus.Complete ? DateTime.UtcNow : null,
                SortOrder = await GetNextSortOrderAsync(projectId)
            };

            _db.Tasks.Add(task);
            _db.TaskActivities.Add(BuildActivity(task.Id, callerId, "Created", null, null, null));

            if (task.AssignedToUserId is Guid assignedOnCreateId)
            {
                await _notificationService.NotifyAsync(
                    assignedOnCreateId, callerId, NotificationTypes.TaskAssigned,
                    "Task assigned to you",
                    $"\"{task.Title}\" was assigned to you in \"{project.Name}\".",
                    task.Id, project.Id);
            }

            await _db.SaveChangesAsync();

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            return task;
        }

        public async Task<TaskItem> UpdateAsync(Guid taskId, UpdateTaskRequest request, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

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

            // SortOrder deliberately isn't diffed here — reordering isn't a meaningful
            // change for a human reading the activity feed, unlike every field below.
            var activities = new List<TaskActivity>();
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
                // was a *different* one — unassigning (new value null) notifies no one.
                if (request.AssignedToUserId is Guid newlyAssignedId)
                {
                    var (type, notifTitle) = previousAssigneeId is null
                        ? (NotificationTypes.TaskAssigned, "Task assigned to you")
                        : (NotificationTypes.TaskReassigned, "Task reassigned to you");
                    await _notificationService.NotifyAsync(
                        newlyAssignedId, callerId, type, notifTitle,
                        $"\"{trimmedTitle}\" was assigned to you in \"{task.Project!.Name}\".",
                        task.Id, task.ProjectId);
                }
            }

            if (previousAssigneeId is Guid currentAssigneeId)
            {
                if (previousStatus != request.Status)
                {
                    await _notificationService.NotifyAsync(
                        currentAssigneeId, callerId, NotificationTypes.TaskStatusChanged, "Task status changed",
                        $"\"{trimmedTitle}\" status changed from {previousStatus} to {request.Status}.",
                        task.Id, task.ProjectId);
                }

                if (previousPriority != request.Priority)
                {
                    await _notificationService.NotifyAsync(
                        currentAssigneeId, callerId, NotificationTypes.TaskPriorityChanged, "Task priority changed",
                        $"\"{trimmedTitle}\" priority changed from {previousPriority} to {request.Priority}.",
                        task.Id, task.ProjectId);
                }

                if (previousDueDate != request.DueDate)
                {
                    var dueDateMessage = request.DueDate is DateOnly newDueDate
                        ? $"\"{trimmedTitle}\" due date changed to {DateLabel(newDueDate)}."
                        : $"\"{trimmedTitle}\" no longer has a due date.";
                    await _notificationService.NotifyAsync(
                        currentAssigneeId, callerId, NotificationTypes.TaskDueDateChanged, "Task due date changed",
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

            if (willBeComplete && !wasComplete)
            {
                task.CompletedAt = DateTime.UtcNow;

                await _notificationService.NotifyAsync(
                    task.Project!.OwnerId, callerId, NotificationTypes.ProjectTaskCompleted, "Task completed",
                    $"\"{trimmedTitle}\" was marked complete in \"{task.Project.Name}\".",
                    task.Id, task.ProjectId);
            }
            else if (!willBeComplete && wasComplete)
            {
                task.CompletedAt = null;
            }

            _db.TaskActivities.AddRange(activities);
            await _db.SaveChangesAsync();

            task.AssignedTo = task.AssignedToUserId is null ? null : await _db.Users.FindAsync(task.AssignedToUserId);
            return task;
        }

        public async Task DeleteAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);

            // "Allow members to delete tasks" relaxes the tier from manage (Administrator/owner/
            // Manager-member) down to participate (also plain Members) — the inverse of most
            // Behavior settings, which narrow an otherwise-open action instead.
            var allowMembersToDelete = await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowMembersToDeleteTasks);
            if (allowMembersToDelete)
            {
                await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            }
            else
            {
                await _projectAccess.EnsureCanManageAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);
            }

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

        private static string? DateLabel(DateOnly? date) =>
            date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public async Task<TaskItem> SetCustomValueAsync(Guid taskId, Guid customFieldId, string? value, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

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

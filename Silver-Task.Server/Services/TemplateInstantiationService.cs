using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.DTOs.Templates;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ITemplateInstantiationService
    {
        /// <summary>Read-only — no writes. Computes the schedule every task WOULD get and flags
        /// (never silently fixes) any dependency whose computed dates would be scheduled
        /// backwards (spec #80).</summary>
        Task<ProjectTemplatePreviewDto> PreviewProjectTemplateAsync(Guid templateId, DateOnly startDate, Guid callerId, UserRole callerRole);

        /// <summary>The whole operation — Project + every Task/Subtask + every Dependency — runs
        /// inside one database transaction (spec #55): either everything commits, or nothing does.
        /// Reuses IProjectService.CreateAsync/ITaskService.CreateAsync/CreateSubtaskAsync/
        /// SetCustomValueAsync/AddLabelAsync/ITaskDependencyService.CreateAsync throughout — never
        /// a parallel, less-validated creation path (spec #61/#62/#65).</summary>
        Task<ProjectDto> CreateProjectFromTemplateAsync(CreateProjectFromTemplateRequest request, Guid callerId, UserRole callerRole);

        Task<TaskDto> CreateTaskFromTemplateAsync(CreateTaskFromTemplateRequest request, Guid callerId, UserRole callerRole);
    }

    /// <summary>
    /// Phase 40 — the ONLY place templates are turned into real Projects/Tasks. Deliberately
    /// separate from ITemplateService (which only edits template definitions) per the spec's own
    /// explicit service-boundary suggestion (#57). ID mapping (TemplateTaskId -> real TaskId,
    /// spec #58) is a plain in-memory Dictionary built up as tasks are created, in parent-before-
    /// child order, and is what dependency creation maps through — dependencies are always created
    /// between the NEW tasks, never pointing back at template rows (spec #20).
    /// </summary>
    public class TemplateInstantiationService(
        AppDbContext db,
        IProjectService projectService,
        ITaskService taskService,
        ITaskDependencyService dependencyService,
        INotificationService notificationService) : ITemplateInstantiationService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectService _projectService = projectService;
        private readonly ITaskService _taskService = taskService;
        private readonly ITaskDependencyService _dependencyService = dependencyService;
        private readonly INotificationService _notificationService = notificationService;

        public async Task<ProjectTemplatePreviewDto> PreviewProjectTemplateAsync(Guid templateId, DateOnly startDate, Guid callerId, UserRole callerRole)
        {
            var template = await LoadProjectTemplateForUseAsync(templateId, callerId, callerRole, requireNotArchived: false);

            var computedDates = new Dictionary<Guid, (DateOnly? Start, DateOnly? Due)>();
            var schedule = new List<TemplateScheduleItemDto>();
            foreach (var task in template.Tasks)
            {
                var taskStart = task.StartOffsetDays is int startOffset ? startDate.AddDays(startOffset) : (DateOnly?)null;
                DateOnly? taskDue;
                if (task.DueOffsetDays is int dueOffset)
                {
                    taskDue = startDate.AddDays(dueOffset);
                }
                else if (taskStart is DateOnly resolvedStart && task.EstimatedDurationDays is int duration)
                {
                    taskDue = resolvedStart.AddDays(duration);
                }
                else
                {
                    taskDue = null;
                }

                computedDates[task.Id] = (taskStart, taskDue);
                schedule.Add(new TemplateScheduleItemDto
                { TemplateTaskId = task.Id, Title = task.Title, ComputedStartDate = taskStart, ComputedDueDate = taskDue });
            }

            var warnings = new List<string>();
            var taskById = template.Tasks.ToDictionary(t => t.Id);
            foreach (var dep in template.Dependencies)
            {
                var (fromStart, fromDue) = computedDates.GetValueOrDefault(dep.DependsOnTemplateTaskId);
                var (toStart, toDue) = computedDates.GetValueOrDefault(dep.TemplateTaskId);

                var impossible = dep.DependencyType switch
                {
                    DependencyTypes.FinishToStart => fromDue is DateOnly fd && toStart is DateOnly ts && fd > ts,
                    DependencyTypes.StartToStart => fromStart is DateOnly fs && toStart is DateOnly ts2 && fs > ts2,
                    DependencyTypes.FinishToFinish => fromDue is DateOnly fd2 && toDue is DateOnly td && fd2 > td,
                    DependencyTypes.StartToFinish => fromStart is DateOnly fs2 && toDue is DateOnly td2 && fs2 > td2,
                    _ => false
                };
                if (impossible)
                {
                    warnings.Add(
                        $"\"{taskById[dep.TemplateTaskId].Title}\" depends on \"{taskById[dep.DependsOnTemplateTaskId].Title}\", " +
                        "but its computed schedule places it before the prerequisite. Dates are not changed automatically — review the offsets.");
                }
            }

            var datedDueDates = schedule.Where(s => s.ComputedDueDate.HasValue).Select(s => s.ComputedDueDate!.Value).ToList();
            int? estimatedDuration = datedDueDates.Count > 0 ? (datedDueDates.Max().DayNumber - startDate.DayNumber) + 1 : null;

            return new ProjectTemplatePreviewDto
            {
                TemplateName = template.Name,
                TaskCount = template.Tasks.Count,
                SubtaskCount = template.Tasks.Count(t => t.ParentTemplateTaskId != null),
                DependencyCount = template.Dependencies.Count,
                EstimatedDurationDays = estimatedDuration,
                Schedule = schedule.OrderBy(s => s.ComputedStartDate ?? DateOnly.MaxValue).ToList(),
                Warnings = warnings
            };
        }

        public async Task<ProjectDto> CreateProjectFromTemplateAsync(CreateProjectFromTemplateRequest request, Guid callerId, UserRole callerRole)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectName))
            {
                throw new ValidationException("Project name is required.");
            }
            if (request.AssignmentOverride is not null &&
                request.AssignmentOverride != TemplateAssignmentModes.ProjectManager &&
                request.AssignmentOverride != TemplateAssignmentModes.Unassigned)
            {
                throw new ValidationException("Unrecognized assignment override.");
            }

            var template = await LoadProjectTemplateForUseAsync(request.TemplateId, callerId, callerRole, requireNotArchived: true);
            var orderedTasks = OrderForCreation(template.Tasks.ToList());

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // The project's owner is always the caller (matches every other project-creation
                // path in this app — see ProjectService.CreateAsync's own doc comment); there is
                // no separate "assign ownership to someone else" concept to reuse here.
                var project = await _projectService.CreateAsync(
                    new CreateProjectRequest { Name = request.ProjectName.Trim(), Description = request.ProjectDescription }, callerId, callerRole);

                var actorName = await _db.Users.Where(u => u.Id == callerId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Someone";
                var startDateText = request.StartDate.ToString("yyyy-MM-dd");

                var idMap = new Dictionary<Guid, Guid>();
                foreach (var templateTask in orderedTasks)
                {
                    var title = SubstituteVariables(templateTask.Title, project.Name, startDateText, actorName)!;
                    var description = SubstituteVariables(templateTask.Description, project.Name, startDateText, actorName);
                    var assigneeId = ResolveAssignment(templateTask.AssignmentMode, templateTask.AssignedToUserId, callerId, request.AssignmentOverride);

                    var startDate = templateTask.StartOffsetDays is int so ? request.StartDate.AddDays(so) : (DateOnly?)null;
                    DateOnly? dueDate;
                    if (templateTask.DueOffsetDays is int dueOffset)
                    {
                        dueDate = request.StartDate.AddDays(dueOffset);
                    }
                    else if (startDate is DateOnly sd && templateTask.EstimatedDurationDays is int duration)
                    {
                        dueDate = sd.AddDays(duration);
                    }
                    else
                    {
                        dueDate = null;
                    }

                    var createRequest = new CreateTaskRequest
                    {
                        Title = title,
                        Description = description,
                        Status = templateTask.Status,
                        Priority = templateTask.Priority,
                        AssignedToUserId = assigneeId,
                        StartDate = startDate,
                        DueDate = dueDate
                    };

                    var createdTask = templateTask.ParentTemplateTaskId is Guid parentTemplateId
                        ? await _taskService.CreateSubtaskAsync(idMap[parentTemplateId], createRequest, callerId, callerRole)
                        : await _taskService.CreateAsync(project.Id, createRequest, callerId, callerRole);

                    idMap[templateTask.Id] = createdTask.Id;

                    foreach (var tagName in templateTask.Tags.Select(t => t.Tag?.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
                    {
                        await _taskService.AddLabelAsync(createdTask.Id, tagName!, callerId, callerRole);
                    }
                    foreach (var customValue in templateTask.CustomValues)
                    {
                        await _taskService.SetCustomValueAsync(createdTask.Id, customValue.CustomFieldId, customValue.Value, callerId, callerRole);
                    }
                    if (templateTask.ChecklistItems.Count > 0)
                    {
                        _db.TaskChecklistItems.AddRange(templateTask.ChecklistItems.OrderBy(c => c.SortOrder).Select(c => new TaskChecklistItem
                        { Id = Guid.NewGuid(), TaskId = createdTask.Id, Text = c.Text, SortOrder = c.SortOrder }));
                        await _db.SaveChangesAsync();
                    }
                }

                // Dependencies are created between the NEW tasks via the id map — never pointing
                // back at template rows (spec #20) — through the exact same
                // ITaskDependencyService.CreateAsync every manually-created dependency uses,
                // including its own cycle/self-dependency checks (spec #65).
                foreach (var dep in template.Dependencies)
                {
                    await _dependencyService.CreateAsync(
                        idMap[dep.TemplateTaskId], idMap[dep.DependsOnTemplateTaskId], dep.DependencyType, callerId, callerRole);
                }

                var templateEntity = await _db.ProjectTemplates.FirstAsync(t => t.Id == template.Id);
                templateEntity.UsageCount++;
                templateEntity.LastUsedAt = DateTime.UtcNow;

                var projectEntity = await _db.Projects.FirstAsync(p => p.Id == project.Id);
                projectEntity.SourceProjectTemplateId = template.Id;
                projectEntity.SourceTemplateSnapshotAt = template.UpdatedAt;

                // One summary notification, not one per generated task (spec #63) — the closest
                // existing mechanism to a durable "project created from template" event, since
                // this app has no project-level activity/audit log to write to (see the Phase 40
                // final report's own disclosed research note). NotifyAsync only adds to the change
                // tracker (see its own doc comment), so this must happen before the SaveChangesAsync
                // below — calling it after CommitAsync would silently drop the notification, since
                // nothing would ever persist it.
                await _notificationService.NotifyAsync(
                    callerId, null, NotificationTypes.TemplateProjectCreated,
                    "Project created from template",
                    $"\"{project.Name}\" was created from the \"{template.Name}\" template with {orderedTasks.Count} tasks.",
                    null, project.Id);

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return await ReloadProjectDtoAsync(project.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<TaskDto> CreateTaskFromTemplateAsync(CreateTaskFromTemplateRequest request, Guid callerId, UserRole callerRole)
        {
            var template = await _db.TaskTemplates
                .Include(t => t.Tags).ThenInclude(tg => tg.Tag)
                .Include(t => t.CustomValues)
                .Include(t => t.ChecklistItems)
                .Include(t => t.Shares)
                .FirstOrDefaultAsync(t => t.Id == request.TemplateId) ?? throw new NotFoundException("Template not found.");

            var isAdmin = callerRole == UserRole.Administrator;
            if (!isAdmin && template.CreatedByUserId != callerId && !template.IsPublic && !template.Shares.Any(s => s.SharedWithUserId == callerId))
            {
                throw new ForbiddenException("You do not have access to this template.");
            }
            if (template.IsArchived)
            {
                throw new ValidationException("Archived templates cannot be used.");
            }

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId) ?? throw new NotFoundException("Project not found.");

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var anchorDate = request.StartDateOverride ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var actorName = await _db.Users.Where(u => u.Id == callerId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Someone";

                var startDate = template.StartOffsetDays is int so ? anchorDate.AddDays(so) : (DateOnly?)null;
                DateOnly? dueDate;
                if (template.DueOffsetDays is int dueOffset)
                {
                    dueDate = anchorDate.AddDays(dueOffset);
                }
                else if (startDate is DateOnly sd && template.EstimatedDurationDays is int duration)
                {
                    dueDate = sd.AddDays(duration);
                }
                else
                {
                    dueDate = null;
                }

                var assigneeId = ResolveAssignment(template.AssignmentMode, template.AssignedToUserId, project.OwnerId, null);

                var createdTask = await _taskService.CreateAsync(project.Id, new CreateTaskRequest
                {
                    Title = SubstituteVariables(template.Name, project.Name, anchorDate.ToString("yyyy-MM-dd"), actorName)!,
                    Description = SubstituteVariables(template.Description, project.Name, anchorDate.ToString("yyyy-MM-dd"), actorName),
                    Status = template.Status,
                    Priority = template.Priority,
                    AssignedToUserId = assigneeId,
                    StartDate = startDate,
                    DueDate = dueDate
                }, callerId, callerRole);

                foreach (var tagName in template.Tags.Select(t => t.Tag?.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    await _taskService.AddLabelAsync(createdTask.Id, tagName!, callerId, callerRole);
                }
                foreach (var customValue in template.CustomValues)
                {
                    await _taskService.SetCustomValueAsync(createdTask.Id, customValue.CustomFieldId, customValue.Value, callerId, callerRole);
                }
                if (template.ChecklistItems.Count > 0)
                {
                    _db.TaskChecklistItems.AddRange(template.ChecklistItems.OrderBy(c => c.SortOrder).Select(c => new TaskChecklistItem
                    { Id = Guid.NewGuid(), TaskId = createdTask.Id, Text = c.Text, SortOrder = c.SortOrder }));
                }

                template.UsageCount++;
                template.LastUsedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
                return createdTask.ToDto();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ProjectTemplate> LoadProjectTemplateForUseAsync(Guid id, Guid callerId, UserRole callerRole, bool requireNotArchived)
        {
            var template = await _db.ProjectTemplates
                .Include(t => t.Tasks).ThenInclude(tt => tt.Tags).ThenInclude(tg => tg.Tag)
                .Include(t => t.Tasks).ThenInclude(tt => tt.CustomValues)
                .Include(t => t.Tasks).ThenInclude(tt => tt.ChecklistItems)
                .Include(t => t.Dependencies)
                .Include(t => t.Shares)
                .FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException($"Template '{id}' was not found.");

            var isAdmin = callerRole == UserRole.Administrator;
            if (!isAdmin && template.CreatedByUserId != callerId && !template.IsPublic && !template.Shares.Any(s => s.SharedWithUserId == callerId))
            {
                throw new ForbiddenException("You do not have access to this template.");
            }
            if (requireNotArchived && template.IsArchived)
            {
                throw new ValidationException("Archived templates cannot be used.");
            }
            return template;
        }

        /// <summary>Parent-before-child order (a recursive pre-order walk) — the only ordering
        /// requirement for creation, since ITaskService.CreateSubtaskAsync needs its parent's
        /// REAL id to already exist. Within each level, tasks are created in the template's own
        /// SortOrder, so the resulting task order matches what the template author intended.</summary>
        private static List<ProjectTemplateTask> OrderForCreation(List<ProjectTemplateTask> tasks)
        {
            var byParent = tasks.ToLookup(t => t.ParentTemplateTaskId);
            var ordered = new List<ProjectTemplateTask>();

            void AddLevel(Guid? parentId)
            {
                foreach (var task in byParent[parentId].OrderBy(t => t.SortOrder))
                {
                    ordered.Add(task);
                    AddLevel(task.Id);
                }
            }

            AddLevel(null);
            return ordered;
        }

        /// <summary>Only three fixed, predefined tokens are ever recognized — a plain
        /// string.Replace, never an expression parser or code path capable of executing anything
        /// (spec #85/#86's own explicit "never execute code from template values" requirement is
        /// satisfied structurally, not by a sandboxing layer).</summary>
        private static string? SubstituteVariables(string? text, string projectName, string startDate, string projectManagerName)
        {
            if (text is null)
            {
                return null;
            }
            return text
                .Replace("{{ProjectName}}", projectName)
                .Replace("{{StartDate}}", startDate)
                .Replace("{{ProjectManager}}", projectManagerName);
        }

        /// <summary>globalOverride (from the wizard's "Configure Assignments" step) replaces the
        /// task's own AssignmentMode entirely when set — ProjectManager resolves to the created
        /// project's owner (the caller — see CreateProjectFromTemplateAsync's own doc comment on
        /// why there is no separate manager picker); SpecificUser uses the template's own stored
        /// AssignedToUserId; anything else (or no mode) is Unassigned.</summary>
        private static Guid? ResolveAssignment(string assignmentMode, Guid? specificUserId, Guid projectOwnerId, string? globalOverride)
        {
            var effectiveMode = globalOverride ?? assignmentMode;
            return effectiveMode switch
            {
                TemplateAssignmentModes.ProjectManager => projectOwnerId,
                TemplateAssignmentModes.SpecificUser => specificUserId,
                _ => null
            };
        }

        private async Task<ProjectDto> ReloadProjectDtoAsync(Guid projectId)
        {
            var project = await _db.Projects.Include(p => p.Owner).Include(p => p.Members).FirstAsync(p => p.Id == projectId);
            return project.ToDto();
        }
    }
}

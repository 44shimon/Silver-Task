using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Automation;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.AutomationParameters;
using Silver_Task.Server.Models.DTOs.Automations;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IAutomationService
    {
        Task<IReadOnlyList<Automation>> GetAllForProjectAsync(
            Guid projectId, Guid callerId, UserRole callerRole,
            string? search = null, AutomationTriggerType? triggerType = null, bool? isActive = null, Guid? createdByUserId = null);

        /// <summary>Administrator-only (see AutomationService's own doc comment on global
        /// automation permissions).</summary>
        Task<IReadOnlyList<Automation>> GetAllGlobalAsync(
            Guid callerId, UserRole callerRole,
            string? search = null, AutomationTriggerType? triggerType = null, bool? isActive = null, Guid? createdByUserId = null);

        Task<Automation> GetByIdAsync(Guid automationId, Guid callerId, UserRole callerRole);

        Task<Automation> CreateAsync(SaveAutomationRequest request, Guid callerId, UserRole callerRole);

        Task<Automation> UpdateAsync(Guid automationId, SaveAutomationRequest request, Guid callerId, UserRole callerRole);

        /// <summary>Soft delete (Automation.IsDeleted) — see that entity's own doc comment for why:
        /// AutomationExecution history must survive, per the spec's "prefer retaining execution
        /// history" instruction.</summary>
        Task DeleteAsync(Guid automationId, Guid callerId, UserRole callerRole);

        Task<Automation> SetActiveAsync(Guid automationId, bool isActive, Guid callerId, UserRole callerRole);

        Task<Automation> DuplicateAsync(Guid automationId, Guid callerId, UserRole callerRole);

        /// <summary>Called only by AutomationQueueBackgroundService — never inline during a normal
        /// request (see the spec's own "do not execute expensive automation chains synchronously"
        /// requirement).</summary>
        Task ProcessEventAsync(AutomationEventEnvelope envelope);

        Task<(IReadOnlyList<AutomationExecution> Items, int TotalCount)> GetRunsAsync(
            Guid automationId, Guid callerId, UserRole callerRole, int page, int pageSize);

        Task<AutomationExecution> RetryAsync(Guid executionId, Guid callerId, UserRole callerRole);

        /// <summary>Dry run — evaluates conditions against a real sample entity but never executes
        /// an action, per the spec's "without unexpectedly modifying production data" requirement.</summary>
        Task<AutomationTestResultDto> TestAsync(Guid automationId, Guid sampleEntityId, Guid callerId, UserRole callerRole);
    }

    /// <summary>
    /// The automation engine (Phase 35) — matches active, non-deleted automations against a
    /// dispatched event by (ProjectId or global) + TriggerType, evaluates their conditions (always
    /// AND-ed, see AutomationCondition's own doc comment) against freshly-loaded entity state, and
    /// executes their actions in order.
    ///
    /// Execution identity/security model: every action runs through the exact same service
    /// methods (ITaskService/ICommentService/IAttachmentService/INotificationService) a normal
    /// user request would use, authenticated as the automation's own CreatedByUserId — re-checked
    /// live (current UserRole, current ProjectMember row) on every single run, never cached from
    /// when the automation was created. This is deliberate: an automation cannot bypass
    /// project/task/file permissions in any way, because it never has its own "system" identity —
    /// it always acts exactly as capable (or as restricted) as whichever real user created it. If
    /// that user is later removed from the project, demoted, or deactivated, the automation's
    /// actions start failing with the same ForbiddenException a real request from that user would
    /// get — see EnsureCreatorIsUsableAsync.
    ///
    /// Loop protection: AutomationExecutionContext (ambient, AsyncLocal-based chain depth) is
    /// entered before invoking any action; TaskService/CommentService/etc. dispatch their own new
    /// events as a normal part of handling that call, and those events inherit depth+1
    /// automatically. Once depth exceeds MaxChainDepth, matching automations are skipped (not
    /// silently ignored — a Skipped AutomationExecution row records why).
    /// </summary>
    public class AutomationService(
        AppDbContext db,
        IProjectAccessService projectAccess,
        ITaskService taskService,
        ICommentService commentService,
        IAttachmentService attachmentService,
        INotificationService notificationService,
        ITagService tagService,
        IAutomationVariableResolver variableResolver,
        ILogger<AutomationService> logger) : IAutomationService
    {
        private const int MaxChainDepth = 10;
        private const int MaxTasksCreatedPerEvent = 10;
        private const int MaxNotificationsPerEvent = 100;
        private const int MaxRetries = 5;

        // PropertyNameCaseInsensitive so a camelCase request body (this API's usual convention,
        // set via Program.cs's controller-level JSON options, which don't apply to this private
        // manual (de)serialization) still deserializes correctly against these PascalCase
        // parameter record properties; CamelCase naming policy so what gets stored back into
        // ParametersJson (and later returned verbatim as a raw JsonElement — see
        // AutomationMappingExtensions.ToDto) matches that same camelCase convention on the way
        // out too. The bare (no-naming-policy) JsonStringEnumConverter matches Program.cs's own
        // global one exactly, so enum *values* here (e.g. "InProgress") stay consistent with
        // every other enum in this API — only object *keys* are camelCase, not enum values.
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() },
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ITaskService _taskService = taskService;
        private readonly ICommentService _commentService = commentService;
        private readonly IAttachmentService _attachmentService = attachmentService;
        private readonly INotificationService _notificationService = notificationService;
        private readonly ITagService _tagService = tagService;
        private readonly IAutomationVariableResolver _variableResolver = variableResolver;
        private readonly ILogger<AutomationService> _logger = logger;

        // ---------- CRUD ----------

        public async Task<IReadOnlyList<Automation>> GetAllForProjectAsync(
            Guid projectId, Guid callerId, UserRole callerRole,
            string? search = null, AutomationTriggerType? triggerType = null, bool? isActive = null, Guid? createdByUserId = null)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            return await QueryAutomationsAsync(a => a.ProjectId == projectId, search, triggerType, isActive, createdByUserId);
        }

        public async Task<IReadOnlyList<Automation>> GetAllGlobalAsync(
            Guid callerId, UserRole callerRole,
            string? search = null, AutomationTriggerType? triggerType = null, bool? isActive = null, Guid? createdByUserId = null)
        {
            if (callerRole != UserRole.Administrator)
            {
                throw new ForbiddenException("Only Administrators can view global automations.");
            }

            return await QueryAutomationsAsync(a => a.ProjectId == null, search, triggerType, isActive, createdByUserId);
        }

        private async Task<IReadOnlyList<Automation>> QueryAutomationsAsync(
            Func<Automation, bool> scopeFilter, string? search, AutomationTriggerType? triggerType, bool? isActive, Guid? createdByUserId)
        {
            var query = _db.Automations
                .Include(a => a.CreatedBy)
                .Include(a => a.Conditions)
                .Include(a => a.Actions)
                .Where(a => !a.IsDeleted)
                .AsEnumerable()
                .Where(scopeFilter)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.Trim();
                query = query.Where(a => a.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase));
            }
            if (triggerType.HasValue)
            {
                query = query.Where(a => a.TriggerType == triggerType.Value);
            }
            if (isActive.HasValue)
            {
                query = query.Where(a => a.IsActive == isActive.Value);
            }
            if (createdByUserId.HasValue)
            {
                query = query.Where(a => a.CreatedByUserId == createdByUserId.Value);
            }

            return [.. query.OrderByDescending(a => a.CreatedAt)];
        }

        public async Task<Automation> GetByIdAsync(Guid automationId, Guid callerId, UserRole callerRole)
        {
            var automation = await LoadAutomationAsync(automationId);
            await EnsureCanViewAutomationAsync(automation, callerId, callerRole);
            return automation;
        }

        public async Task<Automation> CreateAsync(SaveAutomationRequest request, Guid callerId, UserRole callerRole)
        {
            await EnsureCanManageAutomationsAsync(request.ProjectId, callerId, callerRole);

            var automation = new Automation
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                ProjectId = request.ProjectId,
                TriggerType = request.TriggerType,
                CreatedByUserId = callerId,
                IsActive = request.IsActive
            };

            automation.Conditions = await BuildConditionsAsync(request.ProjectId, request.TriggerType, request.Conditions);
            automation.Actions = await BuildActionsAsync(request.ProjectId, request.TriggerType, request.Actions);

            _db.Automations.Add(automation);
            await _db.SaveChangesAsync();

            automation.CreatedBy = await _db.Users.FindAsync(callerId);
            return automation;
        }

        public async Task<Automation> UpdateAsync(Guid automationId, SaveAutomationRequest request, Guid callerId, UserRole callerRole)
        {
            var automation = await LoadAutomationAsync(automationId);
            await EnsureCanManageAutomationsAsync(automation.ProjectId, callerId, callerRole);

            // ProjectId is deliberately immutable after creation — moving an automation between
            // "global" and "a specific project" (or between two projects) would mean re-checking
            // an entirely different permission scope for something that already exists; simplest
            // and safest is "create a new one" instead.
            if (request.ProjectId != automation.ProjectId)
            {
                throw new ValidationException("An automation's project scope cannot be changed after creation — create a new automation instead.");
            }

            automation.Name = request.Name.Trim();
            automation.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            automation.TriggerType = request.TriggerType;
            automation.IsActive = request.IsActive;
            automation.UpdatedAt = DateTime.UtcNow;

            _db.AutomationConditions.RemoveRange(automation.Conditions);
            _db.AutomationActions.RemoveRange(automation.Actions);
            automation.Conditions = await BuildConditionsAsync(automation.ProjectId, request.TriggerType, request.Conditions);
            automation.Actions = await BuildActionsAsync(automation.ProjectId, request.TriggerType, request.Actions);

            await _db.SaveChangesAsync();
            return automation;
        }

        public async Task DeleteAsync(Guid automationId, Guid callerId, UserRole callerRole)
        {
            var automation = await LoadAutomationAsync(automationId);
            await EnsureCanManageAutomationsAsync(automation.ProjectId, callerId, callerRole);

            automation.IsDeleted = true;
            automation.IsActive = false;
            automation.DeletedAt = DateTime.UtcNow;
            automation.DeletedByUserId = callerId;
            automation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<Automation> SetActiveAsync(Guid automationId, bool isActive, Guid callerId, UserRole callerRole)
        {
            var automation = await LoadAutomationAsync(automationId);
            await EnsureCanManageAutomationsAsync(automation.ProjectId, callerId, callerRole);

            automation.IsActive = isActive;
            automation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return automation;
        }

        public async Task<Automation> DuplicateAsync(Guid automationId, Guid callerId, UserRole callerRole)
        {
            var original = await LoadAutomationAsync(automationId);
            await EnsureCanManageAutomationsAsync(original.ProjectId, callerId, callerRole);

            var copy = new Automation
            {
                Id = Guid.NewGuid(),
                Name = $"{original.Name} (Copy)",
                Description = original.Description,
                ProjectId = original.ProjectId,
                TriggerType = original.TriggerType,
                CreatedByUserId = callerId,
                // Duplicates start disabled — a user reviewing/renaming conditions before it goes
                // live is the safer default than an untouched copy silently running immediately.
                IsActive = false,
                Conditions = [.. original.Conditions.Select(c => new AutomationCondition
                {
                    Id = Guid.NewGuid(), Field = c.Field, Operator = c.Operator, Value = c.Value, SortOrder = c.SortOrder
                })],
                Actions = [.. original.Actions.Select(a => new AutomationAction
                {
                    Id = Guid.NewGuid(), ActionType = a.ActionType, ParametersJson = a.ParametersJson, SortOrder = a.SortOrder
                })]
            };

            _db.Automations.Add(copy);
            await _db.SaveChangesAsync();

            copy.CreatedBy = await _db.Users.FindAsync(callerId);
            return copy;
        }

        // ---------- Validation ----------

        private async Task<List<AutomationCondition>> BuildConditionsAsync(
            Guid? projectId, AutomationTriggerType triggerType, List<AutomationConditionRequest> requests)
        {
            var result = new List<AutomationCondition>();
            foreach (var request in requests)
            {
                if (!AutomationFields.IsValidField(triggerType, request.Field))
                {
                    throw new ValidationException($"'{request.Field}' is not a valid condition field for this trigger.");
                }

                await ValidateConditionValueAsync(projectId, request.Field, request.Operator, request.Value);

                result.Add(new AutomationCondition
                {
                    Id = Guid.NewGuid(),
                    Field = request.Field,
                    Operator = request.Operator,
                    Value = request.Value,
                    SortOrder = result.Count
                });
            }
            return result;
        }

        private async Task ValidateConditionValueAsync(Guid? projectId, string field, AutomationConditionOperator op, string? value)
        {
            if (op is AutomationConditionOperator.IsEmpty or AutomationConditionOperator.IsNotEmpty)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException($"A value is required for the '{op}' operator.");
            }

            switch (field)
            {
                case AutomationFields.TaskStatus:
                    if (!Enum.TryParse<TaskItemStatus>(value, out _))
                    {
                        throw new ValidationException($"'{value}' is not a valid task status.");
                    }
                    break;

                case AutomationFields.TaskPriority:
                    if (!Enum.TryParse<TaskPriority>(value, out _))
                    {
                        throw new ValidationException($"'{value}' is not a valid task priority.");
                    }
                    break;

                case AutomationFields.TaskAssigneeId:
                case AutomationFields.TaskCreatorId:
                case AutomationFields.FileUploadedByUserId:
                case AutomationFields.ProjectOwnerId:
                    if (!Guid.TryParse(value, out var userId) || !await _db.Users.AnyAsync(u => u.Id == userId))
                    {
                        throw new ValidationException($"'{value}' is not a valid user.");
                    }
                    break;

                case AutomationFields.TaskProjectId:
                case AutomationFields.FileProjectId:
                    if (!Guid.TryParse(value, out var refProjectId) || !await _db.Projects.AnyAsync(p => p.Id == refProjectId))
                    {
                        throw new ValidationException($"'{value}' is not a valid project.");
                    }
                    break;

                case AutomationFields.FileCategoryId:
                    if (!Guid.TryParse(value, out var categoryId) || !await _db.FileCategories.AnyAsync(c => c.Id == categoryId))
                    {
                        throw new ValidationException($"'{value}' is not a valid file category.");
                    }
                    break;

                case AutomationFields.TaskDueDate:
                case AutomationFields.TaskStartDate:
                    if (!DateOnly.TryParse(value, out _))
                    {
                        throw new ValidationException($"'{value}' is not a valid date (expected YYYY-MM-DD).");
                    }
                    break;

                case AutomationFields.TaskAllSiblingSubtasksComplete:
                    if (value is not ("true" or "false"))
                    {
                        throw new ValidationException("This condition's value must be 'true' or 'false'.");
                    }
                    break;

                default:
                    if (field.StartsWith(AutomationFields.TaskCustomFieldPrefix, StringComparison.Ordinal))
                    {
                        var fieldIdText = field[AutomationFields.TaskCustomFieldPrefix.Length..];
                        if (!Guid.TryParse(fieldIdText, out var customFieldId) ||
                            !await _db.CustomFields.AnyAsync(f => f.Id == customFieldId && (f.ProjectId == projectId || f.ProjectId == null)))
                        {
                            throw new ValidationException("The referenced custom field does not exist for this project.");
                        }
                    }
                    // Free-text fields (Title, Description, FileName, Name, Labels, Tags, FileType) accept any string.
                    break;
            }
        }

        private static readonly IReadOnlyDictionary<AutomationActionType, AutomationTriggerType[]> ActionRequiresOneOfTriggerCategory = new Dictionary<AutomationActionType, AutomationTriggerType[]>
        {
            // File-only action
            [AutomationActionType.AddFileTag] = [AutomationTriggerType.FileUploaded, AutomationTriggerType.FileTagged],
        };

        private static readonly IReadOnlyList<AutomationTriggerType> TaskTriggers =
        [
            AutomationTriggerType.TaskCreated, AutomationTriggerType.TaskUpdated, AutomationTriggerType.TaskCompleted,
            AutomationTriggerType.TaskReopened, AutomationTriggerType.TaskAssigned, AutomationTriggerType.TaskOverdue,
            AutomationTriggerType.CommentAdded, AutomationTriggerType.SubtaskCompleted
        ];

        private async Task<List<AutomationAction>> BuildActionsAsync(
            Guid? projectId, AutomationTriggerType triggerType, List<AutomationActionRequest> requests)
        {
            if (requests.Count == 0)
            {
                throw new ValidationException("At least one action is required.");
            }

            var result = new List<AutomationAction>();
            foreach (var request in requests)
            {
                EnsureActionCompatibleWithTrigger(request.ActionType, triggerType);
                var json = await ValidateAndSerializeActionAsync(projectId, request.ActionType, request.Parameters);
                result.Add(new AutomationAction { Id = Guid.NewGuid(), ActionType = request.ActionType, ParametersJson = json, SortOrder = result.Count });
            }
            return result;
        }

        private static void EnsureActionCompatibleWithTrigger(AutomationActionType actionType, AutomationTriggerType triggerType)
        {
            if (actionType == AutomationActionType.AddFileTag)
            {
                if (!ActionRequiresOneOfTriggerCategory[actionType].Contains(triggerType))
                {
                    throw new ValidationException("Add File Tag can only be used with a file-related trigger.");
                }
                return;
            }

            // Every other action operates on "the task in context" — valid for any task-category
            // trigger. ProjectCreated has no task, so task-targeting actions never apply to it.
            if (!TaskTriggers.Contains(triggerType))
            {
                throw new ValidationException($"'{actionType}' requires a task-related trigger.");
            }
        }

        private async Task<string> ValidateAndSerializeActionAsync(Guid? projectId, AutomationActionType actionType, JsonElement raw)
        {
            switch (actionType)
            {
                case AutomationActionType.AssignTask:
                {
                    var p = Deserialize<AssignTaskParameters>(raw);
                    if (p.AssignMode == AutomationUserSelector.None)
                    {
                        throw new ValidationException("Assign Task requires a target user or Project Manager.");
                    }
                    await ValidateUserSelectorAsync(p.AssignMode, p.TargetUserId);
                    return Serialize(p);
                }
                case AutomationActionType.ChangeStatus:
                {
                    var p = Deserialize<ChangeStatusParameters>(raw);
                    return Serialize(p);
                }
                case AutomationActionType.ChangePriority:
                {
                    var p = Deserialize<ChangePriorityParameters>(raw);
                    return Serialize(p);
                }
                case AutomationActionType.AddLabel:
                {
                    var p = Deserialize<AddLabelParameters>(raw);
                    if (string.IsNullOrWhiteSpace(p.TagName))
                    {
                        throw new ValidationException("A label name is required.");
                    }
                    return Serialize(p);
                }
                case AutomationActionType.RemoveLabel:
                {
                    var p = Deserialize<RemoveLabelParameters>(raw);
                    if (string.IsNullOrWhiteSpace(p.TagName))
                    {
                        throw new ValidationException("A label name is required.");
                    }
                    return Serialize(p);
                }
                case AutomationActionType.SetDueDate:
                {
                    var p = Deserialize<SetDueDateParameters>(raw);
                    if (!p.ClearDate && p.OffsetDays is null)
                    {
                        throw new ValidationException("Set Due Date requires either an offset or Clear Date.");
                    }
                    return Serialize(p);
                }
                case AutomationActionType.SetStartDate:
                {
                    var p = Deserialize<SetStartDateParameters>(raw);
                    if (!p.ClearDate && p.OffsetDays is null)
                    {
                        throw new ValidationException("Set Start Date requires either an offset or Clear Date.");
                    }
                    return Serialize(p);
                }
                case AutomationActionType.AddComment:
                {
                    var p = Deserialize<AddCommentParameters>(raw);
                    if (string.IsNullOrWhiteSpace(p.CommentTemplate))
                    {
                        throw new ValidationException("A comment template is required.");
                    }
                    return Serialize(p);
                }
                case AutomationActionType.CreateTask:
                {
                    var p = Deserialize<CreateTaskParameters>(raw);
                    if (string.IsNullOrWhiteSpace(p.TitleTemplate))
                    {
                        throw new ValidationException("A title template is required for Create Task.");
                    }
                    await ValidateUserSelectorAsync(p.AssignMode, p.TargetUserId);
                    return Serialize(p);
                }
                case AutomationActionType.SendNotification:
                {
                    var p = Deserialize<SendNotificationParameters>(raw);
                    if (string.IsNullOrWhiteSpace(p.MessageTemplate))
                    {
                        throw new ValidationException("A notification message is required.");
                    }
                    if (p.RecipientMode == AutomationUserSelector.None)
                    {
                        throw new ValidationException("Send Notification requires a recipient.");
                    }
                    await ValidateUserSelectorAsync(p.RecipientMode, p.TargetUserId);
                    return Serialize(p);
                }
                case AutomationActionType.AddFileTag:
                {
                    var p = Deserialize<AddFileTagParameters>(raw);
                    if (string.IsNullOrWhiteSpace(p.TagName))
                    {
                        throw new ValidationException("A tag name is required.");
                    }
                    return Serialize(p);
                }
                default:
                    throw new ValidationException($"Unsupported action type '{actionType}'.");
            }
        }

        private async Task ValidateUserSelectorAsync(AutomationUserSelector mode, Guid? targetUserId)
        {
            if (mode == AutomationUserSelector.SpecificUser)
            {
                if (targetUserId is not Guid userId || !await _db.Users.AnyAsync(u => u.Id == userId))
                {
                    throw new ValidationException("A valid target user is required.");
                }
            }
        }

        private static T Deserialize<T>(JsonElement raw)
        {
            try
            {
                return raw.Deserialize<T>(JsonOptions) ?? throw new ValidationException("Action parameters cannot be empty.");
            }
            catch (JsonException)
            {
                throw new ValidationException("Action parameters are not in the expected shape.");
            }
        }

        private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

        // ---------- Permission tiers ----------

        /// <summary>Global automations (ProjectId null) require Administrator — the spec's own
        /// "global automations require appropriate administrative permissions" rule; project
        /// automations use the same Manage tier as every other project-configuration concern
        /// (custom field definitions, project settings) — deliberately uniform, not relaxed for
        /// the automation's own creator, given how impactful an automation's actions can be
        /// (assigning tasks, creating tasks, notifying users) — see the class's own doc comment
        /// on the "extremely important" execution-security requirement.</summary>
        private async Task EnsureCanManageAutomationsAsync(Guid? projectId, Guid callerId, UserRole callerRole)
        {
            if (projectId is Guid pid)
            {
                var project = await LoadProjectAsync(pid);
                await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);
            }
            else if (callerRole != UserRole.Administrator)
            {
                throw new ForbiddenException("Only Administrators can manage global automations.");
            }
        }

        private async Task EnsureCanViewAutomationAsync(Automation automation, Guid callerId, UserRole callerRole)
        {
            if (automation.ProjectId is Guid projectId)
            {
                var project = await LoadProjectAsync(projectId);
                await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);
            }
            else if (callerRole != UserRole.Administrator)
            {
                throw new ForbiddenException("Only Administrators can view global automations.");
            }
        }

        // ---------- Execution engine ----------

        public async Task ProcessEventAsync(AutomationEventEnvelope envelope)
        {
            var candidates = await _db.Automations
                .Include(a => a.Conditions)
                .Include(a => a.Actions)
                .Where(a => a.IsActive && !a.IsDeleted && a.TriggerType == envelope.Event.TriggerType &&
                            (a.ProjectId == envelope.Event.ProjectId || a.ProjectId == null))
                .ToListAsync();

            if (candidates.Count == 0)
            {
                return;
            }

            var rateLimits = new AutomationRateLimitTracker();

            foreach (var automation in candidates)
            {
                var alreadyProcessed = await _db.AutomationExecutions
                    .AnyAsync(e => e.AutomationId == automation.Id && e.TriggerEventId == envelope.EventId);
                if (alreadyProcessed)
                {
                    continue;
                }

                if (envelope.ChainDepth > MaxChainDepth)
                {
                    await RecordExecutionAsync(
                        automation, envelope, AutomationExecutionStatus.Skipped, ResolveEntityId(envelope.Event),
                        "Automation chain stopped because maximum execution depth was reached.", null, DateTime.UtcNow, 0);
                    continue;
                }

                await ExecuteAutomationAsync(automation, envelope, rateLimits);
            }
        }

        private async Task ExecuteAutomationAsync(Automation automation, AutomationEventEnvelope envelope, AutomationRateLimitTracker rateLimits)
        {
            var startedAt = DateTime.UtcNow;
            var entityId = ResolveEntityId(envelope.Event);

            try
            {
                var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == automation.CreatedByUserId);
                if (creator is null || !creator.IsActive || creator.IsDeleted)
                {
                    await RecordExecutionAsync(
                        automation, envelope, AutomationExecutionStatus.Failed, entityId,
                        "This automation's owner account is inactive or no longer exists.", null, startedAt, ElapsedMs(startedAt));
                    return;
                }

                var context = await BuildContextAsync(envelope.Event.TriggerType, entityId);
                if (context is null)
                {
                    // The triggering entity no longer exists (e.g. deleted between dispatch and
                    // processing) — nothing to evaluate against, and not an error.
                    return;
                }

                if (!EvaluateConditions(automation.Conditions, context))
                {
                    return;
                }

                var resultParts = new List<string>();
                using (AutomationExecutionContext.EnterChain(envelope.ChainDepth + 1))
                {
                    foreach (var action in automation.Actions.OrderBy(a => a.SortOrder))
                    {
                        resultParts.Add(await ExecuteActionAsync(automation, action, context, creator, rateLimits));
                    }
                }

                automation.LastRunAt = DateTime.UtcNow;
                automation.RunCount++;
                automation.LastError = null;
                await _db.SaveChangesAsync();

                await RecordExecutionAsync(
                    automation, envelope, AutomationExecutionStatus.Success, entityId, null,
                    string.Join("; ", resultParts), startedAt, ElapsedMs(startedAt));
            }
            catch (Exception ex)
            {
                automation.LastError = ex.Message;
                automation.LastRunAt = DateTime.UtcNow;
                automation.RunCount++;
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch
                {
                    // Best-effort — the execution row below is the authoritative failure record
                    // regardless of whether this bookkeeping save succeeds.
                }

                await RecordExecutionAsync(automation, envelope, AutomationExecutionStatus.Failed, entityId, ex.Message, null, startedAt, ElapsedMs(startedAt));
                _logger.LogError(ex, "Automation {AutomationId} failed while processing event {EventId}.", automation.Id, envelope.EventId);
            }
        }

        private static int ElapsedMs(DateTime startedAt) => (int)Math.Max(0, (DateTime.UtcNow - startedAt).TotalMilliseconds);

        private async Task RecordExecutionAsync(
            Automation automation, AutomationEventEnvelope envelope, AutomationExecutionStatus status,
            Guid? entityId, string? error, string? summary, DateTime startedAt, int durationMs, Guid? retryOfExecutionId = null)
        {
            _db.AutomationExecutions.Add(new AutomationExecution
            {
                Id = Guid.NewGuid(),
                AutomationId = automation.Id,
                TriggerEventId = envelope.EventId,
                ChainDepth = envelope.ChainDepth,
                EntityId = entityId,
                Status = status,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                DurationMs = durationMs,
                ErrorMessage = error,
                ResultSummary = summary,
                RetryOfExecutionId = retryOfExecutionId
            });
            await _db.SaveChangesAsync();
        }

        private static Guid? ResolveEntityId(IAutomationEvent evt) => evt switch
        {
            TaskCreatedEvent e => e.TaskId,
            TaskUpdatedEvent e => e.TaskId,
            TaskCompletedEvent e => e.TaskId,
            TaskReopenedEvent e => e.TaskId,
            TaskAssignedEvent e => e.TaskId,
            TaskOverdueEvent e => e.TaskId,
            CommentAddedEvent e => e.CommentId,
            FileUploadedEvent e => e.FileId,
            FileTaggedEvent e => e.FileId,
            SubtaskCompletedEvent e => e.SubtaskId,
            ProjectCreatedEvent e => e.ProjectId,
            _ => null
        };

        // ---------- Context loading ----------

        private async Task<AutomationEvaluationContext?> BuildContextAsync(AutomationTriggerType triggerType, Guid? entityId)
        {
            if (entityId is not Guid id)
            {
                return null;
            }

            return triggerType switch
            {
                AutomationTriggerType.FileUploaded or AutomationTriggerType.FileTagged => await LoadFileContextAsync(id),
                AutomationTriggerType.ProjectCreated => await LoadProjectContextAsync(id),
                AutomationTriggerType.CommentAdded => await LoadTaskContextFromCommentAsync(id),
                AutomationTriggerType.SubtaskCompleted => await LoadTaskContextFromSubtaskAsync(id),
                _ => await LoadTaskContextAsync(id)
            };
        }

        private async Task<AutomationEvaluationContext?> LoadTaskContextAsync(Guid taskId)
        {
            var task = await _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Include(t => t.CustomValues)
                .Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
                .FirstOrDefaultAsync(t => t.Id == taskId);
            if (task is null)
            {
                return null;
            }

            var creatorId = await _db.TaskActivities
                .Where(a => a.TaskId == taskId && a.Action == "Created")
                .Select(a => a.UserId)
                .FirstOrDefaultAsync();

            // "Are this task's own subtasks all complete" — not "are this task's siblings all
            // complete". This matters specifically for the SubtaskCompleted trigger, where the
            // task in context is the *parent* (see LoadTaskContextFromSubtaskAsync), so the
            // question being asked is always about the parent's children, never its own siblings.
            bool? allSiblingsComplete = null;
            if (await _db.Tasks.AnyAsync(t => t.ParentTaskId == task.Id))
            {
                allSiblingsComplete = !await _db.Tasks.AnyAsync(t => t.ParentTaskId == task.Id && t.Status != TaskItemStatus.Complete);
            }

            return new AutomationEvaluationContext
            {
                Task = task,
                ProjectId = task.ProjectId,
                ProjectOwnerId = task.Project!.OwnerId,
                TaskCreatorId = creatorId,
                AllSiblingSubtasksComplete = allSiblingsComplete
            };
        }

        private async Task<AutomationEvaluationContext?> LoadTaskContextFromCommentAsync(Guid commentId)
        {
            var taskId = await _db.TaskComments.Where(c => c.Id == commentId).Select(c => c.TaskId).FirstOrDefaultAsync();
            return taskId == Guid.Empty ? null : await LoadTaskContextAsync(taskId);
        }

        private async Task<AutomationEvaluationContext?> LoadTaskContextFromSubtaskAsync(Guid subtaskId)
        {
            var parentId = await _db.Tasks.Where(t => t.Id == subtaskId).Select(t => t.ParentTaskId).FirstOrDefaultAsync();
            return parentId is Guid pid ? await LoadTaskContextAsync(pid) : null;
        }

        private async Task<AutomationEvaluationContext?> LoadFileContextAsync(Guid fileId)
        {
            var file = await _db.Attachments
                .Include(a => a.Category)
                .Include(a => a.FileTags).ThenInclude(ft => ft.Tag)
                .Include(a => a.Project)
                .Include(a => a.Task).ThenInclude(t => t!.Project)
                .Include(a => a.Comment).ThenInclude(c => c!.Task).ThenInclude(t => t!.Project)
                .FirstOrDefaultAsync(a => a.Id == fileId);
            if (file is null)
            {
                return null;
            }

            Guid projectId;
            Guid ownerId;
            if (file.Project is Project project)
            {
                (projectId, ownerId) = (project.Id, project.OwnerId);
            }
            else if (file.Task is TaskItem task)
            {
                (projectId, ownerId) = (task.ProjectId, task.Project!.OwnerId);
            }
            else if (file.Comment?.Task is TaskItem commentTask)
            {
                (projectId, ownerId) = (commentTask.ProjectId, commentTask.Project!.OwnerId);
            }
            else
            {
                return null;
            }

            return new AutomationEvaluationContext { File = file, ProjectId = projectId, ProjectOwnerId = ownerId };
        }

        private async Task<AutomationEvaluationContext?> LoadProjectContextAsync(Guid projectId)
        {
            var project = await _db.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == projectId);
            return project is null ? null : new AutomationEvaluationContext { Project = project, ProjectId = project.Id, ProjectOwnerId = project.OwnerId };
        }

        // ---------- Condition evaluation ----------

        private static bool EvaluateConditions(IEnumerable<AutomationCondition> conditions, AutomationEvaluationContext context)
        {
            foreach (var condition in conditions)
            {
                var actual = ResolveFieldValue(condition.Field, context);
                if (!CompareValues(condition.Operator, actual, condition.Value, condition.Field))
                {
                    return false;
                }
            }
            return true;
        }

        private static string? ResolveFieldValue(string field, AutomationEvaluationContext context)
        {
            if (field.StartsWith(AutomationFields.TaskCustomFieldPrefix, StringComparison.Ordinal))
            {
                var fieldIdText = field[AutomationFields.TaskCustomFieldPrefix.Length..];
                return Guid.TryParse(fieldIdText, out var fieldId)
                    ? context.Task?.CustomValues.FirstOrDefault(v => v.CustomFieldId == fieldId)?.Value
                    : null;
            }

            return field switch
            {
                AutomationFields.TaskTitle => context.Task?.Title,
                AutomationFields.TaskDescription => context.Task?.Description,
                AutomationFields.TaskStatus => context.Task?.Status.ToString(),
                AutomationFields.TaskPriority => context.Task?.Priority.ToString(),
                AutomationFields.TaskAssigneeId => context.Task?.AssignedToUserId?.ToString(),
                AutomationFields.TaskCreatorId => context.TaskCreatorId?.ToString(),
                AutomationFields.TaskDueDate => context.Task?.DueDate?.ToString("yyyy-MM-dd"),
                AutomationFields.TaskStartDate => context.Task?.StartDate?.ToString("yyyy-MM-dd"),
                AutomationFields.TaskProjectId => context.Task?.ProjectId.ToString(),
                AutomationFields.TaskParentTaskId => context.Task?.ParentTaskId?.ToString(),
                AutomationFields.TaskLabels => context.Task is null ? null : string.Join(",", context.Task.TaskTags.Select(tt => tt.Tag!.Name)),
                AutomationFields.TaskAllSiblingSubtasksComplete => context.AllSiblingSubtasksComplete?.ToString().ToLowerInvariant(),

                AutomationFields.FileFileName => context.File?.FileName,
                AutomationFields.FileCategoryId => context.File?.CategoryId?.ToString(),
                AutomationFields.FileTags => context.File is null ? null : string.Join(",", context.File.FileTags.Select(ft => ft.Tag!.Name)),
                AutomationFields.FileType => context.File is null ? null : AttachmentTypeClassifier.Classify(context.File.MimeType),
                AutomationFields.FileUploadedByUserId => context.File?.UploadedByUserId.ToString(),
                AutomationFields.FileProjectId => context.ProjectId.ToString(),
                AutomationFields.FileTaskId => context.File?.TaskId?.ToString(),

                AutomationFields.ProjectName => context.Project?.Name,
                AutomationFields.ProjectStatus => context.Project is null ? null : (context.Project.IsArchived ? "Archived" : "Active"),
                AutomationFields.ProjectOwnerId => context.Project?.OwnerId.ToString(),

                _ => null
            };
        }

        private static bool CompareValues(AutomationConditionOperator op, string? actual, string? expected, string field)
        {
            if (op == AutomationConditionOperator.IsEmpty)
            {
                return string.IsNullOrEmpty(actual);
            }
            if (op == AutomationConditionOperator.IsNotEmpty)
            {
                return !string.IsNullOrEmpty(actual);
            }
            if (expected is null)
            {
                // An incomplete condition never matches — fail safe, not fail open.
                return false;
            }

            return op switch
            {
                AutomationConditionOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                AutomationConditionOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                AutomationConditionOperator.Contains => actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) ?? false,
                AutomationConditionOperator.NotContains => !(actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) ?? false),
                AutomationConditionOperator.GreaterThan or AutomationConditionOperator.LessThan or
                AutomationConditionOperator.GreaterThanOrEqual or AutomationConditionOperator.LessThanOrEqual or
                AutomationConditionOperator.Before or AutomationConditionOperator.After => CompareOrdered(op, actual, expected, field),
                _ => false
            };
        }

        private static bool CompareOrdered(AutomationConditionOperator op, string? actual, string expected, string field)
        {
            if (field is AutomationFields.TaskDueDate or AutomationFields.TaskStartDate)
            {
                if (!DateOnly.TryParse(actual, out var actualDate) || !DateOnly.TryParse(expected, out var expectedDate))
                {
                    return false;
                }
                return op switch
                {
                    AutomationConditionOperator.GreaterThan or AutomationConditionOperator.After => actualDate > expectedDate,
                    AutomationConditionOperator.LessThan or AutomationConditionOperator.Before => actualDate < expectedDate,
                    AutomationConditionOperator.GreaterThanOrEqual => actualDate >= expectedDate,
                    AutomationConditionOperator.LessThanOrEqual => actualDate <= expectedDate,
                    _ => false
                };
            }

            if (decimal.TryParse(actual, out var actualNum) && decimal.TryParse(expected, out var expectedNum))
            {
                return op switch
                {
                    AutomationConditionOperator.GreaterThan or AutomationConditionOperator.After => actualNum > expectedNum,
                    AutomationConditionOperator.LessThan or AutomationConditionOperator.Before => actualNum < expectedNum,
                    AutomationConditionOperator.GreaterThanOrEqual => actualNum >= expectedNum,
                    AutomationConditionOperator.LessThanOrEqual => actualNum <= expectedNum,
                    _ => false
                };
            }

            var cmp = string.CompareOrdinal(actual, expected);
            return op switch
            {
                AutomationConditionOperator.GreaterThan or AutomationConditionOperator.After => cmp > 0,
                AutomationConditionOperator.LessThan or AutomationConditionOperator.Before => cmp < 0,
                AutomationConditionOperator.GreaterThanOrEqual => cmp >= 0,
                AutomationConditionOperator.LessThanOrEqual => cmp <= 0,
                _ => false
            };
        }

        // ---------- Action execution ----------

        private async Task<string> ExecuteActionAsync(
            Automation automation, AutomationAction action, AutomationEvaluationContext context, User creator, AutomationRateLimitTracker rateLimits)
        {
            switch (action.ActionType)
            {
                case AutomationActionType.AssignTask:
                {
                    var p = Deserialize<AssignTaskParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    var targetUserId = ResolveUserSelector(p.AssignMode, p.TargetUserId, context)
                        ?? throw new InvalidOperationException("Could not resolve a target user for Assign Task.");
                    var request = CloneRequest(task);
                    request.AssignedToUserId = targetUserId;
                    var updated = await _taskService.UpdateAsync(task.Id, request, creator.Id, creator.Role);
                    context.Task = updated;
                    var name = (await _db.Users.FindAsync(targetUserId))?.Name ?? targetUserId.ToString();
                    return $"Assigned to {name}";
                }
                case AutomationActionType.ChangeStatus:
                {
                    var p = Deserialize<ChangeStatusParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    var request = CloneRequest(task);
                    request.Status = p.NewStatus;
                    var updated = await _taskService.UpdateAsync(task.Id, request, creator.Id, creator.Role);
                    context.Task = updated;
                    return $"Status changed to {p.NewStatus}";
                }
                case AutomationActionType.ChangePriority:
                {
                    var p = Deserialize<ChangePriorityParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    var request = CloneRequest(task);
                    request.Priority = p.NewPriority;
                    var updated = await _taskService.UpdateAsync(task.Id, request, creator.Id, creator.Role);
                    context.Task = updated;
                    return $"Priority changed to {p.NewPriority}";
                }
                case AutomationActionType.AddLabel:
                {
                    var p = Deserialize<AddLabelParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    await _taskService.AddLabelAsync(task.Id, p.TagName, creator.Id, creator.Role);
                    return $"Added label '{p.TagName}'";
                }
                case AutomationActionType.RemoveLabel:
                {
                    var p = Deserialize<RemoveLabelParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == p.TagName.Trim().ToLower());
                    if (tag is not null)
                    {
                        await _taskService.RemoveLabelAsync(task.Id, tag.Id, creator.Id, creator.Role);
                    }
                    return $"Removed label '{p.TagName}'";
                }
                case AutomationActionType.SetDueDate:
                {
                    var p = Deserialize<SetDueDateParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    var request = CloneRequest(task);
                    request.DueDate = p.ClearDate ? null : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(p.OffsetDays ?? 0));
                    var updated = await _taskService.UpdateAsync(task.Id, request, creator.Id, creator.Role);
                    context.Task = updated;
                    return request.DueDate is DateOnly d ? $"Due date set to {d:yyyy-MM-dd}" : "Due date cleared";
                }
                case AutomationActionType.SetStartDate:
                {
                    var p = Deserialize<SetStartDateParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    var request = CloneRequest(task);
                    request.StartDate = p.ClearDate ? null : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(p.OffsetDays ?? 0));
                    var updated = await _taskService.UpdateAsync(task.Id, request, creator.Id, creator.Role);
                    context.Task = updated;
                    return request.StartDate is DateOnly d ? $"Start date set to {d:yyyy-MM-dd}" : "Start date cleared";
                }
                case AutomationActionType.AddComment:
                {
                    var p = Deserialize<AddCommentParameters>(ParseJson(action.ParametersJson));
                    var task = RequireTask(context);
                    var text = _variableResolver.Resolve(p.CommentTemplate, new AutomationVariableContext { Task = task, ActingUser = creator });
                    await _commentService.CreateAutomatedAsync(task.Id, text, automation.Id, creator.Id);
                    return "Added comment";
                }
                case AutomationActionType.CreateTask:
                {
                    if (rateLimits.TasksCreated >= MaxTasksCreatedPerEvent)
                    {
                        return "Skipped Create Task (rate limit reached)";
                    }
                    var p = Deserialize<CreateTaskParameters>(ParseJson(action.ParametersJson));
                    var variableContext = new AutomationVariableContext { Task = context.Task, ActingUser = creator };
                    var title = _variableResolver.Resolve(p.TitleTemplate, variableContext);
                    var description = p.DescriptionTemplate is null ? null : _variableResolver.Resolve(p.DescriptionTemplate, variableContext);
                    var assigneeId = ResolveUserSelector(p.AssignMode, p.TargetUserId, context);
                    var dueDate = p.DueDateOffsetDays is int days ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days)) : (DateOnly?)null;
                    var request = new CreateTaskRequest
                    {
                        Title = title,
                        Description = description,
                        Status = p.Status,
                        Priority = p.Priority,
                        AssignedToUserId = assigneeId,
                        DueDate = dueDate
                    };
                    var newTask = await _taskService.CreateAsync(context.ProjectId, request, creator.Id, creator.Role);
                    rateLimits.TasksCreated++;
                    return $"Created task '{newTask.Title}'";
                }
                case AutomationActionType.SendNotification:
                {
                    if (rateLimits.NotificationsSent >= MaxNotificationsPerEvent)
                    {
                        return "Skipped Send Notification (rate limit reached)";
                    }
                    var p = Deserialize<SendNotificationParameters>(ParseJson(action.ParametersJson));
                    var recipientId = ResolveUserSelector(p.RecipientMode, p.TargetUserId, context);
                    if (recipientId is not Guid recipient)
                    {
                        return "Skipped Send Notification (no recipient resolved)";
                    }
                    var message = _variableResolver.Resolve(
                        p.MessageTemplate, new AutomationVariableContext { Task = context.Task, ActingUser = creator });
                    await _notificationService.NotifyAsync(
                        recipient, creator.Id, NotificationTypes.AutomationNotification, $"Automation: {automation.Name}",
                        message, context.Task?.Id, context.ProjectId);
                    await _db.SaveChangesAsync();
                    rateLimits.NotificationsSent++;
                    return "Sent notification";
                }
                case AutomationActionType.AddFileTag:
                {
                    var p = Deserialize<AddFileTagParameters>(ParseJson(action.ParametersJson));
                    var file = RequireFile(context);
                    await _attachmentService.AddTagAsync(file.Id, p.TagName, creator.Id, creator.Role);
                    return $"Added file tag '{p.TagName}'";
                }
                default:
                    throw new InvalidOperationException($"Unsupported action type {action.ActionType}.");
            }
        }

        private static JsonElement ParseJson(string json) => JsonDocument.Parse(json).RootElement;

        private static Guid? ResolveUserSelector(AutomationUserSelector mode, Guid? targetUserId, AutomationEvaluationContext context) => mode switch
        {
            AutomationUserSelector.SpecificUser => targetUserId,
            AutomationUserSelector.TaskAssignee => context.Task?.AssignedToUserId,
            AutomationUserSelector.ProjectManager => context.ProjectOwnerId,
            _ => null
        };

        private static TaskItem RequireTask(AutomationEvaluationContext context) =>
            context.Task ?? throw new InvalidOperationException("This action requires a task, but the triggering event has none.");

        private static Attachment RequireFile(AutomationEvaluationContext context) =>
            context.File ?? throw new InvalidOperationException("This action requires a file, but the triggering event has none.");

        private static UpdateTaskRequest CloneRequest(TaskItem task) => new()
        {
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            AssignedToUserId = task.AssignedToUserId,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            SortOrder = task.SortOrder
        };

        // ---------- Runs / retry / test ----------

        public async Task<(IReadOnlyList<AutomationExecution> Items, int TotalCount)> GetRunsAsync(
            Guid automationId, Guid callerId, UserRole callerRole, int page, int pageSize)
        {
            var automation = await LoadAutomationAsync(automationId);
            await EnsureCanViewAutomationAsync(automation, callerId, callerRole);

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.AutomationExecutions.Include(e => e.Automation).Where(e => e.AutomationId == automationId);
            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<AutomationExecution> RetryAsync(Guid executionId, Guid callerId, UserRole callerRole)
        {
            var execution = await _db.AutomationExecutions
                .Include(e => e.Automation).ThenInclude(a => a!.Conditions)
                .Include(e => e.Automation).ThenInclude(a => a!.Actions)
                .FirstOrDefaultAsync(e => e.Id == executionId)
                ?? throw new NotFoundException($"Execution '{executionId}' was not found.");

            var automation = execution.Automation ?? throw new NotFoundException("The automation for this execution no longer exists.");
            await EnsureCanManageAutomationsAsync(automation.ProjectId, callerId, callerRole);

            if (execution.Status != AutomationExecutionStatus.Failed)
            {
                throw new ConflictException("Only failed executions can be retried.");
            }

            var retryCount = await _db.AutomationExecutions.CountAsync(e => e.RetryOfExecutionId == executionId);
            if (retryCount >= MaxRetries)
            {
                throw new ConflictException("This execution has already been retried the maximum number of times.");
            }

            var startedAt = DateTime.UtcNow;
            var rateLimits = new AutomationRateLimitTracker();

            var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == automation.CreatedByUserId);
            if (creator is null || !creator.IsActive || creator.IsDeleted)
            {
                return await RecordAndReturnAsync(
                    automation, execution, AutomationExecutionStatus.Failed,
                    "This automation's owner account is inactive or no longer exists.", null, startedAt);
            }

            var context = await BuildContextAsync(automation.TriggerType, execution.EntityId);
            if (context is null)
            {
                return await RecordAndReturnAsync(
                    automation, execution, AutomationExecutionStatus.Skipped,
                    "The original task/file/project no longer exists.", null, startedAt);
            }

            if (!EvaluateConditions(automation.Conditions, context))
            {
                return await RecordAndReturnAsync(
                    automation, execution, AutomationExecutionStatus.Skipped,
                    "Conditions no longer match the current state.", null, startedAt);
            }

            try
            {
                var resultParts = new List<string>();
                using (AutomationExecutionContext.EnterChain(1))
                {
                    foreach (var action in automation.Actions.OrderBy(a => a.SortOrder))
                    {
                        resultParts.Add(await ExecuteActionAsync(automation, action, context, creator, rateLimits));
                    }
                }

                automation.LastRunAt = DateTime.UtcNow;
                automation.RunCount++;
                automation.LastError = null;
                await _db.SaveChangesAsync();

                return await RecordAndReturnAsync(
                    automation, execution, AutomationExecutionStatus.Success, null, string.Join("; ", resultParts), startedAt);
            }
            catch (Exception ex)
            {
                automation.LastError = ex.Message;
                automation.LastRunAt = DateTime.UtcNow;
                automation.RunCount++;
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch
                {
                    // Best-effort.
                }

                _logger.LogError(ex, "Retry of automation execution {ExecutionId} failed.", executionId);
                return await RecordAndReturnAsync(automation, execution, AutomationExecutionStatus.Failed, ex.Message, null, startedAt);
            }
        }

        private async Task<AutomationExecution> RecordAndReturnAsync(
            Automation automation, AutomationExecution original, AutomationExecutionStatus status, string? error, string? summary, DateTime startedAt)
        {
            var retry = new AutomationExecution
            {
                Id = Guid.NewGuid(),
                AutomationId = automation.Id,
                TriggerEventId = original.TriggerEventId,
                ChainDepth = original.ChainDepth,
                EntityId = original.EntityId,
                Status = status,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                DurationMs = ElapsedMs(startedAt),
                ErrorMessage = error,
                ResultSummary = summary,
                RetryOfExecutionId = original.Id
            };
            _db.AutomationExecutions.Add(retry);
            await _db.SaveChangesAsync();
            retry.Automation = automation;
            return retry;
        }

        public async Task<AutomationTestResultDto> TestAsync(Guid automationId, Guid sampleEntityId, Guid callerId, UserRole callerRole)
        {
            var automation = await LoadAutomationAsync(automationId);
            await EnsureCanManageAutomationsAsync(automation.ProjectId, callerId, callerRole);

            var context = await BuildContextAsync(automation.TriggerType, sampleEntityId);
            if (context is null)
            {
                return new AutomationTestResultDto { ConditionsMatched = false, ActionPreviews = [], Explanation = "The sample task/file/project could not be found." };
            }

            var matched = EvaluateConditions(automation.Conditions, context);
            if (!matched)
            {
                return new AutomationTestResultDto
                {
                    ConditionsMatched = false,
                    ActionPreviews = [],
                    Explanation = "Conditions do not match this sample — no actions would run."
                };
            }

            var creator = await _db.Users.FindAsync(automation.CreatedByUserId);
            var previews = automation.Actions.OrderBy(a => a.SortOrder).Select(a => PreviewAction(a, context, creator)).ToList();

            return new AutomationTestResultDto
            {
                ConditionsMatched = true,
                ActionPreviews = previews,
                Explanation = "Conditions match — the actions below would run (no changes were made)."
            };
        }

        private string PreviewAction(AutomationAction action, AutomationEvaluationContext context, User? creator)
        {
            var variableContext = new AutomationVariableContext { Task = context.Task, ActingUser = creator };
            try
            {
                switch (action.ActionType)
                {
                    case AutomationActionType.AssignTask:
                    {
                        var p = Deserialize<AssignTaskParameters>(ParseJson(action.ParametersJson));
                        var userId = ResolveUserSelector(p.AssignMode, p.TargetUserId, context);
                        return $"Would assign task to {DescribeUser(userId)}";
                    }
                    case AutomationActionType.ChangeStatus:
                        return $"Would change status to {Deserialize<ChangeStatusParameters>(ParseJson(action.ParametersJson)).NewStatus}";
                    case AutomationActionType.ChangePriority:
                        return $"Would change priority to {Deserialize<ChangePriorityParameters>(ParseJson(action.ParametersJson)).NewPriority}";
                    case AutomationActionType.AddLabel:
                        return $"Would add label '{Deserialize<AddLabelParameters>(ParseJson(action.ParametersJson)).TagName}'";
                    case AutomationActionType.RemoveLabel:
                        return $"Would remove label '{Deserialize<RemoveLabelParameters>(ParseJson(action.ParametersJson)).TagName}'";
                    case AutomationActionType.SetDueDate:
                    {
                        var p = Deserialize<SetDueDateParameters>(ParseJson(action.ParametersJson));
                        return p.ClearDate ? "Would clear the due date" : $"Would set due date to {DateTime.UtcNow.AddDays(p.OffsetDays ?? 0):yyyy-MM-dd}";
                    }
                    case AutomationActionType.SetStartDate:
                    {
                        var p = Deserialize<SetStartDateParameters>(ParseJson(action.ParametersJson));
                        return p.ClearDate ? "Would clear the start date" : $"Would set start date to {DateTime.UtcNow.AddDays(p.OffsetDays ?? 0):yyyy-MM-dd}";
                    }
                    case AutomationActionType.AddComment:
                    {
                        var p = Deserialize<AddCommentParameters>(ParseJson(action.ParametersJson));
                        return $"Would post comment: \"{_variableResolver.Resolve(p.CommentTemplate, variableContext)}\"";
                    }
                    case AutomationActionType.CreateTask:
                    {
                        var p = Deserialize<CreateTaskParameters>(ParseJson(action.ParametersJson));
                        return $"Would create task '{_variableResolver.Resolve(p.TitleTemplate, variableContext)}'";
                    }
                    case AutomationActionType.SendNotification:
                    {
                        var p = Deserialize<SendNotificationParameters>(ParseJson(action.ParametersJson));
                        var recipientId = ResolveUserSelector(p.RecipientMode, p.TargetUserId, context);
                        return $"Would notify {DescribeUser(recipientId)}: \"{_variableResolver.Resolve(p.MessageTemplate, variableContext)}\"";
                    }
                    case AutomationActionType.AddFileTag:
                        return $"Would add file tag '{Deserialize<AddFileTagParameters>(ParseJson(action.ParametersJson)).TagName}'";
                    default:
                        return $"Unrecognized action '{action.ActionType}'";
                }
            }
            catch (Exception ex)
            {
                return $"Could not preview this action: {ex.Message}";
            }
        }

        private string DescribeUser(Guid? userId) =>
            userId is Guid id ? (_db.Users.FirstOrDefault(u => u.Id == id)?.Name ?? id.ToString()) : "(nobody — unresolved)";

        // ---------- Loaders ----------

        private async Task<Automation> LoadAutomationAsync(Guid automationId)
        {
            var automation = await _db.Automations
                .Include(a => a.CreatedBy)
                .Include(a => a.Conditions)
                .Include(a => a.Actions)
                .FirstOrDefaultAsync(a => a.Id == automationId && !a.IsDeleted);
            return automation ?? throw new NotFoundException($"Automation '{automationId}' was not found.");
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        /// <summary>Everything a condition/action might need for one evaluation+execution pass —
        /// loaded once per event (see BuildContextAsync), not re-queried per condition/action.
        /// Task is deliberately mutable: after an action changes the task (e.g. AssignTask),
        /// subsequent actions in the same run see the updated state rather than stale data.</summary>
        private class AutomationEvaluationContext
        {
            public TaskItem? Task { get; set; }
            public Attachment? File { get; set; }
            public Project? Project { get; set; }
            public required Guid ProjectId { get; set; }
            public required Guid ProjectOwnerId { get; set; }
            public Guid? TaskCreatorId { get; set; }
            public bool? AllSiblingSubtasksComplete { get; set; }
        }

        /// <summary>Per-event safety limits (spec section 55) — reset for every new event, shared
        /// across every automation that matches it (not per-automation), so ten different
        /// automations each creating one task still stops at the 10th, not 10-per-automation.</summary>
        private class AutomationRateLimitTracker
        {
            public int TasksCreated { get; set; }
            public int NotificationsSent { get; set; }
        }
    }
}

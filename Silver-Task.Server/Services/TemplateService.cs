using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Templates;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ITemplateService
    {
        /// <summary>Every template the caller can see (owned, explicitly shared, or Public),
        /// regardless of type — the Template Home list. Archived templates are only included when
        /// owned by the caller (or for an Administrator), matching spec #70's "remain available
        /// for history" for the owner specifically, not a blanket-visible archive.</summary>
        Task<List<TemplateSummaryDto>> ListForCallerAsync(Guid callerId, UserRole callerRole);

        Task<ProjectTemplateDto> GetProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<ProjectTemplateDto> SaveProjectTemplateAsync(Guid? id, SaveProjectTemplateRequest request, Guid callerId, UserRole callerRole);

        Task DeleteProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<ProjectTemplateDto> SetProjectTemplateArchivedAsync(Guid id, bool archived, Guid callerId, UserRole callerRole);

        Task<ProjectTemplateDto> DuplicateProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole);

        Task ShareProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole, string email);

        Task UnshareProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId);

        Task FavoriteProjectTemplateAsync(Guid id, Guid callerId, bool favorite);

        /// <summary>JSON export (spec's own stated preference over CSV, for preserving hierarchy/
        /// dependency information) — the DTO this serializes never contains passwords, tokens, or
        /// any credential (it's the same read DTO the UI already renders), satisfying the spec's
        /// export-security requirement structurally rather than via a separate redaction pass.</summary>
        Task<string> ExportProjectTemplateJsonAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<TaskTemplateDto> GetTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<TaskTemplateDto> SaveTaskTemplateAsync(Guid? id, SaveTaskTemplateRequest request, Guid callerId, UserRole callerRole);

        Task DeleteTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<TaskTemplateDto> SetTaskTemplateArchivedAsync(Guid id, bool archived, Guid callerId, UserRole callerRole);

        Task<TaskTemplateDto> DuplicateTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole);

        Task ShareTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole, string email);

        Task UnshareTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId);

        Task FavoriteTaskTemplateAsync(Guid id, Guid callerId, bool favorite);
    }

    /// <summary>
    /// Phase 40 — CRUD/validation/sharing/favoriting/duplication for both template types.
    /// Deliberately does NOT create projects/tasks — that's ITemplateInstantiationService's job
    /// (spec's own explicit service-boundary suggestion). "Edit"/"Delete"/"Share" are owner-tier
    /// (or Administrator) — Permissions.TemplatesEdit/Delete/Share are NOT blanket-granted to
    /// Manager/Member in PermissionService.SystemMatrix; only View/Create/Use are, mirroring
    /// SavedReportService's own EnsureCanModify precedent (Phase 38) rather than inventing a new
    /// authorization shape.
    ///
    /// Save (create/update) is a full-resource replace of the whole task/dependency graph, same
    /// "PUT replaces everything" convention UpdateTaskRequest already established — simpler and
    /// safer to reason about than a diff/patch API for a builder-style UI that submits the whole
    /// form at once. Circular-dependency validation runs over the REQUEST's own client-correlation
    /// ids before anything is written, using a standard directed-graph cycle detector (conceptually
    /// the same reachability idea as TaskDependencyService.WouldCreateCycleAsync, generalized to
    /// check an entire graph of possibly-many-new-edges at once rather than one candidate edge).
    /// </summary>
    public class TemplateService(AppDbContext db, ITagService tagService) : ITemplateService
    {
        private readonly AppDbContext _db = db;
        private readonly ITagService _tagService = tagService;

        // Mirrors TaskService's own MaxNestingDepth exactly (Phase 1-39 established limit) — see
        // spec #27's "respect the existing maximum nesting level."
        private const int MaxNestingDepth = 10;

        public async Task<List<TemplateSummaryDto>> ListForCallerAsync(Guid callerId, UserRole callerRole)
        {
            var isAdmin = callerRole == UserRole.Administrator;

            var projectTemplates = await _db.ProjectTemplates
                .Include(t => t.CreatedBy)
                .Include(t => t.Tasks)
                .Include(t => t.Shares)
                .Include(t => t.FavoritedBy)
                .Where(t => isAdmin || t.CreatedByUserId == callerId || t.IsPublic || t.Shares.Any(s => s.SharedWithUserId == callerId))
                .Where(t => !t.IsArchived || t.CreatedByUserId == callerId || isAdmin)
                .ToListAsync();

            var taskTemplates = await _db.TaskTemplates
                .Include(t => t.CreatedBy)
                .Include(t => t.Shares)
                .Include(t => t.FavoritedBy)
                .Where(t => isAdmin || t.CreatedByUserId == callerId || t.IsPublic || t.Shares.Any(s => s.SharedWithUserId == callerId))
                .Where(t => !t.IsArchived || t.CreatedByUserId == callerId || isAdmin)
                .ToListAsync();

            var result = new List<TemplateSummaryDto>();
            result.AddRange(projectTemplates.Select(t => new TemplateSummaryDto
            {
                Id = t.Id,
                Type = TemplateTypes.Project,
                Name = t.Name,
                Description = t.Description,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByName = t.CreatedBy?.Name ?? "Unknown",
                IsArchived = t.IsArchived,
                TaskCount = t.Tasks.Count,
                UsageCount = t.UsageCount,
                LastUsedAt = t.LastUsedAt,
                IsOwnedByMe = t.CreatedByUserId == callerId,
                IsFavorite = t.FavoritedBy.Any(f => f.UserId == callerId),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }));
            result.AddRange(taskTemplates.Select(t => new TemplateSummaryDto
            {
                Id = t.Id,
                Type = TemplateTypes.Task,
                Name = t.Name,
                Description = t.Description,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByName = t.CreatedBy?.Name ?? "Unknown",
                IsArchived = t.IsArchived,
                TaskCount = 1,
                UsageCount = t.UsageCount,
                LastUsedAt = t.LastUsedAt,
                IsOwnedByMe = t.CreatedByUserId == callerId,
                IsFavorite = t.FavoritedBy.Any(f => f.UserId == callerId),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }));

            return result.OrderByDescending(t => t.UpdatedAt).ToList();
        }

        // ---------- Project Templates ----------

        public async Task<ProjectTemplateDto> GetProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var template = await LoadProjectTemplateFullAsync(id);
            await EnsureCanViewProjectTemplateAsync(template, callerId, callerRole);
            return ToDto(template, callerId);
        }

        public async Task<ProjectTemplateDto> SaveProjectTemplateAsync(Guid? id, SaveProjectTemplateRequest request, Guid callerId, UserRole callerRole)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("Template name is required.");
            }
            await ValidateProjectTemplateGraphAsync(request);

            ProjectTemplate template;
            if (id is Guid existingId)
            {
                template = await _db.ProjectTemplates
                    .Include(t => t.Tasks)
                    .Include(t => t.Dependencies)
                    .FirstOrDefaultAsync(t => t.Id == existingId) ?? throw new NotFoundException("Template not found.");
                EnsureCanModify(template.CreatedByUserId, callerId, callerRole);

                // Full-resource replace — cascades remove the old Tags/CustomValues/ChecklistItems/
                // Dependencies automatically once their parent ProjectTemplateTask rows are removed.
                _db.ProjectTemplateTasks.RemoveRange(template.Tasks);
                template.Name = request.Name.Trim();
                template.Description = request.Description;
                template.IsPublic = request.IsPublic;
                template.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                template = new ProjectTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    IsPublic = request.IsPublic,
                    CreatedByUserId = callerId
                };
                _db.ProjectTemplates.Add(template);
            }

            var clientIdToTask = new Dictionary<Guid, ProjectTemplateTask>();
            foreach (var taskRequest in request.Tasks)
            {
                var task = new ProjectTemplateTask
                {
                    Id = Guid.NewGuid(),
                    ProjectTemplateId = template.Id,
                    Title = taskRequest.Title.Trim(),
                    Description = taskRequest.Description,
                    Status = taskRequest.Status,
                    Priority = taskRequest.Priority,
                    StartOffsetDays = taskRequest.StartOffsetDays,
                    DueOffsetDays = taskRequest.DueOffsetDays,
                    EstimatedDurationDays = taskRequest.EstimatedDurationDays,
                    AssignmentMode = taskRequest.AssignmentMode,
                    AssignedToUserId = taskRequest.AssignmentMode == TemplateAssignmentModes.SpecificUser ? taskRequest.AssignedToUserId : null,
                    SortOrder = taskRequest.SortOrder
                };
                clientIdToTask[taskRequest.ClientId] = task;
                _db.ProjectTemplateTasks.Add(task);
            }
            foreach (var taskRequest in request.Tasks)
            {
                if (taskRequest.ParentClientId is Guid parentClientId)
                {
                    clientIdToTask[taskRequest.ClientId].ParentTemplateTaskId = clientIdToTask[parentClientId].Id;
                }
            }
            foreach (var taskRequest in request.Tasks)
            {
                var task = clientIdToTask[taskRequest.ClientId];
                foreach (var tagName in taskRequest.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var tag = await _tagService.GetOrCreateAsync(tagName, callerId);
                    _db.ProjectTemplateTaskTags.Add(new ProjectTemplateTaskTag { Id = Guid.NewGuid(), ProjectTemplateTaskId = task.Id, TagId = tag.Id });
                }
                foreach (var customValue in taskRequest.CustomValues)
                {
                    _db.ProjectTemplateTaskCustomValues.Add(new ProjectTemplateTaskCustomValue
                    {
                        Id = Guid.NewGuid(), ProjectTemplateTaskId = task.Id, CustomFieldId = customValue.CustomFieldId, Value = customValue.Value
                    });
                }
                for (var i = 0; i < taskRequest.ChecklistItems.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(taskRequest.ChecklistItems[i]))
                    {
                        continue;
                    }
                    _db.ProjectTemplateTaskChecklistItems.Add(new ProjectTemplateTaskChecklistItem
                    {
                        Id = Guid.NewGuid(), ProjectTemplateTaskId = task.Id, Text = taskRequest.ChecklistItems[i].Trim(), SortOrder = i
                    });
                }
            }
            foreach (var depRequest in request.Dependencies)
            {
                var fromTask = clientIdToTask[depRequest.TemplateTaskClientId];
                var toTask = clientIdToTask[depRequest.DependsOnTemplateTaskClientId];
                _db.ProjectTemplateTaskDependencies.Add(new ProjectTemplateTaskDependency
                {
                    Id = Guid.NewGuid(),
                    ProjectTemplateId = template.Id,
                    TemplateTaskId = fromTask.Id,
                    DependsOnTemplateTaskId = toTask.Id,
                    DependencyType = depRequest.DependencyType
                });
            }

            await _db.SaveChangesAsync();
            return await GetProjectTemplateAsync(template.Id, callerId, callerRole);
        }

        public async Task DeleteProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var template = await _db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            // Projects.SourceProjectTemplateId is SetNull on delete (see ProjectConfiguration) —
            // projects already created from this template are never affected (spec #69).
            _db.ProjectTemplates.Remove(template);
            await _db.SaveChangesAsync();
        }

        public async Task<ProjectTemplateDto> SetProjectTemplateArchivedAsync(Guid id, bool archived, Guid callerId, UserRole callerRole)
        {
            var template = await _db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            template.IsArchived = archived;
            template.ArchivedAt = archived ? DateTime.UtcNow : null;
            template.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return await GetProjectTemplateAsync(id, callerId, callerRole);
        }

        public async Task<ProjectTemplateDto> DuplicateProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var template = await LoadProjectTemplateFullAsync(id);
            await EnsureCanViewProjectTemplateAsync(template, callerId, callerRole);

            var copy = new ProjectTemplate
            {
                Id = Guid.NewGuid(), Name = $"{template.Name} (Copy)", Description = template.Description,
                IsPublic = false, CreatedByUserId = callerId
            };
            _db.ProjectTemplates.Add(copy);

            var idMap = template.Tasks.ToDictionary(t => t.Id, _ => Guid.NewGuid());
            foreach (var task in template.Tasks)
            {
                var newTask = new ProjectTemplateTask
                {
                    Id = idMap[task.Id],
                    ProjectTemplateId = copy.Id,
                    ParentTemplateTaskId = task.ParentTemplateTaskId is Guid p ? idMap[p] : null,
                    Title = task.Title,
                    Description = task.Description,
                    Status = task.Status,
                    Priority = task.Priority,
                    StartOffsetDays = task.StartOffsetDays,
                    DueOffsetDays = task.DueOffsetDays,
                    EstimatedDurationDays = task.EstimatedDurationDays,
                    AssignmentMode = task.AssignmentMode,
                    AssignedToUserId = task.AssignedToUserId,
                    SortOrder = task.SortOrder
                };
                _db.ProjectTemplateTasks.Add(newTask);
                foreach (var tag in task.Tags)
                {
                    _db.ProjectTemplateTaskTags.Add(new ProjectTemplateTaskTag { Id = Guid.NewGuid(), ProjectTemplateTaskId = newTask.Id, TagId = tag.TagId });
                }
                foreach (var cv in task.CustomValues)
                {
                    _db.ProjectTemplateTaskCustomValues.Add(new ProjectTemplateTaskCustomValue
                    { Id = Guid.NewGuid(), ProjectTemplateTaskId = newTask.Id, CustomFieldId = cv.CustomFieldId, Value = cv.Value });
                }
                foreach (var item in task.ChecklistItems)
                {
                    _db.ProjectTemplateTaskChecklistItems.Add(new ProjectTemplateTaskChecklistItem
                    { Id = Guid.NewGuid(), ProjectTemplateTaskId = newTask.Id, Text = item.Text, SortOrder = item.SortOrder });
                }
            }
            foreach (var dep in template.Dependencies)
            {
                _db.ProjectTemplateTaskDependencies.Add(new ProjectTemplateTaskDependency
                {
                    Id = Guid.NewGuid(), ProjectTemplateId = copy.Id,
                    TemplateTaskId = idMap[dep.TemplateTaskId], DependsOnTemplateTaskId = idMap[dep.DependsOnTemplateTaskId],
                    DependencyType = dep.DependencyType
                });
            }

            await _db.SaveChangesAsync();
            return await GetProjectTemplateAsync(copy.Id, callerId, callerRole);
        }

        public async Task ShareProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole, string email)
        {
            var template = await _db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            await CreateShareAsync(id, null, template.CreatedByUserId, email);
        }

        public async Task UnshareProjectTemplateAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId)
        {
            var template = await _db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            var share = await _db.TemplateShares.FirstOrDefaultAsync(s => s.ProjectTemplateId == id && s.SharedWithUserId == targetUserId);
            if (share != null)
            {
                _db.TemplateShares.Remove(share);
                await _db.SaveChangesAsync();
            }
        }

        public async Task FavoriteProjectTemplateAsync(Guid id, Guid callerId, bool favorite)
        {
            if (favorite)
            {
                var visible = await _db.ProjectTemplates.AnyAsync(t =>
                    t.Id == id && (t.CreatedByUserId == callerId || t.IsPublic || t.Shares.Any(s => s.SharedWithUserId == callerId)));
                if (!visible)
                {
                    throw new NotFoundException("Template not found.");
                }
                if (!await _db.UserTemplateFavorites.AnyAsync(f => f.ProjectTemplateId == id && f.UserId == callerId))
                {
                    _db.UserTemplateFavorites.Add(new UserTemplateFavorite { Id = Guid.NewGuid(), UserId = callerId, ProjectTemplateId = id });
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                var fav = await _db.UserTemplateFavorites.FirstOrDefaultAsync(f => f.ProjectTemplateId == id && f.UserId == callerId);
                if (fav != null)
                {
                    _db.UserTemplateFavorites.Remove(fav);
                    await _db.SaveChangesAsync();
                }
            }
        }

        public async Task<string> ExportProjectTemplateJsonAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var dto = await GetProjectTemplateAsync(id, callerId, callerRole);
            return System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
        }

        // ---------- Task Templates ----------

        public async Task<TaskTemplateDto> GetTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var template = await LoadTaskTemplateFullAsync(id);
            await EnsureCanViewTaskTemplateAsync(template, callerId, callerRole);
            return ToDto(template, callerId);
        }

        public async Task<TaskTemplateDto> SaveTaskTemplateAsync(Guid? id, SaveTaskTemplateRequest request, Guid callerId, UserRole callerRole)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("Template name is required.");
            }
            if (!TemplateAssignmentModes.All.Contains(request.AssignmentMode))
            {
                throw new ValidationException("Unrecognized assignment mode.");
            }
            if (request.AssignmentMode == TemplateAssignmentModes.SpecificUser)
            {
                if (request.AssignedToUserId is not Guid userId || !await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted))
                {
                    throw new ValidationException("A valid, active assignee is required for this assignment mode.");
                }
            }
            foreach (var customValue in request.CustomValues)
            {
                if (!await _db.CustomFields.AnyAsync(f => f.Id == customValue.CustomFieldId))
                {
                    throw new ValidationException("Unrecognized custom field.");
                }
            }

            TaskTemplate template;
            if (id is Guid existingId)
            {
                template = await _db.TaskTemplates
                    .Include(t => t.Tags)
                    .Include(t => t.CustomValues)
                    .Include(t => t.ChecklistItems)
                    .FirstOrDefaultAsync(t => t.Id == existingId) ?? throw new NotFoundException("Template not found.");
                EnsureCanModify(template.CreatedByUserId, callerId, callerRole);

                _db.TaskTemplateTags.RemoveRange(template.Tags);
                _db.TaskTemplateCustomValues.RemoveRange(template.CustomValues);
                _db.TaskTemplateChecklistItems.RemoveRange(template.ChecklistItems);

                template.Name = request.Name.Trim();
                template.Description = request.Description;
                template.Status = request.Status;
                template.Priority = request.Priority;
                template.StartOffsetDays = request.StartOffsetDays;
                template.DueOffsetDays = request.DueOffsetDays;
                template.EstimatedDurationDays = request.EstimatedDurationDays;
                template.AssignmentMode = request.AssignmentMode;
                template.AssignedToUserId = request.AssignmentMode == TemplateAssignmentModes.SpecificUser ? request.AssignedToUserId : null;
                template.IsPublic = request.IsPublic;
                template.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                template = new TaskTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    Status = request.Status,
                    Priority = request.Priority,
                    StartOffsetDays = request.StartOffsetDays,
                    DueOffsetDays = request.DueOffsetDays,
                    EstimatedDurationDays = request.EstimatedDurationDays,
                    AssignmentMode = request.AssignmentMode,
                    AssignedToUserId = request.AssignmentMode == TemplateAssignmentModes.SpecificUser ? request.AssignedToUserId : null,
                    IsPublic = request.IsPublic,
                    CreatedByUserId = callerId
                };
                _db.TaskTemplates.Add(template);
            }

            foreach (var tagName in request.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await _tagService.GetOrCreateAsync(tagName, callerId);
                _db.TaskTemplateTags.Add(new TaskTemplateTag { Id = Guid.NewGuid(), TaskTemplateId = template.Id, TagId = tag.Id });
            }
            foreach (var customValue in request.CustomValues)
            {
                _db.TaskTemplateCustomValues.Add(new TaskTemplateCustomValue
                { Id = Guid.NewGuid(), TaskTemplateId = template.Id, CustomFieldId = customValue.CustomFieldId, Value = customValue.Value });
            }
            for (var i = 0; i < request.ChecklistItems.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(request.ChecklistItems[i]))
                {
                    continue;
                }
                _db.TaskTemplateChecklistItems.Add(new TaskTemplateChecklistItem
                { Id = Guid.NewGuid(), TaskTemplateId = template.Id, Text = request.ChecklistItems[i].Trim(), SortOrder = i });
            }

            await _db.SaveChangesAsync();
            return await GetTaskTemplateAsync(template.Id, callerId, callerRole);
        }

        public async Task DeleteTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var template = await _db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            _db.TaskTemplates.Remove(template);
            await _db.SaveChangesAsync();
        }

        public async Task<TaskTemplateDto> SetTaskTemplateArchivedAsync(Guid id, bool archived, Guid callerId, UserRole callerRole)
        {
            var template = await _db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            template.IsArchived = archived;
            template.ArchivedAt = archived ? DateTime.UtcNow : null;
            template.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return await GetTaskTemplateAsync(id, callerId, callerRole);
        }

        public async Task<TaskTemplateDto> DuplicateTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var template = await LoadTaskTemplateFullAsync(id);
            await EnsureCanViewTaskTemplateAsync(template, callerId, callerRole);

            var copy = new TaskTemplate
            {
                Id = Guid.NewGuid(),
                Name = $"{template.Name} (Copy)",
                Description = template.Description,
                Status = template.Status,
                Priority = template.Priority,
                StartOffsetDays = template.StartOffsetDays,
                DueOffsetDays = template.DueOffsetDays,
                EstimatedDurationDays = template.EstimatedDurationDays,
                AssignmentMode = template.AssignmentMode,
                AssignedToUserId = template.AssignedToUserId,
                IsPublic = false,
                CreatedByUserId = callerId
            };
            _db.TaskTemplates.Add(copy);

            foreach (var tag in template.Tags)
            {
                _db.TaskTemplateTags.Add(new TaskTemplateTag { Id = Guid.NewGuid(), TaskTemplateId = copy.Id, TagId = tag.TagId });
            }
            foreach (var cv in template.CustomValues)
            {
                _db.TaskTemplateCustomValues.Add(new TaskTemplateCustomValue { Id = Guid.NewGuid(), TaskTemplateId = copy.Id, CustomFieldId = cv.CustomFieldId, Value = cv.Value });
            }
            foreach (var item in template.ChecklistItems)
            {
                _db.TaskTemplateChecklistItems.Add(new TaskTemplateChecklistItem { Id = Guid.NewGuid(), TaskTemplateId = copy.Id, Text = item.Text, SortOrder = item.SortOrder });
            }

            await _db.SaveChangesAsync();
            return await GetTaskTemplateAsync(copy.Id, callerId, callerRole);
        }

        public async Task ShareTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole, string email)
        {
            var template = await _db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            await CreateShareAsync(null, id, template.CreatedByUserId, email);
        }

        public async Task UnshareTaskTemplateAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId)
        {
            var template = await _db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Template not found.");
            EnsureCanModify(template.CreatedByUserId, callerId, callerRole);
            var share = await _db.TemplateShares.FirstOrDefaultAsync(s => s.TaskTemplateId == id && s.SharedWithUserId == targetUserId);
            if (share != null)
            {
                _db.TemplateShares.Remove(share);
                await _db.SaveChangesAsync();
            }
        }

        public async Task FavoriteTaskTemplateAsync(Guid id, Guid callerId, bool favorite)
        {
            if (favorite)
            {
                var visible = await _db.TaskTemplates.AnyAsync(t =>
                    t.Id == id && (t.CreatedByUserId == callerId || t.IsPublic || t.Shares.Any(s => s.SharedWithUserId == callerId)));
                if (!visible)
                {
                    throw new NotFoundException("Template not found.");
                }
                if (!await _db.UserTemplateFavorites.AnyAsync(f => f.TaskTemplateId == id && f.UserId == callerId))
                {
                    _db.UserTemplateFavorites.Add(new UserTemplateFavorite { Id = Guid.NewGuid(), UserId = callerId, TaskTemplateId = id });
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                var fav = await _db.UserTemplateFavorites.FirstOrDefaultAsync(f => f.TaskTemplateId == id && f.UserId == callerId);
                if (fav != null)
                {
                    _db.UserTemplateFavorites.Remove(fav);
                    await _db.SaveChangesAsync();
                }
            }
        }

        // ---------- Shared helpers ----------

        private async Task CreateShareAsync(Guid? projectTemplateId, Guid? taskTemplateId, Guid ownerId, string email)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var target = await _db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail)
                ?? throw new NotFoundException($"No user found with email '{email}'.");
            if (target.Id == ownerId)
            {
                throw new ValidationException("Cannot share a template with its own owner.");
            }

            var alreadyShared = await _db.TemplateShares.AnyAsync(s =>
                s.ProjectTemplateId == projectTemplateId && s.TaskTemplateId == taskTemplateId && s.SharedWithUserId == target.Id);
            if (alreadyShared)
            {
                return;
            }

            _db.TemplateShares.Add(new TemplateShare
            { Id = Guid.NewGuid(), ProjectTemplateId = projectTemplateId, TaskTemplateId = taskTemplateId, SharedWithUserId = target.Id });
            await _db.SaveChangesAsync();
        }

        private static void EnsureCanModify(Guid createdByUserId, Guid callerId, UserRole callerRole)
        {
            if (createdByUserId != callerId && callerRole != UserRole.Administrator)
            {
                throw new ForbiddenException("You do not have permission to modify this template.");
            }
        }

        private async Task<ProjectTemplate> LoadProjectTemplateFullAsync(Guid id)
        {
            var template = await _db.ProjectTemplates
                .Include(t => t.CreatedBy)
                .Include(t => t.Shares).ThenInclude(s => s.SharedWithUser)
                .Include(t => t.FavoritedBy)
                .Include(t => t.Tasks).ThenInclude(tt => tt.AssignedTo)
                .Include(t => t.Tasks).ThenInclude(tt => tt.Tags).ThenInclude(tg => tg.Tag)
                .Include(t => t.Tasks).ThenInclude(tt => tt.CustomValues)
                .Include(t => t.Tasks).ThenInclude(tt => tt.ChecklistItems)
                .Include(t => t.Dependencies)
                .FirstOrDefaultAsync(t => t.Id == id);
            return template ?? throw new NotFoundException($"Template '{id}' was not found.");
        }

        private async Task<TaskTemplate> LoadTaskTemplateFullAsync(Guid id)
        {
            var template = await _db.TaskTemplates
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Include(t => t.Shares).ThenInclude(s => s.SharedWithUser)
                .Include(t => t.FavoritedBy)
                .Include(t => t.Tags).ThenInclude(tg => tg.Tag)
                .Include(t => t.CustomValues)
                .Include(t => t.ChecklistItems)
                .FirstOrDefaultAsync(t => t.Id == id);
            return template ?? throw new NotFoundException($"Template '{id}' was not found.");
        }

        private async Task EnsureCanViewProjectTemplateAsync(ProjectTemplate template, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || template.CreatedByUserId == callerId || template.IsPublic)
            {
                return;
            }
            var shared = template.Shares.Count > 0
                ? template.Shares.Any(s => s.SharedWithUserId == callerId)
                : await _db.TemplateShares.AnyAsync(s => s.ProjectTemplateId == template.Id && s.SharedWithUserId == callerId);
            if (!shared)
            {
                throw new ForbiddenException("You do not have access to this template.");
            }
        }

        private async Task EnsureCanViewTaskTemplateAsync(TaskTemplate template, Guid callerId, UserRole callerRole)
        {
            if (callerRole == UserRole.Administrator || template.CreatedByUserId == callerId || template.IsPublic)
            {
                return;
            }
            var shared = template.Shares.Count > 0
                ? template.Shares.Any(s => s.SharedWithUserId == callerId)
                : await _db.TemplateShares.AnyAsync(s => s.TaskTemplateId == template.Id && s.SharedWithUserId == callerId);
            if (!shared)
            {
                throw new ForbiddenException("You do not have access to this template.");
            }
        }

        /// <summary>Validates a whole SaveProjectTemplateRequest before anything is written:
        /// required names, valid assignment references (spec #94 — a SpecificUser assignment must
        /// resolve to a real, active user even at save time), valid parent/dependency references,
        /// nesting depth, and — critically — no circular dependency anywhere in the submitted
        /// graph (spec #21/#22).</summary>
        private async Task ValidateProjectTemplateGraphAsync(SaveProjectTemplateRequest request)
        {
            if (request.Tasks.Count == 0)
            {
                throw new ValidationException("A project template must have at least one task.");
            }

            var clientIds = new HashSet<Guid>();
            foreach (var task in request.Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.Title))
                {
                    throw new ValidationException("Every task must have a name.");
                }
                if (!clientIds.Add(task.ClientId))
                {
                    throw new ValidationException("Duplicate task reference in request.");
                }
                if (!TemplateAssignmentModes.All.Contains(task.AssignmentMode))
                {
                    throw new ValidationException($"\"{task.Title}\" has an unrecognized assignment mode.");
                }
                if (task.AssignmentMode == TemplateAssignmentModes.SpecificUser)
                {
                    if (task.AssignedToUserId is not Guid userId || !await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted))
                    {
                        throw new ValidationException($"\"{task.Title}\" requires a valid, active assignee.");
                    }
                }
                foreach (var customValue in task.CustomValues)
                {
                    if (!await _db.CustomFields.AnyAsync(f => f.Id == customValue.CustomFieldId))
                    {
                        throw new ValidationException($"\"{task.Title}\" references an unrecognized custom field.");
                    }
                }
            }

            var parentByClientId = new Dictionary<Guid, Guid>();
            foreach (var task in request.Tasks.Where(t => t.ParentClientId is not null))
            {
                if (!clientIds.Contains(task.ParentClientId!.Value))
                {
                    throw new ValidationException($"\"{task.Title}\" references an unknown parent task.");
                }
                parentByClientId[task.ClientId] = task.ParentClientId.Value;
            }
            foreach (var task in request.Tasks)
            {
                var depth = 0;
                var current = task.ClientId;
                var visitedChain = new HashSet<Guid> { current };
                while (parentByClientId.TryGetValue(current, out var parent))
                {
                    if (!visitedChain.Add(parent))
                    {
                        throw new ValidationException("Circular subtask hierarchy detected.");
                    }
                    depth++;
                    if (depth > MaxNestingDepth)
                    {
                        throw new ValidationException($"Subtask nesting cannot exceed {MaxNestingDepth} levels.");
                    }
                    current = parent;
                }
            }

            var seenEdges = new HashSet<(Guid, Guid, string)>();
            foreach (var dep in request.Dependencies)
            {
                if (dep.TemplateTaskClientId == dep.DependsOnTemplateTaskClientId)
                {
                    throw new ValidationException("A task cannot depend on itself.");
                }
                if (!clientIds.Contains(dep.TemplateTaskClientId) || !clientIds.Contains(dep.DependsOnTemplateTaskClientId))
                {
                    throw new ValidationException("A dependency references an unknown task.");
                }
                if (!DependencyTypes.All.Contains(dep.DependencyType))
                {
                    throw new ValidationException("Unrecognized dependency type.");
                }
                if (!seenEdges.Add((dep.TemplateTaskClientId, dep.DependsOnTemplateTaskClientId, dep.DependencyType)))
                {
                    throw new ValidationException("This dependency has already been added.");
                }
            }

            EnsureNoCycles(clientIds, request.Dependencies
                .GroupBy(d => d.TemplateTaskClientId)
                .ToDictionary(g => g.Key, g => g.Select(d => d.DependsOnTemplateTaskClientId).ToList()));
        }

        /// <summary>Standard directed-graph cycle detector (DFS with a recursion-stack/"gray set")
        /// — checks the ENTIRE submitted graph at once, since a single template save can introduce
        /// many edges simultaneously (unlike TaskDependencyService.WouldCreateCycleAsync, which
        /// only ever needs to check one candidate edge against an already-persisted graph).</summary>
        private static void EnsureNoCycles(HashSet<Guid> nodes, Dictionary<Guid, List<Guid>> adjacency)
        {
            var visited = new HashSet<Guid>();
            var inStack = new HashSet<Guid>();

            bool HasCycle(Guid node)
            {
                if (inStack.Contains(node))
                {
                    return true;
                }
                if (!visited.Add(node))
                {
                    return false;
                }
                inStack.Add(node);
                if (adjacency.TryGetValue(node, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        if (HasCycle(neighbor))
                        {
                            return true;
                        }
                    }
                }
                inStack.Remove(node);
                return false;
            }

            foreach (var node in nodes)
            {
                if (HasCycle(node))
                {
                    throw new ValidationException("This dependency graph would create a circular workflow.");
                }
            }
        }

        private static ProjectTemplateDto ToDto(ProjectTemplate template, Guid callerId)
        {
            var isOwned = template.CreatedByUserId == callerId;
            return new ProjectTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                CreatedByUserId = template.CreatedByUserId,
                CreatedByName = template.CreatedBy?.Name ?? "Unknown",
                IsArchived = template.IsArchived,
                IsPublic = template.IsPublic,
                UsageCount = template.UsageCount,
                LastUsedAt = template.LastUsedAt,
                IsOwnedByMe = isOwned,
                IsFavorite = template.FavoritedBy.Any(f => f.UserId == callerId),
                SharedWith = isOwned
                    ? template.Shares.Select(s => new TemplateSharedUserDto { UserId = s.SharedWithUserId, Name = s.SharedWithUser?.Name ?? "Unknown" }).ToList()
                    : null,
                Tasks = template.Tasks.OrderBy(t => t.SortOrder).Select(t => new ProjectTemplateTaskDto
                {
                    Id = t.Id,
                    ParentTemplateTaskId = t.ParentTemplateTaskId,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    Priority = t.Priority,
                    StartOffsetDays = t.StartOffsetDays,
                    DueOffsetDays = t.DueOffsetDays,
                    EstimatedDurationDays = t.EstimatedDurationDays,
                    AssignmentMode = t.AssignmentMode,
                    AssignedToUserId = t.AssignedToUserId,
                    AssignedToName = t.AssignedTo?.Name,
                    SortOrder = t.SortOrder,
                    Tags = t.Tags.Select(tg => tg.Tag?.Name ?? "").Where(n => n.Length > 0).ToList(),
                    CustomValues = t.CustomValues.Select(cv => new TemplateCustomValueDto { CustomFieldId = cv.CustomFieldId, Value = cv.Value }).ToList(),
                    ChecklistItems = t.ChecklistItems.OrderBy(c => c.SortOrder)
                        .Select(c => new TemplateChecklistItemDto { Id = c.Id, Text = c.Text, SortOrder = c.SortOrder }).ToList()
                }).ToList(),
                Dependencies = template.Dependencies.Select(d => new ProjectTemplateDependencyDto
                {
                    Id = d.Id, TemplateTaskId = d.TemplateTaskId, DependsOnTemplateTaskId = d.DependsOnTemplateTaskId, DependencyType = d.DependencyType
                }).ToList(),
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }

        private static TaskTemplateDto ToDto(TaskTemplate template, Guid callerId)
        {
            var isOwned = template.CreatedByUserId == callerId;
            return new TaskTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                Status = template.Status,
                Priority = template.Priority,
                StartOffsetDays = template.StartOffsetDays,
                DueOffsetDays = template.DueOffsetDays,
                EstimatedDurationDays = template.EstimatedDurationDays,
                AssignmentMode = template.AssignmentMode,
                AssignedToUserId = template.AssignedToUserId,
                AssignedToName = template.AssignedTo?.Name,
                CreatedByUserId = template.CreatedByUserId,
                CreatedByName = template.CreatedBy?.Name ?? "Unknown",
                IsArchived = template.IsArchived,
                IsPublic = template.IsPublic,
                UsageCount = template.UsageCount,
                LastUsedAt = template.LastUsedAt,
                IsOwnedByMe = isOwned,
                IsFavorite = template.FavoritedBy.Any(f => f.UserId == callerId),
                SharedWith = isOwned
                    ? template.Shares.Select(s => new TemplateSharedUserDto { UserId = s.SharedWithUserId, Name = s.SharedWithUser?.Name ?? "Unknown" }).ToList()
                    : null,
                Tags = template.Tags.Select(tg => tg.Tag?.Name ?? "").Where(n => n.Length > 0).ToList(),
                CustomValues = template.CustomValues.Select(cv => new TemplateCustomValueDto { CustomFieldId = cv.CustomFieldId, Value = cv.Value }).ToList(),
                ChecklistItems = template.ChecklistItems.OrderBy(c => c.SortOrder)
                    .Select(c => new TemplateChecklistItemDto { Id = c.Id, Text = c.Text, SortOrder = c.SortOrder }).ToList(),
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }
    }
}

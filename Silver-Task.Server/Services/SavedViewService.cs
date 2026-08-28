using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.DTOs.SavedViews;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ISavedViewService
    {
        /// <summary>Own views, views explicitly shared with the caller, every public view (same
        /// two-tier model as ProjectTemplate/TaskTemplate — see SavedView's own doc comment), plus
        /// the six synthesized system-default views — in that display order.</summary>
        Task<List<SavedViewDto>> ListForCallerAsync(Guid callerId, UserRole callerRole);

        Task<SavedViewDto> GetByIdAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<SavedViewDto> CreateAsync(Guid callerId, UserRole callerRole, SaveViewRequest request);

        Task<SavedViewDto> UpdateAsync(Guid id, Guid callerId, UserRole callerRole, SaveViewRequest request);

        Task DeleteAsync(Guid id, Guid callerId, UserRole callerRole);

        /// <summary>Also the "clone a shared/public/system-default view into a private one" path
        /// (spec's own explicit requirement) — the copy is always private and owned by the caller
        /// regardless of the source view's own visibility/ownership.</summary>
        Task<SavedViewDto> DuplicateAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<bool> ShareAsync(Guid id, Guid callerId, UserRole callerRole, string email);

        Task UnshareAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId);

        Task FavoriteAsync(Guid id, Guid callerId, UserRole callerRole);

        Task UnfavoriteAsync(Guid id, Guid callerId);

        Task ReorderFavoritesAsync(Guid callerId, List<Guid> orderedViewIds);

        /// <summary>The one execution entry point every rendering surface (Table page, single-
        /// project layout resolution, export) goes through. Re-verifies the CURRENT caller's live
        /// project access on every call, regardless of who created/shared the view or what access
        /// existed at save/share time — a saved view can never grant access beyond the executor's
        /// own existing task/project permissions (spec's own non-negotiable rule).</summary>
        Task<ExecuteViewResultDto> ExecuteAsync(Guid id, Guid callerId, UserRole callerRole, int page, int pageSize);

        Task<PreviewResultDto> PreviewAsync(PreviewViewRequest request, Guid callerId, UserRole callerRole);
    }

    public class SavedViewService(AppDbContext db, IProjectAccessService projectAccess, ISavedViewFilterEngine filterEngine, ITaskService taskService) : ISavedViewService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISavedViewFilterEngine _filterEngine = filterEngine;
        private readonly ITaskService _taskService = taskService;

        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 200;

        public async Task<List<SavedViewDto>> ListForCallerAsync(Guid callerId, UserRole callerRole)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var views = await _db.SavedViews
                .Include(v => v.CreatedBy)
                .Include(v => v.Shares).ThenInclude(s => s.SharedWithUser)
                .Where(v => isAdmin || v.CreatedByUserId == callerId || v.IsPublic || v.Shares.Any(s => s.SharedWithUserId == callerId))
                .OrderByDescending(v => v.UpdatedAt)
                .ToListAsync();

            var favorites = await _db.UserSavedViewFavorites.Where(f => f.UserId == callerId).ToListAsync();
            var favoriteByViewId = favorites.ToDictionary(f => f.SavedViewId, f => f.SortOrder);

            var result = views.Select(v => ToDto(v, callerId, favoriteByViewId)).ToList();

            foreach (var def in SavedViewSystemDefaults.All)
            {
                result.Add(ToSystemDefaultDto(def, favoriteByViewId));
            }

            return result;
        }

        public async Task<SavedViewDto> GetByIdAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            if (SavedViewSystemDefaults.Find(id) is { } def)
            {
                var favorites = await _db.UserSavedViewFavorites.Where(f => f.UserId == callerId && f.SavedViewId == id).ToListAsync();
                return ToSystemDefaultDto(def, favorites.ToDictionary(f => f.SavedViewId, f => f.SortOrder));
            }

            var view = await LoadViewAsync(id);
            await EnsureCanViewAsync(view, callerId, callerRole);

            var favorite = await _db.UserSavedViewFavorites.FirstOrDefaultAsync(f => f.UserId == callerId && f.SavedViewId == id);
            return ToDto(view, callerId, favorite is null ? [] : new Dictionary<Guid, int> { [id] = favorite.SortOrder });
        }

        public async Task<SavedViewDto> CreateAsync(Guid callerId, UserRole callerRole, SaveViewRequest request)
        {
            ValidateRequest(request);
            await ValidateFilterFieldsAsync(request.EntityType, request.Filter);

            var view = new SavedView
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description,
                CreatedByUserId = callerId,
                EntityType = request.EntityType,
                IsPublic = request.IsPublic,
                FilterJson = SerializeFilter(request.Filter),
                Columns = request.Columns is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Columns) : null,
                SortField = request.SortField,
                SortDescending = request.SortDescending,
                GroupByField = request.GroupByField,
                Layout = request.Layout,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.SavedViews.Add(view);
            await _db.SaveChangesAsync();

            return await LoadDtoAsync(view.Id, callerId);
        }

        public async Task<SavedViewDto> UpdateAsync(Guid id, Guid callerId, UserRole callerRole, SaveViewRequest request)
        {
            if (SavedViewSystemDefaults.Find(id) is not null)
            {
                throw new ValidationException("System default views cannot be edited.");
            }

            var view = await LoadViewAsync(id);
            EnsureCanModify(view, callerId, callerRole);

            ValidateRequest(request);
            await ValidateFilterFieldsAsync(request.EntityType, request.Filter);

            if (request.EntityType != view.EntityType)
            {
                throw new ValidationException("A saved view's entity type cannot be changed after creation.");
            }

            view.Name = request.Name.Trim();
            view.Description = request.Description;
            view.IsPublic = request.IsPublic;
            view.FilterJson = SerializeFilter(request.Filter);
            view.Columns = request.Columns is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Columns) : null;
            view.SortField = request.SortField;
            view.SortDescending = request.SortDescending;
            view.GroupByField = request.GroupByField;
            view.Layout = request.Layout;
            view.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return await LoadDtoAsync(view.Id, callerId);
        }

        public async Task DeleteAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            if (SavedViewSystemDefaults.Find(id) is not null)
            {
                throw new ValidationException("System default views cannot be deleted.");
            }

            var view = await LoadViewAsync(id);
            EnsureCanModify(view, callerId, callerRole);

            var favorites = await _db.UserSavedViewFavorites.Where(f => f.SavedViewId == id).ToListAsync();
            _db.UserSavedViewFavorites.RemoveRange(favorites);

            _db.SavedViews.Remove(view);
            await _db.SaveChangesAsync();
        }

        public async Task<SavedViewDto> DuplicateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            string name;
            string entityType;
            SavedViewFilterGroupDto filter;
            string? columns;
            string? sortField;
            bool sortDescending;
            string? groupByField;
            string layout;

            if (SavedViewSystemDefaults.Find(id) is { } def)
            {
                name = def.Name;
                entityType = SavedViewEntityTypes.Task;
                filter = def.Filter;
                columns = null;
                sortField = def.SortField;
                sortDescending = def.SortDescending;
                groupByField = null;
                layout = SavedViewLayouts.Table;
            }
            else
            {
                var view = await LoadViewAsync(id);
                await EnsureCanViewAsync(view, callerId, callerRole);
                name = view.Name;
                entityType = view.EntityType;
                filter = DeserializeFilter(view.FilterJson);
                columns = view.Columns;
                sortField = view.SortField;
                sortDescending = view.SortDescending;
                groupByField = view.GroupByField;
                layout = view.Layout;
            }

            var copy = new SavedView
            {
                Id = Guid.NewGuid(),
                Name = $"{name} (Copy)",
                CreatedByUserId = callerId,
                EntityType = entityType,
                IsPublic = false,
                FilterJson = SerializeFilter(filter),
                Columns = columns,
                SortField = sortField,
                SortDescending = sortDescending,
                GroupByField = groupByField,
                Layout = layout,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.SavedViews.Add(copy);
            await _db.SaveChangesAsync();

            return await LoadDtoAsync(copy.Id, callerId);
        }

        public async Task<bool> ShareAsync(Guid id, Guid callerId, UserRole callerRole, string email)
        {
            if (SavedViewSystemDefaults.Find(id) is not null)
            {
                throw new ValidationException("System default views cannot be shared.");
            }

            var view = await LoadViewAsync(id);
            EnsureCanModify(view, callerId, callerRole);

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var target = await _db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (target is null)
            {
                return false;
            }
            if (target.Id == view.CreatedByUserId)
            {
                throw new ValidationException("Cannot share a view with its own owner.");
            }

            var alreadyShared = await _db.SavedViewShares.AnyAsync(s => s.SavedViewId == id && s.SharedWithUserId == target.Id);
            if (alreadyShared)
            {
                return true;
            }

            _db.SavedViewShares.Add(new SavedViewShare
            {
                Id = Guid.NewGuid(),
                SavedViewId = id,
                SharedWithUserId = target.Id,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task UnshareAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId)
        {
            var view = await LoadViewAsync(id);
            EnsureCanModify(view, callerId, callerRole);

            var share = await _db.SavedViewShares.FirstOrDefaultAsync(s => s.SavedViewId == id && s.SharedWithUserId == targetUserId);
            if (share != null)
            {
                _db.SavedViewShares.Remove(share);
                await _db.SaveChangesAsync();
            }
        }

        public async Task FavoriteAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            if (SavedViewSystemDefaults.Find(id) is null)
            {
                var exists = await _db.SavedViews.AnyAsync(v => v.Id == id &&
                    (v.CreatedByUserId == callerId || v.IsPublic || v.Shares.Any(s => s.SharedWithUserId == callerId)));
                if (!exists && callerRole != UserRole.Administrator)
                {
                    throw new NotFoundException("View not found.");
                }
            }

            var alreadyFavorited = await _db.UserSavedViewFavorites.AnyAsync(f => f.SavedViewId == id && f.UserId == callerId);
            if (alreadyFavorited)
            {
                return;
            }

            var maxOrder = await _db.UserSavedViewFavorites.Where(f => f.UserId == callerId).Select(f => (int?)f.SortOrder).MaxAsync() ?? -1;

            _db.UserSavedViewFavorites.Add(new UserSavedViewFavorite
            {
                Id = Guid.NewGuid(),
                UserId = callerId,
                SavedViewId = id,
                SortOrder = maxOrder + 1,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task UnfavoriteAsync(Guid id, Guid callerId)
        {
            var favorite = await _db.UserSavedViewFavorites.FirstOrDefaultAsync(f => f.SavedViewId == id && f.UserId == callerId);
            if (favorite != null)
            {
                _db.UserSavedViewFavorites.Remove(favorite);
                await _db.SaveChangesAsync();
            }
        }

        public async Task ReorderFavoritesAsync(Guid callerId, List<Guid> orderedViewIds)
        {
            var favorites = await _db.UserSavedViewFavorites.Where(f => f.UserId == callerId).ToListAsync();
            var byViewId = favorites.ToDictionary(f => f.SavedViewId);

            for (var i = 0; i < orderedViewIds.Count; i++)
            {
                if (byViewId.TryGetValue(orderedViewIds[i], out var favorite))
                {
                    favorite.SortOrder = i;
                }
            }
            await _db.SaveChangesAsync();
        }

        public async Task<ExecuteViewResultDto> ExecuteAsync(Guid id, Guid callerId, UserRole callerRole, int page, int pageSize)
        {
            var (entityType, filter) = await ResolveDefinitionAsync(id, callerId, callerRole);
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            var (sortField, sortDescending) = await ResolveSortAsync(id);

            return await ExecuteCoreAsync(entityType, filter, sortField, sortDescending, page, pageSize, callerId, callerRole);
        }

        public async Task<PreviewResultDto> PreviewAsync(PreviewViewRequest request, Guid callerId, UserRole callerRole)
        {
            var (accessibleProjectIds, managedProjectIds) = await GetAccessScopeAsync(callerId, callerRole);

            if (request.EntityType == SavedViewEntityTypes.Project)
            {
                var projectResult = await _filterEngine.EvaluateProjectFilterAsync(request.Filter, accessibleProjectIds, managedProjectIds, callerId, callerRole);
                return new PreviewResultDto
                {
                    Total = projectResult.MatchedIds.Count,
                    ResolvedSingleProjectId = projectResult.MatchedIds.Count == 1 ? projectResult.MatchedIds.First() : null,
                    UnavailableFilterFields = projectResult.UnavailableFields
                };
            }

            var taskResult = await _filterEngine.EvaluateTaskFilterAsync(request.Filter, accessibleProjectIds, managedProjectIds, callerId, callerRole);
            var resolvedProjectId = await ResolveSingleProjectIdAsync(taskResult.MatchedIds, isTask: true);
            return new PreviewResultDto
            {
                Total = taskResult.MatchedIds.Count,
                ResolvedSingleProjectId = resolvedProjectId,
                UnavailableFilterFields = taskResult.UnavailableFields
            };
        }

        // ---------- Execution internals ----------

        private async Task<(string EntityType, SavedViewFilterGroupDto Filter)> ResolveDefinitionAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            if (SavedViewSystemDefaults.Find(id) is { } def)
            {
                return (SavedViewEntityTypes.Task, def.Filter);
            }

            var view = await LoadViewAsync(id);
            await EnsureCanViewAsync(view, callerId, callerRole);
            return (view.EntityType, DeserializeFilter(view.FilterJson));
        }

        private async Task<(string? SortField, bool SortDescending)> ResolveSortAsync(Guid id)
        {
            if (SavedViewSystemDefaults.Find(id) is { } def)
            {
                return (def.SortField, def.SortDescending);
            }
            var view = await _db.SavedViews.Where(v => v.Id == id).Select(v => new { v.SortField, v.SortDescending }).FirstOrDefaultAsync();
            return (view?.SortField, view?.SortDescending ?? false);
        }

        private async Task<ExecuteViewResultDto> ExecuteCoreAsync(
            string entityType, SavedViewFilterGroupDto filter, string? sortField, bool sortDescending, int page, int pageSize, Guid callerId, UserRole callerRole)
        {
            var (accessibleProjectIds, managedProjectIds) = await GetAccessScopeAsync(callerId, callerRole);

            if (entityType == SavedViewEntityTypes.Project)
            {
                var result = await _filterEngine.EvaluateProjectFilterAsync(filter, accessibleProjectIds, managedProjectIds, callerId, callerRole);
                return await BuildProjectResultAsync(result, sortField, sortDescending, page, pageSize, callerId, callerRole);
            }

            var taskResult = await _filterEngine.EvaluateTaskFilterAsync(filter, accessibleProjectIds, managedProjectIds, callerId, callerRole);
            return await BuildTaskResultAsync(taskResult, sortField, sortDescending, page, pageSize, callerId, callerRole);
        }

        private async Task<ExecuteViewResultDto> BuildTaskResultAsync(
            SavedViewFilterResult filterResult, string? sortField, bool sortDescending, int page, int pageSize, Guid callerId, UserRole callerRole)
        {
            var matchedIds = filterResult.MatchedIds;
            if (matchedIds.Count == 0)
            {
                return new ExecuteViewResultDto { Total = 0, Page = page, PageSize = pageSize, UnavailableFilterFields = filterResult.UnavailableFields };
            }

            var sortedIds = await SortTaskIdsAsync(matchedIds, sortField, sortDescending);
            var pageIds = sortedIds.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var tasks = await _taskService.GetByIdsAsync(pageIds, callerId, callerRole);
            var byId = tasks.ToDictionary(t => t.Id);
            var orderedDtos = pageIds.Where(byId.ContainsKey).Select(pid => byId[pid].ToDto()).ToList();

            var resolvedProjectId = await ResolveSingleProjectIdAsync(matchedIds, isTask: true);

            return new ExecuteViewResultDto
            {
                Tasks = orderedDtos,
                Total = matchedIds.Count,
                Page = page,
                PageSize = pageSize,
                ResolvedSingleProjectId = resolvedProjectId,
                UnavailableFilterFields = filterResult.UnavailableFields
            };
        }

        private async Task<List<Guid>> SortTaskIdsAsync(HashSet<Guid> ids, string? sortField, bool sortDescending)
        {
            var query = _db.Tasks.Where(t => ids.Contains(t.Id));
            IQueryable<TaskItem> sorted = sortField switch
            {
                "title" => sortDescending ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "status" => sortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "priority" => sortDescending ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
                "createdAt" => sortDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
                "updatedAt" => sortDescending ? query.OrderByDescending(t => t.UpdatedAt) : query.OrderBy(t => t.UpdatedAt),
                _ => sortDescending
                    ? query.OrderByDescending(t => t.DueDate == null).ThenByDescending(t => t.DueDate)
                    : query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)
            };
            return await sorted.Select(t => t.Id).ToListAsync();
        }

        private async Task<ExecuteViewResultDto> BuildProjectResultAsync(
            SavedViewFilterResult filterResult, string? sortField, bool sortDescending, int page, int pageSize, Guid callerId, UserRole callerRole)
        {
            var matchedIds = filterResult.MatchedIds;
            if (matchedIds.Count == 0)
            {
                return new ExecuteViewResultDto { Total = 0, Page = page, PageSize = pageSize, UnavailableFilterFields = filterResult.UnavailableFields };
            }

            var query = _db.Projects.Where(p => matchedIds.Contains(p.Id));
            IQueryable<Project> sorted = sortField switch
            {
                "name" => sortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "createdAt" => sortDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                _ => sortDescending ? query.OrderByDescending(p => p.UpdatedAt) : query.OrderBy(p => p.UpdatedAt)
            };
            var sortedIds = await sorted.Select(p => p.Id).ToListAsync();
            var pageIds = sortedIds.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var projects = await _db.Projects
                .Include(p => p.Owner)
                .Include(p => p.CustomValues).ThenInclude(v => v.CustomField)
                .Where(p => pageIds.Contains(p.Id))
                .ToListAsync();

            if (callerRole != UserRole.Administrator)
            {
                foreach (var project in projects)
                {
                    var role = await _projectAccess.GetProjectRoleAsync(project.Id, callerId);
                    CustomFieldPrivacy.RedactProjectValues(project, callerId, callerRole, role);
                }
            }

            var byId = projects.ToDictionary(p => p.Id);
            var orderedDtos = pageIds.Where(byId.ContainsKey).Select(pid => byId[pid].ToDto()).ToList();

            return new ExecuteViewResultDto
            {
                Projects = orderedDtos,
                Total = matchedIds.Count,
                Page = page,
                PageSize = pageSize,
                ResolvedSingleProjectId = matchedIds.Count == 1 ? matchedIds.First() : null,
                UnavailableFilterFields = filterResult.UnavailableFields
            };
        }

        private async Task<Guid?> ResolveSingleProjectIdAsync(HashSet<Guid> matchedTaskIds, bool isTask)
        {
            if (matchedTaskIds.Count == 0)
            {
                return null;
            }
            var projectIds = await _db.Tasks.Where(t => matchedTaskIds.Contains(t.Id)).Select(t => t.ProjectId).Distinct().Take(2).ToListAsync();
            return projectIds.Count == 1 ? projectIds[0] : null;
        }

        private async Task<(HashSet<Guid> AccessibleProjectIds, HashSet<Guid> ManagedProjectIds)> GetAccessScopeAsync(Guid callerId, UserRole callerRole)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            // Archived projects are excluded, matching GetAssignedToUserAsync's own "My Tasks"
            // convention (TaskService.cs) — a saved view (including the My Tasks system default)
            // should not silently resurrect tasks from a project the caller archived.
            var accessibleProjectIds = isAdmin
                ? (await _db.Projects.Where(p => !p.IsArchived).Select(p => p.Id).ToListAsync()).ToHashSet()
                : (await _db.Projects.Where(p => !p.IsArchived && (p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId))).Select(p => p.Id).ToListAsync()).ToHashSet();

            var managedProjectIds = isAdmin
                ? accessibleProjectIds
                : (await _db.Projects
                    .Where(p => accessibleProjectIds.Contains(p.Id) && (p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId && m.Role == ProjectRole.Manager)))
                    .Select(p => p.Id)
                    .ToListAsync()).ToHashSet();

            return (accessibleProjectIds, managedProjectIds);
        }

        // ---------- Access + validation ----------

        private static void EnsureCanModify(SavedView view, Guid callerId, UserRole callerRole)
        {
            if (view.CreatedByUserId != callerId && callerRole != UserRole.Administrator)
            {
                throw new ForbiddenException("You do not have permission to modify this view.");
            }
        }

        private async Task EnsureCanViewAsync(SavedView view, Guid callerId, UserRole callerRole)
        {
            if (view.CreatedByUserId == callerId || callerRole == UserRole.Administrator || view.IsPublic)
            {
                return;
            }
            var shared = view.Shares.Count > 0
                ? view.Shares.Any(s => s.SharedWithUserId == callerId)
                : await _db.SavedViewShares.AnyAsync(s => s.SavedViewId == view.Id && s.SharedWithUserId == callerId);
            if (!shared)
            {
                throw new ForbiddenException("You do not have access to this view.");
            }
        }

        private async Task<SavedView> LoadViewAsync(Guid id) =>
            await _db.SavedViews
                .Include(v => v.Shares)
                .FirstOrDefaultAsync(v => v.Id == id) ?? throw new NotFoundException("View not found.");

        private static void ValidateRequest(SaveViewRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("View name is required.");
            }
            if (!SavedViewEntityTypes.All.Contains(request.EntityType))
            {
                throw new ValidationException("Unrecognized view entity type.");
            }
            if (!SavedViewLayouts.All.Contains(request.Layout))
            {
                throw new ValidationException("Unrecognized view layout.");
            }
            if (request.EntityType == SavedViewEntityTypes.Project && request.Layout != SavedViewLayouts.Table)
            {
                throw new ValidationException("Project views can only use the Table layout.");
            }
        }

        private async Task ValidateFilterFieldsAsync(string entityType, SavedViewFilterGroupDto group)
        {
            var customFieldIds = new List<Guid>();
            CollectCustomFieldIds(group, customFieldIds);

            if (customFieldIds.Count > 0)
            {
                var expectedEntityType = entityType == SavedViewEntityTypes.Project ? CustomFieldEntityType.Project : CustomFieldEntityType.Task;
                var validIds = await _db.CustomFields
                    .Where(f => customFieldIds.Contains(f.Id) && f.EntityType == expectedEntityType)
                    .Select(f => f.Id)
                    .ToListAsync();
                // A stale/mismatched custom field id is tolerated here (not rejected) — it will
                // simply be reported as an "unavailable" filter at execution time instead, the same
                // graceful degradation a field deleted AFTER save gets. Only structural validation
                // (unrecognized built-in field key) is rejected up front.
                _ = validIds;
            }

            ValidateBuiltInFields(entityType, group);
        }

        private static void CollectCustomFieldIds(SavedViewFilterGroupDto group, List<Guid> ids)
        {
            foreach (var condition in group.Conditions)
            {
                if (condition.Field.StartsWith(SavedViewFields.CustomFieldPrefix, StringComparison.Ordinal) &&
                    Guid.TryParse(condition.Field[SavedViewFields.CustomFieldPrefix.Length..], out var id))
                {
                    ids.Add(id);
                }
            }
            foreach (var subgroup in group.Groups)
            {
                CollectCustomFieldIds(subgroup, ids);
            }
        }

        private static void ValidateBuiltInFields(string entityType, SavedViewFilterGroupDto group)
        {
            var allowedFields = entityType == SavedViewEntityTypes.Project
                ? new[] { SavedViewFields.CreatedAt, SavedViewFields.UpdatedAt }
                : SavedViewFields.TaskFields;

            foreach (var condition in group.Conditions)
            {
                if (condition.Field.StartsWith(SavedViewFields.CustomFieldPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!allowedFields.Contains(condition.Field))
                {
                    throw new ValidationException($"'{condition.Field}' is not a filterable field for {entityType} views.");
                }
            }
            foreach (var subgroup in group.Groups)
            {
                ValidateBuiltInFields(entityType, subgroup);
            }
        }

        private static string SerializeFilter(SavedViewFilterGroupDto filter) => System.Text.Json.JsonSerializer.Serialize(filter);

        private static SavedViewFilterGroupDto DeserializeFilter(string json) =>
            System.Text.Json.JsonSerializer.Deserialize<SavedViewFilterGroupDto>(json) ?? new SavedViewFilterGroupDto();

        private async Task<SavedViewDto> LoadDtoAsync(Guid id, Guid callerId)
        {
            var view = await _db.SavedViews
                .Include(v => v.CreatedBy)
                .Include(v => v.Shares).ThenInclude(s => s.SharedWithUser)
                .FirstAsync(v => v.Id == id);
            var favorite = await _db.UserSavedViewFavorites.FirstOrDefaultAsync(f => f.UserId == callerId && f.SavedViewId == id);
            return ToDto(view, callerId, favorite is null ? [] : new Dictionary<Guid, int> { [id] = favorite.SortOrder });
        }

        private static SavedViewDto ToDto(SavedView v, Guid callerId, Dictionary<Guid, int> favoriteSortOrderByViewId)
        {
            var isOwned = v.CreatedByUserId == callerId;
            var isFavorite = favoriteSortOrderByViewId.TryGetValue(v.Id, out var sortOrder);

            var columns = string.IsNullOrWhiteSpace(v.Columns)
                ? []
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v.Columns) ?? [];

            return new SavedViewDto
            {
                Id = v.Id,
                Name = v.Name,
                Description = v.Description,
                CreatedByUserId = v.CreatedByUserId,
                CreatedByName = v.CreatedBy?.Name ?? "Unknown",
                EntityType = v.EntityType,
                IsPublic = v.IsPublic,
                Filter = DeserializeFilter(v.FilterJson),
                Columns = columns,
                SortField = v.SortField,
                SortDescending = v.SortDescending,
                GroupByField = v.GroupByField,
                Layout = v.Layout,
                IsOwnedByMe = isOwned,
                IsFavorite = isFavorite,
                FavoriteSortOrder = isFavorite ? sortOrder : null,
                IsSystemDefault = false,
                SharedWith = isOwned
                    ? v.Shares.Select(s => new SavedViewSharedUserDto { UserId = s.SharedWithUserId, Name = s.SharedWithUser?.Name ?? "Unknown" }).ToList()
                    : null,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            };
        }

        private static SavedViewDto ToSystemDefaultDto(SavedViewSystemDefaults.Definition def, Dictionary<Guid, int> favoriteSortOrderByViewId)
        {
            var isFavorite = favoriteSortOrderByViewId.TryGetValue(def.Id, out var sortOrder);
            return new SavedViewDto
            {
                Id = def.Id,
                Name = def.Name,
                Description = def.Description,
                CreatedByUserId = Guid.Empty,
                CreatedByName = "System",
                EntityType = SavedViewEntityTypes.Task,
                IsPublic = true,
                Filter = def.Filter,
                Columns = [],
                SortField = def.SortField,
                SortDescending = def.SortDescending,
                GroupByField = null,
                Layout = SavedViewLayouts.Table,
                IsOwnedByMe = false,
                IsFavorite = isFavorite,
                FavoriteSortOrder = isFavorite ? sortOrder : null,
                IsSystemDefault = true,
                SharedWith = null,
                CreatedAt = DateTime.UnixEpoch,
                UpdatedAt = DateTime.UnixEpoch
            };
        }
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.CustomFields;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ICustomFieldService
    {
        Task<IReadOnlyList<CustomField>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole);

        Task<CustomField> GetByIdAsync(Guid customFieldId, Guid callerId, UserRole callerRole);

        /// <param name="projectId">Null creates a field that applies to every project — an
        /// Administrator-only capability, enforced here rather than just at the controller so
        /// the rule holds regardless of which route reaches this method.</param>
        Task<CustomField> CreateAsync(Guid? projectId, CreateCustomFieldRequest request, Guid callerId, UserRole callerRole);

        Task<CustomField> UpdateAsync(Guid customFieldId, UpdateCustomFieldRequest request, Guid callerId, UserRole callerRole);

        /// <param name="confirm">Required (true) to permanently delete a field that still has
        /// task values — see README/CLAUDE "do not silently destroy task data". Deleting a field
        /// with no recorded values never needs confirmation.</param>
        Task DeleteAsync(Guid customFieldId, bool confirm, Guid callerId, UserRole callerRole);

        Task<int> GetUsageCountAsync(Guid customFieldId, Guid callerId, UserRole callerRole);

        /// <summary>Admin > Custom Fields listing across every project, with optional filters —
        /// distinct from GetAllForProjectAsync, which is scoped to one project's effective field
        /// set (its own fields + global ones) for the project grid.</summary>
        Task<IReadOnlyList<CustomField>> GetAllForAdminAsync(Guid? projectId, CustomFieldType? fieldType, bool? isActive);

        Task<CustomFieldOption> AddOptionAsync(Guid customFieldId, string value, Guid callerId, UserRole callerRole);

        Task<CustomFieldOption> UpdateOptionAsync(Guid customFieldId, Guid optionId, CustomFieldOptionRequest request, Guid callerId, UserRole callerRole);

        /// <param name="confirm">Required (true) to permanently delete an option that existing
        /// task values still reference — otherwise those values would silently lose their
        /// selection. Deactivating (IsActive=false) via UpdateOptionAsync is the safe alternative
        /// and never needs confirmation.</param>
        Task DeleteOptionAsync(Guid customFieldId, Guid optionId, bool confirm, Guid callerId, UserRole callerRole);
    }

    public class CustomFieldService(AppDbContext db, IProjectAccessService projectAccess, ISystemSettingsService systemSettings) : ICustomFieldService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;

        public async Task<IReadOnlyList<CustomField>> GetAllForProjectAsync(Guid projectId, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            // A project's effective field set is its own fields plus every "all projects" field —
            // inactive ones stay included so existing values on inactive fields remain visible;
            // only setting a *new* value on one is blocked (TaskService.ValidateAndNormalizeCustomValueAsync).
            return await _db.CustomFields
                .Include(f => f.Options)
                .Where(f => f.ProjectId == projectId || f.ProjectId == null)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();
        }

        public async Task<CustomField> GetByIdAsync(Guid customFieldId, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            if (field.ProjectId is Guid projectId)
            {
                var project = await LoadProjectAsync(projectId);
                await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);
            }
            // A field with no ProjectId applies everywhere — any authenticated caller (already
            // guaranteed by the global FallbackPolicy) can view it, there's no project to check.
            return field;
        }

        public async Task<CustomField> CreateAsync(Guid? projectId, CreateCustomFieldRequest request, Guid callerId, UserRole callerRole)
        {
            if (projectId is Guid pid)
            {
                var project = await LoadProjectAsync(pid);

                // "Allow users to create custom fields" relaxes the tier from manage down to
                // participate, the same inversion pattern as TaskService's "allow members to
                // delete tasks" — field *definitions* otherwise stay a manage-tier action by default.
                var allowMembersToCreate = await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowUsersToCreateCustomFields);
                if (allowMembersToCreate)
                {
                    await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);
                }
                else
                {
                    await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);
                }
            }
            else
            {
                EnsureAdministrator(callerRole);
            }

            await EnsureNameIsAvailableAsync(projectId, request.Name, excludingFieldId: null);

            var field = new CustomField
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = request.Name.Trim(),
                Description = NormalizeOptionalText(request.Description),
                FieldType = request.FieldType,
                IsRequired = request.IsRequired,
                IsActive = true,
                DefaultValue = ValidateAndNormalizeDefaultValue(request.FieldType, request.DefaultValue),
                SortOrder = await GetNextFieldSortOrderAsync(projectId)
            };
            _db.CustomFields.Add(field);

            if (SupportsOptions(request.FieldType) && request.Options is { Count: > 0 })
            {
                var sortOrder = 0;
                foreach (var optionValue in request.Options)
                {
                    if (string.IsNullOrWhiteSpace(optionValue))
                    {
                        continue;
                    }

                    field.Options.Add(new CustomFieldOption
                    {
                        Id = Guid.NewGuid(),
                        CustomFieldId = field.Id,
                        Value = optionValue.Trim(),
                        SortOrder = sortOrder++
                    });
                }
            }

            await _db.SaveChangesAsync();
            return field;
        }

        public async Task<CustomField> UpdateAsync(Guid customFieldId, UpdateCustomFieldRequest request, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);

            await EnsureNameIsAvailableAsync(field.ProjectId, request.Name, excludingFieldId: field.Id);

            field.Name = request.Name.Trim();
            field.Description = NormalizeOptionalText(request.Description);
            field.IsRequired = request.IsRequired;
            field.IsActive = request.IsActive;
            field.DefaultValue = ValidateAndNormalizeDefaultValue(field.FieldType, request.DefaultValue);
            field.SortOrder = request.SortOrder;
            field.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return field;
        }

        public async Task DeleteAsync(Guid customFieldId, bool confirm, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);

            var usageCount = await _db.TaskCustomValues.CountAsync(v => v.CustomFieldId == customFieldId);
            if (usageCount > 0 && !confirm)
            {
                throw new ConflictException(
                    $"'{field.Name}' has values on {usageCount} task{(usageCount == 1 ? "" : "s")}. " +
                    "Deactivate it instead to keep that data, or confirm to permanently delete it and those values.");
            }

            // Cascades to CustomFieldOptions and TaskCustomValues at the database level (Phase 2).
            _db.CustomFields.Remove(field);
            await _db.SaveChangesAsync();
        }

        public async Task<int> GetUsageCountAsync(Guid customFieldId, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);
            return await _db.TaskCustomValues.CountAsync(v => v.CustomFieldId == customFieldId);
        }

        public async Task<IReadOnlyList<CustomField>> GetAllForAdminAsync(Guid? projectId, CustomFieldType? fieldType, bool? isActive)
        {
            var query = _db.CustomFields.Include(f => f.Project).Include(f => f.Options).AsQueryable();

            if (projectId is Guid pid)
            {
                query = query.Where(f => f.ProjectId == pid);
            }
            if (fieldType is CustomFieldType type)
            {
                query = query.Where(f => f.FieldType == type);
            }
            if (isActive is bool active)
            {
                query = query.Where(f => f.IsActive == active);
            }

            return await query
                .OrderBy(f => f.ProjectId == null ? 0 : 1)
                .ThenBy(f => f.Project!.Name)
                .ThenBy(f => f.SortOrder)
                .ToListAsync();
        }

        public async Task<CustomFieldOption> AddOptionAsync(Guid customFieldId, string value, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);
            EnsureFieldSupportsOptions(field);

            var maxSortOrder = await _db.CustomFieldOptions
                .Where(o => o.CustomFieldId == customFieldId)
                .Select(o => (int?)o.SortOrder)
                .MaxAsync();

            var option = new CustomFieldOption
            {
                Id = Guid.NewGuid(),
                CustomFieldId = customFieldId,
                Value = value.Trim(),
                SortOrder = (maxSortOrder ?? -1) + 1
            };
            _db.CustomFieldOptions.Add(option);
            await _db.SaveChangesAsync();
            return option;
        }

        public async Task<CustomFieldOption> UpdateOptionAsync(Guid customFieldId, Guid optionId, CustomFieldOptionRequest request, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);

            var option = await _db.CustomFieldOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.CustomFieldId == customFieldId)
                ?? throw new NotFoundException($"Option '{optionId}' was not found.");

            option.Value = request.Value.Trim();
            if (request.SortOrder is int sortOrder)
            {
                option.SortOrder = sortOrder;
            }
            if (request.IsActive is bool isActive)
            {
                option.IsActive = isActive;
            }

            await _db.SaveChangesAsync();
            return option;
        }

        public async Task DeleteOptionAsync(Guid customFieldId, Guid optionId, bool confirm, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);

            var option = await _db.CustomFieldOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.CustomFieldId == customFieldId)
                ?? throw new NotFoundException($"Option '{optionId}' was not found.");

            // Dropdown stores the option id directly; MultiSelect stores a JSON array that may
            // contain it — a plain string.Contains catches both without needing to know which.
            var affectedValues = await _db.TaskCustomValues
                .Where(v => v.CustomFieldId == customFieldId && v.Value != null && v.Value.Contains(optionId.ToString()))
                .ToListAsync();

            if (affectedValues.Count > 0 && !confirm)
            {
                throw new ConflictException(
                    $"'{option.Value}' is used by {affectedValues.Count} task{(affectedValues.Count == 1 ? "" : "s")}. " +
                    "Disable it instead to keep that data, or confirm to permanently delete it and clear those values.");
            }

            _db.TaskCustomValues.RemoveRange(affectedValues);
            _db.CustomFieldOptions.Remove(option);
            await _db.SaveChangesAsync();
        }

        private async Task<CustomField> LoadFieldAsync(Guid customFieldId)
        {
            var field = await _db.CustomFields.Include(f => f.Options).Include(f => f.Project).FirstOrDefaultAsync(f => f.Id == customFieldId);
            return field ?? throw new NotFoundException($"Custom field '{customFieldId}' was not found.");
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        /// <summary>Field-level authorization for update/delete/option management: a
        /// project-scoped field uses the normal manage tier; a field with no ProjectId (applies
        /// to every project) can only be managed by an Administrator, since no single project
        /// owner/manager should control something that affects projects they don't own.</summary>
        private async Task EnsureCanManageFieldAsync(CustomField field, Guid callerId, UserRole callerRole)
        {
            if (field.ProjectId is Guid projectId)
            {
                var project = await LoadProjectAsync(projectId);
                await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);
            }
            else
            {
                EnsureAdministrator(callerRole);
            }
        }

        private static void EnsureAdministrator(UserRole callerRole)
        {
            if (callerRole != UserRole.Administrator)
            {
                throw new ForbiddenException("Only Administrators can manage custom fields that apply to every project.");
            }
        }

        private async Task EnsureNameIsAvailableAsync(Guid? projectId, string name, Guid? excludingFieldId)
        {
            var normalized = name.Trim().ToLower();
            var query = _db.CustomFields.Where(f => f.Id != excludingFieldId && f.Name.ToLower() == normalized);

            // A project-scoped field's effective visibility set is itself + every "all projects"
            // field, so it only needs to avoid colliding with those. A new "all projects" field is
            // visible everywhere, so it has to avoid colliding with literally any existing field.
            query = projectId is Guid pid
                ? query.Where(f => f.ProjectId == pid || f.ProjectId == null)
                : query;

            if (await query.AnyAsync())
            {
                throw new ConflictException($"A custom field named '{name}' already exists in that scope.");
            }
        }

        private async Task<int> GetNextFieldSortOrderAsync(Guid? projectId)
        {
            var max = await _db.CustomFields.Where(f => f.ProjectId == projectId).Select(f => (int?)f.SortOrder).MaxAsync();
            return (max ?? -1) + 1;
        }

        private static bool SupportsOptions(CustomFieldType type) =>
            type is CustomFieldType.Dropdown or CustomFieldType.MultiSelect;

        private static void EnsureFieldSupportsOptions(CustomField field)
        {
            if (!SupportsOptions(field.FieldType))
            {
                throw new ValidationException($"Field '{field.Name}' does not support options (type is {field.FieldType}).");
            }
        }

        private static string? NormalizeOptionalText(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        /// <summary>Only validated for types whose format is meaningful without other context —
        /// Dropdown/MultiSelect options and User project-membership can't be checked at field-
        /// definition time (the options are being created in this same call; membership is
        /// per-project and this field may apply to every project).</summary>
        private static string? ValidateAndNormalizeDefaultValue(CustomFieldType fieldType, string? value)
        {
            var normalized = NormalizeOptionalText(value);
            if (normalized is null)
            {
                return null;
            }

            switch (fieldType)
            {
                case CustomFieldType.Number:
                case CustomFieldType.Currency:
                    if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    {
                        throw new ValidationException($"'{normalized}' is not a valid default number.");
                    }
                    break;

                case CustomFieldType.Date:
                    if (!DateOnly.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        throw new ValidationException($"'{normalized}' is not a valid default date (expected YYYY-MM-DD).");
                    }
                    break;

                case CustomFieldType.DateTime:
                    if (!DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        throw new ValidationException($"'{normalized}' is not a valid default date/time.");
                    }
                    break;

                case CustomFieldType.Checkbox:
                    if (normalized is not ("true" or "false"))
                    {
                        throw new ValidationException("Default value for a Checkbox field must be 'true' or 'false'.");
                    }
                    break;
            }

            return normalized;
        }
    }
}

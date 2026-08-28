using System.Globalization;
using System.Text.RegularExpressions;
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
        /// <param name="entityType">Which kind of object's field set to load — Task (the
        /// project's task grid columns) or Project (the project's own detail-page fields).</param>
        Task<IReadOnlyList<CustomField>> GetAllForProjectAsync(Guid projectId, CustomFieldEntityType entityType, Guid callerId, UserRole callerRole);

        Task<CustomField> GetByIdAsync(Guid customFieldId, Guid callerId, UserRole callerRole);

        /// <param name="projectId">Null creates a field that applies to every project — an
        /// Administrator-only capability, enforced here rather than just at the controller so
        /// the rule holds regardless of which route reaches this method.</param>
        Task<CustomField> CreateAsync(Guid? projectId, CreateCustomFieldRequest request, Guid callerId, UserRole callerRole);

        Task<CustomField> UpdateAsync(Guid customFieldId, UpdateCustomFieldRequest request, Guid callerId, UserRole callerRole);

        /// <param name="confirm">Required (true) to permanently delete a field that still has
        /// task/project values — see README/CLAUDE "do not silently destroy task data". Deleting
        /// a field with no recorded values never needs confirmation.</param>
        Task DeleteAsync(Guid customFieldId, bool confirm, Guid callerId, UserRole callerRole);

        Task<int> GetUsageCountAsync(Guid customFieldId, Guid callerId, UserRole callerRole);

        /// <summary>Admin > Custom Fields listing across every project, with optional filters —
        /// distinct from GetAllForProjectAsync, which is scoped to one project's effective field
        /// set (its own fields + global ones) for the project grid.</summary>
        Task<IReadOnlyList<CustomField>> GetAllForAdminAsync(Guid? projectId, CustomFieldType? fieldType, CustomFieldEntityType? entityType, bool? isActive);

        Task<CustomFieldOption> AddOptionAsync(Guid customFieldId, string value, Guid callerId, UserRole callerRole);

        Task<CustomFieldOption> UpdateOptionAsync(Guid customFieldId, Guid optionId, CustomFieldOptionRequest request, Guid callerId, UserRole callerRole);

        /// <param name="confirm">Required (true) to permanently delete an option that existing
        /// task values still reference — otherwise those values would silently lose their
        /// selection. Deactivating (IsActive=false) via UpdateOptionAsync is the safe alternative
        /// and never needs confirmation.</param>
        Task DeleteOptionAsync(Guid customFieldId, Guid optionId, bool confirm, Guid callerId, UserRole callerRole);

        /// <summary>Persists a new SortOrder for every field in the given order — backs the admin
        /// field list's drag-and-drop reorder. All fields must share the same EntityType/ProjectId
        /// scope as the first one; mismatched ids are rejected rather than silently skipped.</summary>
        Task ReorderAsync(IReadOnlyList<Guid> orderedFieldIds, Guid callerId, UserRole callerRole);
    }

    public class CustomFieldService(AppDbContext db, IProjectAccessService projectAccess, ISystemSettingsService systemSettings) : ICustomFieldService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;

        public async Task<IReadOnlyList<CustomField>> GetAllForProjectAsync(Guid projectId, CustomFieldEntityType entityType, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);

            // A project's effective field set is its own fields plus every "all projects" field —
            // inactive ones stay included so existing values on inactive fields remain visible;
            // only setting a *new* value on one is blocked (ICustomFieldValueValidator).
            return await _db.CustomFields
                .Include(f => f.Options)
                .Where(f => (f.ProjectId == projectId || f.ProjectId == null) && f.EntityType == entityType)
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
                // edit, the same inversion pattern as TaskService's "allow members to delete
                // tasks" — field *definitions* otherwise stay a manage-tier action by default.
                // Never relaxes all the way to view-tier — a Viewer can never create a field
                // definition regardless of this setting.
                var allowMembersToCreate = await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowUsersToCreateCustomFields);
                if (allowMembersToCreate)
                {
                    await _projectAccess.EnsureCanEditAsync(project.Id, project.OwnerId, callerId, callerRole);
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

            await EnsureNameIsAvailableAsync(projectId, request.EntityType, request.Name, excludingFieldId: null);
            var identifier = await GenerateIdentifierAsync(projectId, request.EntityType, request.Name);
            await ValidateConditionAsync(projectId, request.EntityType, request.ConditionFieldId, request.ConditionOperator, excludingFieldId: null);
            ValidateTypeSettings(request.FieldType, request.MaxLength, request.MinValue, request.MaxValue, request.DecimalPlaces);

            var field = new CustomField
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = request.Name.Trim(),
                Identifier = identifier,
                Description = NormalizeOptionalText(request.Description),
                FieldType = request.FieldType,
                EntityType = request.EntityType,
                IsRequired = request.IsRequired,
                IsActive = true,
                DefaultValue = ValidateAndNormalizeDefaultValue(request.FieldType, request.DefaultValue),
                SortOrder = await GetNextFieldSortOrderAsync(projectId, request.EntityType),
                GroupName = NormalizeOptionalText(request.GroupName),
                Placeholder = NormalizeOptionalText(request.Placeholder),
                MaxLength = request.MaxLength,
                MinValue = request.MinValue,
                MaxValue = request.MaxValue,
                DecimalPlaces = request.DecimalPlaces,
                IsPrivate = request.IsPrivate,
                VisibleToRoles = NormalizeOptionalText(request.VisibleToRoles),
                ConditionFieldId = request.ConditionFieldId,
                ConditionOperator = request.ConditionOperator,
                ConditionValue = NormalizeOptionalText(request.ConditionValue)
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

            await EnsureNameIsAvailableAsync(field.ProjectId, field.EntityType, request.Name, excludingFieldId: field.Id);
            await ValidateConditionAsync(field.ProjectId, field.EntityType, request.ConditionFieldId, request.ConditionOperator, excludingFieldId: field.Id);
            ValidateTypeSettings(field.FieldType, request.MaxLength, request.MinValue, request.MaxValue, request.DecimalPlaces);

            field.Name = request.Name.Trim();
            field.Description = NormalizeOptionalText(request.Description);
            field.IsRequired = request.IsRequired;
            field.IsActive = request.IsActive;
            field.DefaultValue = ValidateAndNormalizeDefaultValue(field.FieldType, request.DefaultValue);
            field.SortOrder = request.SortOrder;
            field.GroupName = NormalizeOptionalText(request.GroupName);
            field.Placeholder = NormalizeOptionalText(request.Placeholder);
            field.MaxLength = request.MaxLength;
            field.MinValue = request.MinValue;
            field.MaxValue = request.MaxValue;
            field.DecimalPlaces = request.DecimalPlaces;
            field.IsPrivate = request.IsPrivate;
            field.VisibleToRoles = NormalizeOptionalText(request.VisibleToRoles);
            field.ConditionFieldId = request.ConditionFieldId;
            field.ConditionOperator = request.ConditionOperator;
            field.ConditionValue = NormalizeOptionalText(request.ConditionValue);
            field.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return field;
        }

        public async Task DeleteAsync(Guid customFieldId, bool confirm, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);

            var usageCount = field.EntityType == CustomFieldEntityType.Project
                ? await _db.ProjectCustomValues.CountAsync(v => v.CustomFieldId == customFieldId)
                : await _db.TaskCustomValues.CountAsync(v => v.CustomFieldId == customFieldId);
            if (usageCount > 0 && !confirm)
            {
                var noun = field.EntityType == CustomFieldEntityType.Project ? "project" : "task";
                throw new ConflictException(
                    $"'{field.Name}' has values on {usageCount} {noun}{(usageCount == 1 ? "" : "s")}. " +
                    "Deactivate it instead to keep that data, or confirm to permanently delete it and those values.");
            }

            // Cascades to CustomFieldOptions and Task/ProjectCustomValues at the database level.
            _db.CustomFields.Remove(field);
            await _db.SaveChangesAsync();
        }

        public async Task<int> GetUsageCountAsync(Guid customFieldId, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            await EnsureCanManageFieldAsync(field, callerId, callerRole);
            return field.EntityType == CustomFieldEntityType.Project
                ? await _db.ProjectCustomValues.CountAsync(v => v.CustomFieldId == customFieldId)
                : await _db.TaskCustomValues.CountAsync(v => v.CustomFieldId == customFieldId);
        }

        public async Task<IReadOnlyList<CustomField>> GetAllForAdminAsync(Guid? projectId, CustomFieldType? fieldType, CustomFieldEntityType? entityType, bool? isActive)
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
            if (entityType is CustomFieldEntityType et)
            {
                query = query.Where(f => f.EntityType == et);
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

            // Dropdown/MultiSelect/UserMulti all store either the raw option id or a JSON array
            // that may contain it — a plain string.Contains catches every shape without needing
            // to know which one this field uses.
            var affectedTaskValues = await _db.TaskCustomValues
                .Where(v => v.CustomFieldId == customFieldId && v.Value != null && v.Value.Contains(optionId.ToString()))
                .ToListAsync();
            var affectedProjectValues = await _db.ProjectCustomValues
                .Where(v => v.CustomFieldId == customFieldId && v.Value != null && v.Value.Contains(optionId.ToString()))
                .ToListAsync();
            var affectedCount = affectedTaskValues.Count + affectedProjectValues.Count;

            if (affectedCount > 0 && !confirm)
            {
                throw new ConflictException(
                    $"'{option.Value}' is used by {affectedCount} value{(affectedCount == 1 ? "" : "s")}. " +
                    "Disable it instead to keep that data, or confirm to permanently delete it and clear those values.");
            }

            _db.TaskCustomValues.RemoveRange(affectedTaskValues);
            _db.ProjectCustomValues.RemoveRange(affectedProjectValues);
            _db.CustomFieldOptions.Remove(option);
            await _db.SaveChangesAsync();
        }

        public async Task ReorderAsync(IReadOnlyList<Guid> orderedFieldIds, Guid callerId, UserRole callerRole)
        {
            if (orderedFieldIds.Count == 0)
            {
                return;
            }

            var fields = await _db.CustomFields.Where(f => orderedFieldIds.Contains(f.Id)).ToListAsync();
            if (fields.Count != orderedFieldIds.Count)
            {
                throw new NotFoundException("One or more fields to reorder were not found.");
            }

            var scopeProjectId = fields[0].ProjectId;
            var scopeEntityType = fields[0].EntityType;
            if (fields.Any(f => f.ProjectId != scopeProjectId || f.EntityType != scopeEntityType))
            {
                throw new ValidationException("Cannot reorder fields from different scopes together.");
            }

            await EnsureCanManageFieldAsync(fields[0], callerId, callerRole);

            for (var i = 0; i < orderedFieldIds.Count; i++)
            {
                var field = fields.First(f => f.Id == orderedFieldIds[i]);
                field.SortOrder = i;
                field.UpdatedAt = DateTime.UtcNow;
            }

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

        private async Task EnsureNameIsAvailableAsync(Guid? projectId, CustomFieldEntityType entityType, string name, Guid? excludingFieldId)
        {
            var normalized = name.Trim().ToLower();
            var query = _db.CustomFields.Where(f => f.Id != excludingFieldId && f.EntityType == entityType && f.Name.ToLower() == normalized);

            // A project-scoped field's effective visibility set is itself + every "all projects"
            // field, so it only needs to avoid colliding with those. A new "all projects" field is
            // visible everywhere, so it has to avoid colliding with literally any existing field
            // of the same EntityType.
            query = projectId is Guid pid
                ? query.Where(f => f.ProjectId == pid || f.ProjectId == null)
                : query;

            if (await query.AnyAsync())
            {
                throw new ConflictException($"A custom field named '{name}' already exists in that scope.");
            }
        }

        /// <summary>Slugifies Name into a stable snake_case key (spec #4: "Property Address" ->
        /// "property_address"), then disambiguates against the same identifier-uniqueness scope
        /// Name itself uses (spec #5). Never re-derived on rename — see CustomField.Identifier's
        /// own doc comment.</summary>
        private async Task<string> GenerateIdentifierAsync(Guid? projectId, CustomFieldEntityType entityType, string name)
        {
            var baseIdentifier = Slugify(name);
            var candidate = baseIdentifier;
            var suffix = 2;

            while (true)
            {
                var query = _db.CustomFields.Where(f => f.EntityType == entityType && f.Identifier == candidate);
                query = projectId is Guid pid
                    ? query.Where(f => f.ProjectId == pid || f.ProjectId == null)
                    : query;

                if (!await query.AnyAsync())
                {
                    return candidate;
                }

                candidate = $"{baseIdentifier}_{suffix++}";
            }
        }

        private static readonly Regex NonAlphanumeric = new("[^a-z0-9]+", RegexOptions.Compiled);

        private static string Slugify(string name)
        {
            var lower = name.Trim().ToLowerInvariant();
            var slug = NonAlphanumeric.Replace(lower, "_").Trim('_');
            return string.IsNullOrEmpty(slug) ? "field" : slug;
        }

        private async Task<int> GetNextFieldSortOrderAsync(Guid? projectId, CustomFieldEntityType entityType)
        {
            var max = await _db.CustomFields.Where(f => f.ProjectId == projectId && f.EntityType == entityType).Select(f => (int?)f.SortOrder).MaxAsync();
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

        /// <summary>A field can only condition on another field in the exact same scope
        /// (EntityType + effective project visibility) — conditioning a Task field on a Project
        /// field (or vice versa) has no coherent evaluation point, and a field can't condition on
        /// itself or form a cycle.</summary>
        private async Task ValidateConditionAsync(Guid? projectId, CustomFieldEntityType entityType, Guid? conditionFieldId, AutomationConditionOperator? conditionOperator, Guid? excludingFieldId)
        {
            if (conditionFieldId is null)
            {
                return;
            }

            if (conditionFieldId == excludingFieldId)
            {
                throw new ValidationException("A field cannot be conditioned on itself.");
            }

            if (conditionOperator is null)
            {
                throw new ValidationException("A condition operator is required when a condition field is set.");
            }

            var controllingField = await _db.CustomFields.FirstOrDefaultAsync(f => f.Id == conditionFieldId)
                ?? throw new ValidationException("The selected condition field does not exist.");

            if (controllingField.EntityType != entityType)
            {
                throw new ValidationException("A field's visibility condition must reference another field of the same scope (Task/Project).");
            }

            var sameProjectVisibility = controllingField.ProjectId == projectId || controllingField.ProjectId is null || projectId is null;
            if (!sameProjectVisibility)
            {
                throw new ValidationException("The selected condition field is not visible in the same project scope.");
            }

            // A depends on B, B must not (transitively) depend back on A — walk B's own
            // condition chain looking for A, capped at 20 hops as a defensive limit.
            var current = controllingField;
            for (var i = 0; i < 20; i++)
            {
                if (current.ConditionFieldId is not Guid nextId)
                {
                    break;
                }
                if (nextId == excludingFieldId)
                {
                    throw new ValidationException("This condition would create a circular dependency between fields.");
                }

                var next = await _db.CustomFields.FirstOrDefaultAsync(f => f.Id == nextId);
                if (next is null)
                {
                    break;
                }
                current = next;
            }
        }

        private static void ValidateTypeSettings(CustomFieldType fieldType, int? maxLength, decimal? minValue, decimal? maxValue, int? decimalPlaces)
        {
            var supportsMaxLength = fieldType is CustomFieldType.Text or CustomFieldType.LongText or CustomFieldType.Url or CustomFieldType.Email or CustomFieldType.Phone;
            if (maxLength is int ml)
            {
                if (!supportsMaxLength)
                {
                    throw new ValidationException("Max length only applies to text-like field types.");
                }
                if (ml <= 0)
                {
                    throw new ValidationException("Max length must be greater than zero.");
                }
            }

            var supportsRange = fieldType is CustomFieldType.Number or CustomFieldType.Currency;
            if ((minValue is not null || maxValue is not null || decimalPlaces is not null) && !supportsRange)
            {
                throw new ValidationException("Minimum, maximum, and decimal places only apply to Number/Currency field types.");
            }
            if (minValue is decimal min && maxValue is decimal max && min > max)
            {
                throw new ValidationException("Minimum cannot be greater than maximum.");
            }
            if (decimalPlaces is int places && places < 0)
            {
                throw new ValidationException("Decimal places cannot be negative.");
            }
        }

        private static string? NormalizeOptionalText(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        /// <summary>Only validated for types whose format is meaningful without other context —
        /// Dropdown/MultiSelect options and User/UserMulti project-membership can't be checked at
        /// field-definition time (the options are being created in this same call; membership is
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

                case CustomFieldType.Email:
                    if (!System.Net.Mail.MailAddress.TryCreate(normalized, out _))
                    {
                        throw new ValidationException($"'{normalized}' is not a valid default email address.");
                    }
                    break;
            }

            return normalized;
        }
    }
}

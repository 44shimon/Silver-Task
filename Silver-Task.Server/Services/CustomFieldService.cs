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

        Task<CustomField> CreateAsync(Guid projectId, CreateCustomFieldRequest request, Guid callerId, UserRole callerRole);

        Task<CustomField> UpdateAsync(Guid customFieldId, UpdateCustomFieldRequest request, Guid callerId, UserRole callerRole);

        Task DeleteAsync(Guid customFieldId, Guid callerId, UserRole callerRole);

        Task<CustomFieldOption> AddOptionAsync(Guid customFieldId, string value, Guid callerId, UserRole callerRole);

        Task<CustomFieldOption> UpdateOptionAsync(Guid customFieldId, Guid optionId, string value, Guid callerId, UserRole callerRole);

        Task DeleteOptionAsync(Guid customFieldId, Guid optionId, Guid callerId, UserRole callerRole);
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

            return await _db.CustomFields
                .Include(f => f.Options)
                .Where(f => f.ProjectId == projectId)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();
        }

        public async Task<CustomField> GetByIdAsync(Guid customFieldId, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            var project = await LoadProjectAsync(field.ProjectId);
            await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);
            return field;
        }

        public async Task<CustomField> CreateAsync(Guid projectId, CreateCustomFieldRequest request, Guid callerId, UserRole callerRole)
        {
            var project = await LoadProjectAsync(projectId);

            // "Allow users to create custom fields" relaxes the tier from manage down to
            // participate, the same inversion pattern as TaskService's "allow members to delete
            // tasks" — field *definitions* otherwise stay a manage-tier action by default.
            var allowMembersToCreate = await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowUsersToCreateCustomFields);
            if (allowMembersToCreate)
            {
                await _projectAccess.EnsureCanParticipateAsync(project.Id, project.OwnerId, callerId, callerRole);
            }
            else
            {
                await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);
            }

            await EnsureNameIsAvailableAsync(projectId, request.Name, excludingFieldId: null);

            var field = new CustomField
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = request.Name.Trim(),
                FieldType = request.FieldType,
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
            var project = await LoadProjectAsync(field.ProjectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            await EnsureNameIsAvailableAsync(field.ProjectId, request.Name, excludingFieldId: field.Id);

            field.Name = request.Name.Trim();
            field.SortOrder = request.SortOrder;
            field.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return field;
        }

        public async Task DeleteAsync(Guid customFieldId, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            var project = await LoadProjectAsync(field.ProjectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            // Cascades to CustomFieldOptions and TaskCustomValues at the database level (Phase 2).
            _db.CustomFields.Remove(field);
            await _db.SaveChangesAsync();
        }

        public async Task<CustomFieldOption> AddOptionAsync(Guid customFieldId, string value, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            var project = await LoadProjectAsync(field.ProjectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);
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

        public async Task<CustomFieldOption> UpdateOptionAsync(Guid customFieldId, Guid optionId, string value, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            var project = await LoadProjectAsync(field.ProjectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            var option = await _db.CustomFieldOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.CustomFieldId == customFieldId)
                ?? throw new NotFoundException($"Option '{optionId}' was not found.");

            option.Value = value.Trim();
            await _db.SaveChangesAsync();
            return option;
        }

        public async Task DeleteOptionAsync(Guid customFieldId, Guid optionId, Guid callerId, UserRole callerRole)
        {
            var field = await LoadFieldAsync(customFieldId);
            var project = await LoadProjectAsync(field.ProjectId);
            await _projectAccess.EnsureCanManageAsync(project.Id, project.OwnerId, callerId, callerRole);

            var option = await _db.CustomFieldOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.CustomFieldId == customFieldId)
                ?? throw new NotFoundException($"Option '{optionId}' was not found.");

            // Clear any task values referencing this option so nothing is left pointing at a
            // deleted option: Dropdown stores the option id directly, MultiSelect stores a JSON
            // array that may contain it — a plain string.Contains catches both.
            var affectedValues = await _db.TaskCustomValues
                .Where(v => v.CustomFieldId == customFieldId && v.Value != null && v.Value.Contains(optionId.ToString()))
                .ToListAsync();
            _db.TaskCustomValues.RemoveRange(affectedValues);

            _db.CustomFieldOptions.Remove(option);
            await _db.SaveChangesAsync();
        }

        private async Task<CustomField> LoadFieldAsync(Guid customFieldId)
        {
            var field = await _db.CustomFields.Include(f => f.Options).FirstOrDefaultAsync(f => f.Id == customFieldId);
            return field ?? throw new NotFoundException($"Custom field '{customFieldId}' was not found.");
        }

        private async Task<Project> LoadProjectAsync(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            return project ?? throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        private async Task EnsureNameIsAvailableAsync(Guid projectId, string name, Guid? excludingFieldId)
        {
            var normalized = name.Trim().ToLower();
            var taken = await _db.CustomFields.AnyAsync(f =>
                f.ProjectId == projectId && f.Id != excludingFieldId && f.Name.ToLower() == normalized);

            if (taken)
            {
                throw new ConflictException($"A custom field named '{name}' already exists on this project.");
            }
        }

        private async Task<int> GetNextFieldSortOrderAsync(Guid projectId)
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
    }
}

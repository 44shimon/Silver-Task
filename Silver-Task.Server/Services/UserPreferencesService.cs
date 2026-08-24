using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Services
{
    public interface IUserPreferencesService
    {
        Task<UserPreference> GetOrCreateAsync(Guid userId);

        Task<UserPreference> UpdateAsync(Guid userId, UpdatePreferencesRequest request);
    }

    /// <summary>
    /// Backs the Preferences settings tab. Preferences are never trusted as-typed from the
    /// frontend — Theme/TimeFormat/DefaultTaskView are checked against small allow-lists and
    /// DefaultProjectId (if set) must be a project the user can actually see, exactly like every
    /// other write path in this app validates server-side regardless of what the UI already
    /// restricts client-side.
    /// </summary>
    public class UserPreferencesService(AppDbContext db, IProjectAccessService projectAccess) : IUserPreferencesService
    {
        private static readonly HashSet<string> ValidThemes = new(StringComparer.OrdinalIgnoreCase) { "Light", "Dark", "System" };
        private static readonly HashSet<string> ValidTimeFormats = new(StringComparer.OrdinalIgnoreCase) { "12h", "24h" };
        private static readonly HashSet<string> ValidTaskViews =
            new(StringComparer.OrdinalIgnoreCase) { "table", "kanban", "calendar", "timeline", "gantt" };
        private static readonly HashSet<string> ValidDateFormats =
            new(StringComparer.OrdinalIgnoreCase) { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "dd MMM yyyy" };

        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;

        public async Task<UserPreference> GetOrCreateAsync(Guid userId)
        {
            var existing = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
            if (existing is not null)
            {
                return existing;
            }

            var preference = new UserPreference { Id = Guid.NewGuid(), UserId = userId };
            _db.UserPreferences.Add(preference);
            await _db.SaveChangesAsync();
            return preference;
        }

        public async Task<UserPreference> UpdateAsync(Guid userId, UpdatePreferencesRequest request)
        {
            if (!ValidThemes.Contains(request.Theme))
            {
                throw new ValidationException($"'{request.Theme}' is not a valid theme.");
            }
            if (!ValidTimeFormats.Contains(request.TimeFormat))
            {
                throw new ValidationException($"'{request.TimeFormat}' is not a valid time format.");
            }
            if (request.DefaultTaskView is not null && !ValidTaskViews.Contains(request.DefaultTaskView))
            {
                throw new ValidationException($"'{request.DefaultTaskView}' is not a valid default task view.");
            }
            if (!ValidDateFormats.Contains(request.DateFormat))
            {
                throw new ValidationException($"'{request.DateFormat}' is not a supported date format.");
            }
            // TimeZoneInfo owns the real (IANA/Windows) timezone database — reusing it here is
            // the actual validation, not just a hand-maintained allow-list that could go stale.
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                throw new ValidationException($"'{request.TimeZone}' is not a recognized time zone.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new ValidationException($"'{request.TimeZone}' is not a recognized time zone.");
            }
            if (request.DefaultProjectId is Guid projectId)
            {
                var project = await _db.Projects.FindAsync(projectId)
                    ?? throw new ValidationException("The selected default project does not exist.");
                var isMember = await _projectAccess.IsMemberAsync(projectId, userId);
                if (!isMember && project.OwnerId != userId)
                {
                    throw new ValidationException("You can only set a default project you have access to.");
                }
            }

            var preference = await GetOrCreateAsync(userId);
            preference.Theme = request.Theme;
            preference.DefaultProjectId = request.DefaultProjectId;
            preference.DefaultTaskView = request.DefaultTaskView;
            preference.DateFormat = request.DateFormat.Trim();
            preference.TimeFormat = request.TimeFormat;
            preference.TimeZone = request.TimeZone.Trim();
            preference.ItemsPerPage = request.ItemsPerPage;
            preference.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return preference;
        }
    }
}

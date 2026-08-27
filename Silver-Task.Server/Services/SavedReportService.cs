using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Reports;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ISavedReportService
    {
        /// <summary>Own reports plus reports explicitly shared with the caller — a shared report
        /// whose ProjectId the caller can no longer access is proactively hidden here too (not
        /// just blocked at execute time), see the implementation's own doc comment.</summary>
        Task<List<SavedReportDto>> ListForCallerAsync(Guid callerId, UserRole callerRole);

        Task<SavedReportDto> CreateAsync(Guid callerId, UserRole callerRole, SaveReportRequest request);

        Task<SavedReportDto> UpdateAsync(Guid id, Guid callerId, UserRole callerRole, SaveReportRequest request);

        /// <summary>Only ever removes the SavedReport row and its own Shares/Favorites (both
        /// Cascade FKs onto SavedReportId) — never cascades into Tasks/Projects/Users/Files/
        /// Notifications, since nothing else has a FK onto SavedReport.</summary>
        Task DeleteAsync(Guid id, Guid callerId, UserRole callerRole);

        Task<SavedReportDto> DuplicateAsync(Guid id, Guid callerId, UserRole callerRole);

        /// <summary>Resolves the target by email (same convention as
        /// IProjectService.AddMemberAsync) — returns null if no such user exists, rather than
        /// throwing, so the controller can surface a clean "user not found" response.</summary>
        Task<bool> ShareAsync(Guid id, Guid callerId, UserRole callerRole, string email);

        Task UnshareAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId);

        Task FavoriteAsync(Guid id, Guid callerId);

        Task UnfavoriteAsync(Guid id, Guid callerId);

        /// <summary>The critical security step (spec's own explicit requirement): re-validates
        /// the CURRENT caller's live project access before returning the parsed configuration to
        /// run — re-checked every single execution, regardless of who created or shared the
        /// report, or what access existed at share time. See ISavedReportService's own doc
        /// comment on why this can never be a share-time-only check.</summary>
        Task<ReportConfiguration> PrepareExecutionAsync(Guid id, Guid callerId, UserRole callerRole);
    }

    /// <summary>
    /// Phase 38 — CRUD/share/favorite/execute for SavedReport. Sharing is deliberately narrow
    /// (explicit user-to-user only, no bulk project-members/role sharing — a disclosed scope cut,
    /// see SavedReportShare's own doc comment) since the report's actual security boundary never
    /// depends on how it was shared: PrepareExecutionAsync always re-verifies live project access
    /// via IProjectAccessService, the same predicate every other project-scoped read uses.
    /// </summary>
    public class SavedReportService(AppDbContext db, IProjectAccessService projectAccess) : ISavedReportService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() },
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task<List<SavedReportDto>> ListForCallerAsync(Guid callerId, UserRole callerRole)
        {
            var reports = await _db.SavedReports
                .Include(r => r.CreatedBy)
                .Include(r => r.Project)
                .Include(r => r.Shares).ThenInclude(s => s.SharedWithUser)
                .Include(r => r.FavoritedBy)
                .Where(r => r.CreatedByUserId == callerId || r.Shares.Any(s => s.SharedWithUserId == callerId))
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();

            var visible = new List<SavedReport>();
            foreach (var report in reports)
            {
                if (report.ProjectId is Guid projectId && report.Project != null)
                {
                    var canAccess = callerRole == UserRole.Administrator || report.Project.OwnerId == callerId ||
                        await _projectAccess.IsMemberAsync(projectId, callerId);
                    if (!canAccess)
                    {
                        continue;
                    }
                }
                visible.Add(report);
            }

            return visible.Select(r => ToDto(r, callerId)).ToList();
        }

        public async Task<SavedReportDto> CreateAsync(Guid callerId, UserRole callerRole, SaveReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("Report name is required.");
            }

            ValidateConfiguration(request.Configuration);

            Project? project = null;
            if (request.ProjectId is Guid projectId)
            {
                project = await _db.Projects.FindAsync(projectId) ?? throw new NotFoundException("Project not found.");
                await _projectAccess.EnsureCanParticipateAsync(projectId, project.OwnerId, callerId, callerRole);
            }

            var report = new SavedReport
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description,
                CreatedByUserId = callerId,
                ProjectId = request.ProjectId,
                Configuration = request.Configuration,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.SavedReports.Add(report);
            await _db.SaveChangesAsync();

            return await LoadDtoAsync(report.Id, callerId, callerRole);
        }

        public async Task<SavedReportDto> UpdateAsync(Guid id, Guid callerId, UserRole callerRole, SaveReportRequest request)
        {
            var report = await _db.SavedReports.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Report not found.");
            EnsureCanModify(report, callerId, callerRole);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("Report name is required.");
            }
            ValidateConfiguration(request.Configuration);

            if (request.ProjectId is Guid projectId)
            {
                var project = await _db.Projects.FindAsync(projectId) ?? throw new NotFoundException("Project not found.");
                await _projectAccess.EnsureCanParticipateAsync(projectId, project.OwnerId, callerId, callerRole);
            }

            report.Name = request.Name.Trim();
            report.Description = request.Description;
            report.ProjectId = request.ProjectId;
            report.Configuration = request.Configuration;
            report.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return await LoadDtoAsync(report.Id, callerId, callerRole);
        }

        public async Task DeleteAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var report = await _db.SavedReports.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Report not found.");
            EnsureCanModify(report, callerId, callerRole);

            _db.SavedReports.Remove(report);
            await _db.SaveChangesAsync();
        }

        public async Task<SavedReportDto> DuplicateAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var report = await _db.SavedReports.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Report not found.");
            await EnsureCanViewAsync(report, callerId, callerRole);

            var copy = new SavedReport
            {
                Id = Guid.NewGuid(),
                Name = $"{report.Name} (Copy)",
                Description = report.Description,
                CreatedByUserId = callerId,
                ProjectId = report.ProjectId,
                Configuration = report.Configuration,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.SavedReports.Add(copy);
            await _db.SaveChangesAsync();

            return await LoadDtoAsync(copy.Id, callerId, callerRole);
        }

        public async Task<bool> ShareAsync(Guid id, Guid callerId, UserRole callerRole, string email)
        {
            var report = await _db.SavedReports.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Report not found.");
            EnsureCanModify(report, callerId, callerRole);

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var target = await _db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (target is null)
            {
                return false;
            }
            if (target.Id == report.CreatedByUserId)
            {
                throw new ValidationException("Cannot share a report with its own owner.");
            }

            var alreadyShared = await _db.SavedReportShares.AnyAsync(s => s.SavedReportId == id && s.SharedWithUserId == target.Id);
            if (alreadyShared)
            {
                return true;
            }

            _db.SavedReportShares.Add(new SavedReportShare
            {
                Id = Guid.NewGuid(),
                SavedReportId = id,
                SharedWithUserId = target.Id,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task UnshareAsync(Guid id, Guid callerId, UserRole callerRole, Guid targetUserId)
        {
            var report = await _db.SavedReports.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Report not found.");
            EnsureCanModify(report, callerId, callerRole);

            var share = await _db.SavedReportShares.FirstOrDefaultAsync(s => s.SavedReportId == id && s.SharedWithUserId == targetUserId);
            if (share != null)
            {
                _db.SavedReportShares.Remove(share);
                await _db.SaveChangesAsync();
            }
        }

        public async Task FavoriteAsync(Guid id, Guid callerId)
        {
            var exists = await _db.SavedReports.AnyAsync(r => r.Id == id &&
                (r.CreatedByUserId == callerId || r.Shares.Any(s => s.SharedWithUserId == callerId)));
            if (!exists)
            {
                throw new NotFoundException("Report not found.");
            }

            var alreadyFavorited = await _db.UserReportFavorites.AnyAsync(f => f.SavedReportId == id && f.UserId == callerId);
            if (alreadyFavorited)
            {
                return;
            }

            _db.UserReportFavorites.Add(new UserReportFavorite
            {
                Id = Guid.NewGuid(),
                UserId = callerId,
                SavedReportId = id,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task UnfavoriteAsync(Guid id, Guid callerId)
        {
            var favorite = await _db.UserReportFavorites.FirstOrDefaultAsync(f => f.SavedReportId == id && f.UserId == callerId);
            if (favorite != null)
            {
                _db.UserReportFavorites.Remove(favorite);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<ReportConfiguration> PrepareExecutionAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var report = await _db.SavedReports
                .Include(r => r.Project)
                .Include(r => r.Shares)
                .FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Report not found.");

            await EnsureCanViewAsync(report, callerId, callerRole);

            if (report.ProjectId is Guid projectId && report.Project != null)
            {
                await _projectAccess.EnsureCanParticipateAsync(projectId, report.Project.OwnerId, callerId, callerRole);
            }

            return ValidateConfiguration(report.Configuration);
        }

        private static void EnsureCanModify(SavedReport report, Guid callerId, UserRole callerRole)
        {
            if (report.CreatedByUserId != callerId && callerRole != UserRole.Administrator)
            {
                throw new ForbiddenException("You do not have permission to modify this report.");
            }
        }

        private async Task EnsureCanViewAsync(SavedReport report, Guid callerId, UserRole callerRole)
        {
            if (report.CreatedByUserId == callerId || callerRole == UserRole.Administrator)
            {
                return;
            }
            var shared = report.Shares.Count > 0
                ? report.Shares.Any(s => s.SharedWithUserId == callerId)
                : await _db.SavedReportShares.AnyAsync(s => s.SavedReportId == report.Id && s.SharedWithUserId == callerId);
            if (!shared)
            {
                throw new ForbiddenException("You do not have access to this report.");
            }
        }

        private static ReportConfiguration ValidateConfiguration(string json)
        {
            ReportConfiguration? config;
            try
            {
                config = JsonSerializer.Deserialize<ReportConfiguration>(json, JsonOptions);
            }
            catch (JsonException)
            {
                throw new ValidationException("Report configuration is not valid JSON.");
            }

            if (config is null || string.IsNullOrWhiteSpace(config.ReportType) || !ReportTypes.All.Contains(config.ReportType))
            {
                throw new ValidationException("Report configuration has an unrecognized report type.");
            }
            if (config.ReportType == ReportTypes.Custom &&
                (string.IsNullOrWhiteSpace(config.GroupBy) || !ReportTypes.GroupByFields.Contains(config.GroupBy)))
            {
                throw new ValidationException("Custom reports require a valid Group By field.");
            }
            return config;
        }

        private async Task<SavedReportDto> LoadDtoAsync(Guid id, Guid callerId, UserRole callerRole)
        {
            var report = await _db.SavedReports
                .Include(r => r.CreatedBy)
                .Include(r => r.Project)
                .Include(r => r.Shares).ThenInclude(s => s.SharedWithUser)
                .Include(r => r.FavoritedBy)
                .FirstAsync(r => r.Id == id);
            return ToDto(report, callerId);
        }

        private static SavedReportDto ToDto(SavedReport r, Guid callerId)
        {
            var isOwned = r.CreatedByUserId == callerId;
            var reportType = "Unknown";
            try
            {
                var config = JsonSerializer.Deserialize<ReportConfiguration>(r.Configuration, JsonOptions);
                if (config != null)
                {
                    reportType = config.ReportType;
                }
            }
            catch (JsonException)
            {
                // Leave as "Unknown" — display-only, never blocks the row from listing.
            }

            return new SavedReportDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                CreatedByUserId = r.CreatedByUserId,
                CreatedByName = r.CreatedBy?.Name ?? "Unknown",
                ProjectId = r.ProjectId,
                ProjectName = r.Project?.Name,
                ReportType = reportType,
                Configuration = r.Configuration,
                IsOwnedByMe = isOwned,
                IsFavorite = r.FavoritedBy.Any(f => f.UserId == callerId),
                SharedWith = isOwned
                    ? r.Shares.Select(s => new SharedUserDto { UserId = s.SharedWithUserId, Name = s.SharedWithUser?.Name ?? "Unknown" }).ToList()
                    : null,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Computes "what can this user do" as a set of Permissions.* strings — the single place the
    /// fixed role -> permission matrix lives, so the frontend's usePermissions() hook and any
    /// future backend check both read the same answer instead of each re-deriving it.
    ///
    /// This does NOT replace ProjectAccessService — every actual authorization decision in the
    /// backend still goes through EnsureCanParticipateAsync/EnsureCanEditAsync/EnsureCanManageAsync
    /// (imperative, per-request, always re-checked against the database). PermissionService is
    /// purely descriptive: it answers "what would this user be allowed to do" for API responses
    /// (GET /auth/me's `permissions`, ProjectDto's `myPermissions`, the Admin Roles & Permissions
    /// read-only viewer) — nothing here is itself a security boundary.
    ///
    /// Scope decision: the matrix (which roles grant which permissions) is fixed, code-defined
    /// configuration, not a database-editable table — see Permissions.cs's own doc comment for
    /// the full reasoning. What's dynamic (and admin-editable) is which role a given user/project
    /// membership has, not what each role means.
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>Every system-wide role's permission set, for the read-only Admin Roles &amp;
        /// Permissions matrix view.</summary>
        IReadOnlyDictionary<UserRole, IReadOnlySet<string>> SystemMatrix { get; }

        /// <summary>Every project role's permission set, same purpose as SystemMatrix but for the
        /// Manager/Member/Viewer project-role legend.</summary>
        IReadOnlyDictionary<ProjectRole, IReadOnlySet<string>> ProjectMatrix { get; }

        /// <summary>The caller's own system-level permissions — Administrator gets everything;
        /// everyone else gets a small fixed set (see SystemMatrix) plus whatever the "allow users
        /// to create projects" system setting currently permits.</summary>
        Task<IReadOnlySet<string>> GetSystemPermissionsAsync(UserRole role);

        /// <summary>The caller's effective permissions *within a specific project* — combines
        /// Administrator/owner bypass, the caller's ProjectMember.Role via ProjectMatrix, and the
        /// same "allow members to..." system settings ProjectAccessService's callers already
        /// apply, so this never drifts out of sync with what the backend actually enforces.
        /// Empty set (never null) if the caller has no access to the project at all.</summary>
        Task<IReadOnlySet<string>> GetProjectPermissionsAsync(Guid projectId, Guid projectOwnerId, Guid userId, UserRole userRole);
    }

    public class PermissionService(AppDbContext db, IProjectAccessService projectAccess, ISystemSettingsService systemSettings) : IPermissionService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;

        public IReadOnlyDictionary<UserRole, IReadOnlySet<string>> SystemMatrix { get; } = new Dictionary<UserRole, IReadOnlySet<string>>
        {
            [UserRole.Administrator] = new HashSet<string>(Permissions.All),
            [UserRole.Manager] = new HashSet<string> { Permissions.ProjectsView, Permissions.ProjectsCreate },
            [UserRole.Member] = new HashSet<string> { Permissions.ProjectsView, Permissions.ProjectsCreate },
            [UserRole.Viewer] = new HashSet<string> { Permissions.ProjectsView }
        };

        public IReadOnlyDictionary<ProjectRole, IReadOnlySet<string>> ProjectMatrix { get; } = new Dictionary<ProjectRole, IReadOnlySet<string>>
        {
            [ProjectRole.Manager] = new HashSet<string>
            {
                Permissions.ProjectsView, Permissions.ProjectsEdit, Permissions.ProjectsManageMembers,
                Permissions.TasksView, Permissions.TasksCreate, Permissions.TasksEdit, Permissions.TasksDelete, Permissions.TasksAssign,
                Permissions.CommentsCreate, Permissions.CommentsDelete,
                Permissions.FilesUpload, Permissions.FilesDelete,
                Permissions.DependenciesManage, Permissions.RecurringTasksManage,
                Permissions.CustomFieldsManage,
                Permissions.AutomationsView, Permissions.AutomationsCreate, Permissions.AutomationsEdit,
                Permissions.AutomationsDelete, Permissions.AutomationsExecute
            },
            [ProjectRole.Member] = new HashSet<string>
            {
                Permissions.ProjectsView,
                Permissions.TasksView, Permissions.TasksCreate, Permissions.TasksEdit, Permissions.TasksAssign,
                Permissions.CommentsCreate,
                Permissions.FilesUpload,
                Permissions.DependenciesManage, Permissions.RecurringTasksManage,
                Permissions.AutomationsView
            },
            [ProjectRole.Viewer] = new HashSet<string>
            {
                Permissions.ProjectsView,
                Permissions.TasksView,
                Permissions.AutomationsView
            }
        };

        public async Task<IReadOnlySet<string>> GetSystemPermissionsAsync(UserRole role)
        {
            var permissions = new HashSet<string>(SystemMatrix[role]);

            if (role != UserRole.Administrator && role != UserRole.Viewer &&
                !await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowUsersToCreateProjects))
            {
                permissions.Remove(Permissions.ProjectsCreate);
            }

            return permissions;
        }

        public async Task<IReadOnlySet<string>> GetProjectPermissionsAsync(Guid projectId, Guid projectOwnerId, Guid userId, UserRole userRole)
        {
            if (userRole == UserRole.Administrator || userId == projectOwnerId)
            {
                return new HashSet<string>(Permissions.All);
            }

            var role = await _projectAccess.GetProjectRoleAsync(projectId, userId);
            if (role is not ProjectRole projectRole)
            {
                return new HashSet<string>();
            }

            var permissions = new HashSet<string>(ProjectMatrix[projectRole]);

            // Layer the same dynamic Behavior settings TaskService/CommentService/AttachmentService/
            // CustomFieldService already apply on top of the static matrix, so the permission set
            // this reports never disagrees with what a real request would actually be allowed to do.
            if (projectRole == ProjectRole.Member)
            {
                if (!await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowMembersToCreateTasks))
                {
                    permissions.Remove(Permissions.TasksCreate);
                }
                if (await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowMembersToDeleteTasks))
                {
                    permissions.Add(Permissions.TasksDelete);
                }
                if (await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowUsersToCreateCustomFields))
                {
                    permissions.Add(Permissions.CustomFieldsManage);
                }
            }

            if (projectRole is ProjectRole.Manager or ProjectRole.Member)
            {
                if (!await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowComments))
                {
                    permissions.Remove(Permissions.CommentsCreate);
                }
                if (!await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowAttachments))
                {
                    permissions.Remove(Permissions.FilesUpload);
                }
            }

            return permissions;
        }
    }
}

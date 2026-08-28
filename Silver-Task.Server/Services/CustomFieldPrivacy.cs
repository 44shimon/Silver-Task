using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>Phase 41 — the single rule for "can this caller see a private field's VALUE."
    /// Pure/static (no DB access) so both TaskService and ProjectService can call it identically
    /// right before returning a DTO-bound entity, redacting (removing, not just masking) any
    /// CustomValue that belongs to a private field the caller isn't allowed to see. Never a
    /// frontend-only hide — the value must never leave the server for an unauthorized caller
    /// (spec #38's own explicit "not merely hidden by CSS" requirement).</summary>
    public static class CustomFieldPrivacy
    {
        public static bool CanSeeValue(CustomField field, Guid callerId, UserRole callerRole, Guid projectOwnerId, ProjectRole? callerProjectRole)
        {
            if (!field.IsPrivate)
            {
                return true;
            }

            if (callerRole == UserRole.Administrator || callerId == projectOwnerId || callerProjectRole == ProjectRole.Manager)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(field.VisibleToRoles))
            {
                var allowedRoles = field.VisibleToRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowedRoles.Contains(callerRole.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Removes (mutates in place) every TaskCustomValue whose field is private and
        /// not visible to this caller. Safe to call on an entity that's about to be serialized to
        /// a DTO and then discarded — never persisted, since the values still exist in the
        /// database for authorized callers. Takes projectOwnerId explicitly rather than reading
        /// task.Project.OwnerId — several of TaskService's read queries don't bother Including
        /// Project (they already resolve/validate the project separately), so requiring the
        /// navigation to be loaded here would be an easy-to-miss crash/security gap.</summary>
        public static void RedactTaskValues(TaskItem task, Guid callerId, UserRole callerRole, Guid projectOwnerId, ProjectRole? callerProjectRole)
        {
            if (task.CustomValues.Count == 0)
            {
                return;
            }

            var toRemove = task.CustomValues
                .Where(v => v.CustomField is null || !CanSeeValue(v.CustomField, callerId, callerRole, projectOwnerId, callerProjectRole))
                .ToList();

            foreach (var value in toRemove)
            {
                task.CustomValues.Remove(value);
            }
        }

        public static void RedactProjectValues(Project project, Guid callerId, UserRole callerRole, ProjectRole? callerProjectRole)
        {
            if (project.CustomValues.Count == 0)
            {
                return;
            }

            var toRemove = project.CustomValues
                .Where(v => v.CustomField is null || !CanSeeValue(v.CustomField, callerId, callerRole, project.OwnerId, callerProjectRole))
                .ToList();

            foreach (var value in toRemove)
            {
                project.CustomValues.Remove(value);
            }
        }
    }
}

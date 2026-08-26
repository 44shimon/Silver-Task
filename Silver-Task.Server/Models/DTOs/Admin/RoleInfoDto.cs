namespace Silver_Task.Server.Models.DTOs.Admin
{
    /// <summary>One row of the read-only Admin -> Roles & Permissions matrix — a role's fixed
    /// permission set plus how many users/memberships currently have it. See
    /// PermissionService's own doc comment for why this is fixed/code-defined rather than a
    /// database-editable "create role" system.</summary>
    public class RoleInfoDto
    {
        public required string Name { get; set; }

        public List<string> Permissions { get; set; } = [];

        public int UserCount { get; set; }
    }
}

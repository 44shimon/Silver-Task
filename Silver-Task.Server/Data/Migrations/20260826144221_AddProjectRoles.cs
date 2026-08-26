using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ProjectMembers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Member");

            // Backfill: preserve current behavior exactly at migration time. Before Phase 32,
            // "can manage this project" meant Administrator, the project owner, or a member whose
            // *system-wide* UserRole was Manager — so the project owner's own membership row, and
            // every existing member whose global role is Manager, become that project's Manager;
            // everyone else becomes a plain Member. Going forward the two concepts (system role,
            // project role) are independently editable, but no existing membership's *current*
            // effective access changes the moment this migration runs.
            migrationBuilder.Sql(
                """
                UPDATE "ProjectMembers" pm
                SET "Role" = CASE
                    WHEN pm."UserId" = p."OwnerId" THEN 'Manager'
                    WHEN u."Role" = 'Manager' THEN 'Manager'
                    ELSE 'Member'
                END
                FROM "Projects" p, "Users" u
                WHERE pm."ProjectId" = p."Id" AND pm."UserId" = u."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "ProjectMembers");
        }
    }
}

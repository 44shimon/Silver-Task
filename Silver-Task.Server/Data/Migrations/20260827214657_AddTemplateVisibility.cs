using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "TaskTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "ProjectTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "TaskTemplates");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "ProjectTemplates");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDependencyTypesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskDependencies_TaskId_DependsOnTaskId",
                table: "TaskDependencies");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_TaskId_DependsOnTaskId_DependencyType",
                table: "TaskDependencies",
                columns: new[] { "TaskId", "DependsOnTaskId", "DependencyType" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskDependencies_NoSelfDependency",
                table: "TaskDependencies",
                sql: "\"TaskId\" != \"DependsOnTaskId\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskDependencies_TaskId_DependsOnTaskId_DependencyType",
                table: "TaskDependencies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskDependencies_NoSelfDependency",
                table: "TaskDependencies");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_TaskId_DependsOnTaskId",
                table: "TaskDependencies",
                columns: new[] { "TaskId", "DependsOnTaskId" },
                unique: true);
        }
    }
}

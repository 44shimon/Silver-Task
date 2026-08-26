using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OccurrenceNumber",
                table: "Tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecurrenceOccurrenceDate",
                table: "Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringTaskId",
                table: "Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeek = table.Column<int>(type: "integer", nullable: false),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    MonthOfYear = table.Column<int>(type: "integer", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxOccurrences = table.Column<int>(type: "integer", nullable: true),
                    OccurrencesGenerated = table.Column<int>(type: "integer", nullable: false),
                    NextOccurrenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringTasks_Tasks_ParentTaskId",
                        column: x => x.ParentTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecurringTasks_Tasks_TemplateTaskId",
                        column: x => x.TemplateTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecurringTasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecurringTasks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringTaskExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExceptionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTaskExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringTaskExceptions_RecurringTasks_RecurringTaskId",
                        column: x => x.RecurringTaskId,
                        principalTable: "RecurringTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_RecurringTaskId_RecurrenceOccurrenceDate",
                table: "Tasks",
                columns: new[] { "RecurringTaskId", "RecurrenceOccurrenceDate" },
                unique: true,
                filter: "\"RecurringTaskId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskExceptions_RecurringTaskId_OccurrenceDate",
                table: "RecurringTaskExceptions",
                columns: new[] { "RecurringTaskId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTasks_AssignedToUserId",
                table: "RecurringTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTasks_CreatedByUserId",
                table: "RecurringTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTasks_IsActive_NextOccurrenceDate",
                table: "RecurringTasks",
                columns: new[] { "IsActive", "NextOccurrenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTasks_ParentTaskId",
                table: "RecurringTasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTasks_ProjectId",
                table: "RecurringTasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTasks_TemplateTaskId",
                table: "RecurringTasks",
                column: "TemplateTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_RecurringTasks_RecurringTaskId",
                table: "Tasks",
                column: "RecurringTaskId",
                principalTable: "RecurringTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_RecurringTasks_RecurringTaskId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "RecurringTaskExceptions");

            migrationBuilder.DropTable(
                name: "RecurringTasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_RecurringTaskId_RecurrenceOccurrenceDate",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OccurrenceNumber",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "RecurrenceOccurrenceDate",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "RecurringTaskId",
                table: "Tasks");
        }
    }
}

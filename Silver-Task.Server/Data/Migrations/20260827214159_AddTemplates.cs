using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceProjectTemplateId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceTemplateSnapshotAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsChecked = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskChecklistItems_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    DueOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    EstimatedDurationDays = table.Column<int>(type: "integer", nullable: true),
                    AssignmentMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTemplates_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplateTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTemplateTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    DueOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    EstimatedDurationDays = table.Column<int>(type: "integer", nullable: true),
                    AssignmentMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplateTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTasks_ProjectTemplateTasks_ParentTemplateTas~",
                        column: x => x.ParentTemplateTaskId,
                        principalTable: "ProjectTemplateTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTasks_ProjectTemplates_ProjectTemplateId",
                        column: x => x.ProjectTemplateId,
                        principalTable: "ProjectTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TaskTemplateChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTemplateChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTemplateChecklistItems_TaskTemplates_TaskTemplateId",
                        column: x => x.TaskTemplateId,
                        principalTable: "TaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskTemplateCustomValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTemplateCustomValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTemplateCustomValues_CustomFields_CustomFieldId",
                        column: x => x.CustomFieldId,
                        principalTable: "CustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskTemplateCustomValues_TaskTemplates_TaskTemplateId",
                        column: x => x.TaskTemplateId,
                        principalTable: "TaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskTemplateTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTemplateTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTemplateTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskTemplateTags_TaskTemplates_TaskTemplateId",
                        column: x => x.TaskTemplateId,
                        principalTable: "TaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedWithUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateShares", x => x.Id);
                    table.CheckConstraint("CK_TemplateShares_ExactlyOneParent", "(CASE WHEN \"ProjectTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"TaskTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_TemplateShares_ProjectTemplates_ProjectTemplateId",
                        column: x => x.ProjectTemplateId,
                        principalTable: "ProjectTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemplateShares_TaskTemplates_TaskTemplateId",
                        column: x => x.TaskTemplateId,
                        principalTable: "TaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemplateShares_Users_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTemplateFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTemplateFavorites", x => x.Id);
                    table.CheckConstraint("CK_UserTemplateFavorites_ExactlyOneParent", "(CASE WHEN \"ProjectTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"TaskTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_UserTemplateFavorites_ProjectTemplates_ProjectTemplateId",
                        column: x => x.ProjectTemplateId,
                        principalTable: "ProjectTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTemplateFavorites_TaskTemplates_TaskTemplateId",
                        column: x => x.TaskTemplateId,
                        principalTable: "TaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTemplateFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplateTaskChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplateTaskChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskChecklistItems_ProjectTemplateTasks_Proj~",
                        column: x => x.ProjectTemplateTaskId,
                        principalTable: "ProjectTemplateTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplateTaskCustomValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplateTaskCustomValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskCustomValues_CustomFields_CustomFieldId",
                        column: x => x.CustomFieldId,
                        principalTable: "CustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskCustomValues_ProjectTemplateTasks_Projec~",
                        column: x => x.ProjectTemplateTaskId,
                        principalTable: "ProjectTemplateTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplateTaskDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnTemplateTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependencyType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplateTaskDependencies", x => x.Id);
                    table.CheckConstraint("CK_ProjectTemplateTaskDependencies_NoSelfDependency", "\"TemplateTaskId\" != \"DependsOnTemplateTaskId\"");
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskDependencies_ProjectTemplateTasks_Depend~",
                        column: x => x.DependsOnTemplateTaskId,
                        principalTable: "ProjectTemplateTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskDependencies_ProjectTemplateTasks_Templa~",
                        column: x => x.TemplateTaskId,
                        principalTable: "ProjectTemplateTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskDependencies_ProjectTemplates_ProjectTem~",
                        column: x => x.ProjectTemplateId,
                        principalTable: "ProjectTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplateTaskTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplateTaskTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskTags_ProjectTemplateTasks_ProjectTemplat~",
                        column: x => x.ProjectTemplateTaskId,
                        principalTable: "ProjectTemplateTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateTaskTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_SourceProjectTemplateId",
                table: "Projects",
                column: "SourceProjectTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_CreatedByUserId",
                table: "ProjectTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_IsArchived",
                table: "ProjectTemplates",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskChecklistItems_ProjectTemplateTaskId",
                table: "ProjectTemplateTaskChecklistItems",
                column: "ProjectTemplateTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskCustomValues_CustomFieldId",
                table: "ProjectTemplateTaskCustomValues",
                column: "CustomFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskCustomValues_ProjectTemplateTaskId_Custo~",
                table: "ProjectTemplateTaskCustomValues",
                columns: new[] { "ProjectTemplateTaskId", "CustomFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskDependencies_DependsOnTemplateTaskId",
                table: "ProjectTemplateTaskDependencies",
                column: "DependsOnTemplateTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskDependencies_ProjectTemplateId",
                table: "ProjectTemplateTaskDependencies",
                column: "ProjectTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskDependencies_TemplateTaskId_DependsOnTem~",
                table: "ProjectTemplateTaskDependencies",
                columns: new[] { "TemplateTaskId", "DependsOnTemplateTaskId", "DependencyType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTasks_AssignedToUserId",
                table: "ProjectTemplateTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTasks_ParentTemplateTaskId",
                table: "ProjectTemplateTasks",
                column: "ParentTemplateTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTasks_ProjectTemplateId",
                table: "ProjectTemplateTasks",
                column: "ProjectTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskTags_ProjectTemplateTaskId_TagId",
                table: "ProjectTemplateTaskTags",
                columns: new[] { "ProjectTemplateTaskId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateTaskTags_TagId",
                table: "ProjectTemplateTaskTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChecklistItems_TaskId",
                table: "TaskChecklistItems",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplateChecklistItems_TaskTemplateId",
                table: "TaskTemplateChecklistItems",
                column: "TaskTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplateCustomValues_CustomFieldId",
                table: "TaskTemplateCustomValues",
                column: "CustomFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplateCustomValues_TaskTemplateId_CustomFieldId",
                table: "TaskTemplateCustomValues",
                columns: new[] { "TaskTemplateId", "CustomFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_AssignedToUserId",
                table: "TaskTemplates",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_CreatedByUserId",
                table: "TaskTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_IsArchived",
                table: "TaskTemplates",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplateTags_TagId",
                table: "TaskTemplateTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplateTags_TaskTemplateId_TagId",
                table: "TaskTemplateTags",
                columns: new[] { "TaskTemplateId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateShares_ProjectTemplateId_SharedWithUserId",
                table: "TemplateShares",
                columns: new[] { "ProjectTemplateId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateShares_SharedWithUserId",
                table: "TemplateShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateShares_TaskTemplateId_SharedWithUserId",
                table: "TemplateShares",
                columns: new[] { "TaskTemplateId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTemplateFavorites_ProjectTemplateId",
                table: "UserTemplateFavorites",
                column: "ProjectTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTemplateFavorites_TaskTemplateId",
                table: "UserTemplateFavorites",
                column: "TaskTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTemplateFavorites_UserId_ProjectTemplateId",
                table: "UserTemplateFavorites",
                columns: new[] { "UserId", "ProjectTemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTemplateFavorites_UserId_TaskTemplateId",
                table: "UserTemplateFavorites",
                columns: new[] { "UserId", "TaskTemplateId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectTemplates_SourceProjectTemplateId",
                table: "Projects",
                column: "SourceProjectTemplateId",
                principalTable: "ProjectTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectTemplates_SourceProjectTemplateId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectTemplateTaskChecklistItems");

            migrationBuilder.DropTable(
                name: "ProjectTemplateTaskCustomValues");

            migrationBuilder.DropTable(
                name: "ProjectTemplateTaskDependencies");

            migrationBuilder.DropTable(
                name: "ProjectTemplateTaskTags");

            migrationBuilder.DropTable(
                name: "TaskChecklistItems");

            migrationBuilder.DropTable(
                name: "TaskTemplateChecklistItems");

            migrationBuilder.DropTable(
                name: "TaskTemplateCustomValues");

            migrationBuilder.DropTable(
                name: "TaskTemplateTags");

            migrationBuilder.DropTable(
                name: "TemplateShares");

            migrationBuilder.DropTable(
                name: "UserTemplateFavorites");

            migrationBuilder.DropTable(
                name: "ProjectTemplateTasks");

            migrationBuilder.DropTable(
                name: "TaskTemplates");

            migrationBuilder.DropTable(
                name: "ProjectTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Projects_SourceProjectTemplateId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SourceProjectTemplateId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SourceTemplateSnapshotAt",
                table: "Projects");
        }
    }
}

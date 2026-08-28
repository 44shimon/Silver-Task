using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFieldScopeAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFields_ProjectId_SortOrder",
                table: "CustomFields");

            migrationBuilder.AddColumn<Guid>(
                name: "ConditionFieldId",
                table: "CustomFields",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionOperator",
                table: "CustomFields",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionValue",
                table: "CustomFields",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecimalPlaces",
                table: "CustomFields",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "CustomFields",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Task");

            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "CustomFields",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier",
                table: "CustomFields",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "CustomFields",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxLength",
                table: "CustomFields",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxValue",
                table: "CustomFields",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinValue",
                table: "CustomFields",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Placeholder",
                table: "CustomFields",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisibleToRoles",
                table: "CustomFields",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectCustomValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCustomValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCustomValues_CustomFields_CustomFieldId",
                        column: x => x.CustomFieldId,
                        principalTable: "CustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCustomValues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_ConditionFieldId",
                table: "CustomFields",
                column: "ConditionFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_EntityType_ProjectId_SortOrder",
                table: "CustomFields",
                columns: new[] { "EntityType", "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_ProjectId",
                table: "CustomFields",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomValues_CustomFieldId",
                table: "ProjectCustomValues",
                column: "CustomFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomValues_ProjectId_CustomFieldId",
                table: "ProjectCustomValues",
                columns: new[] { "ProjectId", "CustomFieldId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomFields_CustomFields_ConditionFieldId",
                table: "CustomFields",
                column: "ConditionFieldId",
                principalTable: "CustomFields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill Identifier for every field that existed before this migration — slugifies
            // Name the same way CustomFieldService.Slugify does (lowercase, non-alphanumeric runs
            // collapsed to a single underscore, trimmed), then disambiguates any within-scope
            // collision by appending its row number, mirroring CustomFieldService's own
            // GenerateIdentifierAsync suffix scheme (_2, _3, ...). Never used for a NEW field
            // going forward — CustomFieldService always generates Identifier at create time.
            migrationBuilder.Sql(@"
                WITH numbered AS (
                    SELECT
                        cf.""Id"" AS field_id,
                        COALESCE(NULLIF(TRIM(BOTH '_' FROM REGEXP_REPLACE(LOWER(TRIM(cf.""Name"")), '[^a-z0-9]+', '_', 'g')), ''), 'field') AS base_slug,
                        ROW_NUMBER() OVER (
                            PARTITION BY cf.""EntityType"", cf.""ProjectId"",
                                COALESCE(NULLIF(TRIM(BOTH '_' FROM REGEXP_REPLACE(LOWER(TRIM(cf.""Name"")), '[^a-z0-9]+', '_', 'g')), ''), 'field')
                            ORDER BY cf.""Id""
                        ) AS rn
                    FROM ""CustomFields"" cf
                )
                UPDATE ""CustomFields""
                SET ""Identifier"" = CASE WHEN numbered.rn = 1 THEN numbered.base_slug ELSE numbered.base_slug || '_' || numbered.rn END
                FROM numbered
                WHERE ""CustomFields"".""Id"" = numbered.field_id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomFields_CustomFields_ConditionFieldId",
                table: "CustomFields");

            migrationBuilder.DropTable(
                name: "ProjectCustomValues");

            migrationBuilder.DropIndex(
                name: "IX_CustomFields_ConditionFieldId",
                table: "CustomFields");

            migrationBuilder.DropIndex(
                name: "IX_CustomFields_EntityType_ProjectId_SortOrder",
                table: "CustomFields");

            migrationBuilder.DropIndex(
                name: "IX_CustomFields_ProjectId",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ConditionFieldId",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ConditionOperator",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ConditionValue",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "DecimalPlaces",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "Identifier",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "MaxLength",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "Placeholder",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "VisibleToRoles",
                table: "CustomFields");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_ProjectId_SortOrder",
                table: "CustomFields",
                columns: new[] { "ProjectId", "SortOrder" });
        }
    }
}

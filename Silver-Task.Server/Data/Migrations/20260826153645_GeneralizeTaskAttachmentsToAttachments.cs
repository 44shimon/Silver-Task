using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeTaskAttachmentsToAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename rather than drop/recreate — TaskAttachments already has real uploaded
            // rows/files from earlier phases and this table gains new nullable-FK
            // (Project/Comment) capability rather than replacing what it already does for Tasks.
            migrationBuilder.RenameTable(
                name: "TaskAttachments",
                newName: "Attachments");

            migrationBuilder.RenameIndex(
                name: "IX_TaskAttachments_TaskId",
                table: "Attachments",
                newName: "IX_Attachments_TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskAttachments_UploadedByUserId",
                table: "Attachments",
                newName: "IX_Attachments_UploadedByUserId");

            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" RENAME CONSTRAINT \"PK_TaskAttachments\" TO \"PK_Attachments\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" RENAME CONSTRAINT \"FK_TaskAttachments_Tasks_TaskId\" TO \"FK_Attachments_Tasks_TaskId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" RENAME CONSTRAINT \"FK_TaskAttachments_Users_UploadedByUserId\" TO \"FK_Attachments_Users_UploadedByUserId\";");

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskId",
                table: "Attachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommentId",
                table: "Attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Attachments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Attachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Attachments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "timezone('utc', now())");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Attachments_ExactlyOneParent",
                table: "Attachments",
                sql: "(CASE WHEN \"ProjectId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"TaskId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"CommentId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Projects_ProjectId",
                table: "Attachments",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_TaskComments_CommentId",
                table: "Attachments",
                column: "CommentId",
                principalTable: "TaskComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Users_DeletedByUserId",
                table: "Attachments",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CommentId",
                table: "Attachments",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_DeletedByUserId",
                table: "Attachments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ProjectId",
                table: "Attachments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ProjectId_IsDeleted",
                table: "Attachments",
                columns: new[] { "ProjectId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TaskId_IsDeleted",
                table: "Attachments",
                columns: new[] { "TaskId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Projects_ProjectId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_TaskComments_CommentId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Users_DeletedByUserId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_CommentId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_DeletedByUserId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_ProjectId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_ProjectId_IsDeleted",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_TaskId_IsDeleted",
                table: "Attachments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Attachments_ExactlyOneParent",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "CommentId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Attachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskId",
                table: "Attachments",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" RENAME CONSTRAINT \"FK_Attachments_Tasks_TaskId\" TO \"FK_TaskAttachments_Tasks_TaskId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" RENAME CONSTRAINT \"FK_Attachments_Users_UploadedByUserId\" TO \"FK_TaskAttachments_Users_UploadedByUserId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" RENAME CONSTRAINT \"PK_Attachments\" TO \"PK_TaskAttachments\";");

            migrationBuilder.RenameIndex(
                name: "IX_Attachments_TaskId",
                table: "Attachments",
                newName: "IX_TaskAttachments_TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_Attachments_UploadedByUserId",
                table: "Attachments",
                newName: "IX_TaskAttachments_UploadedByUserId");

            migrationBuilder.RenameTable(
                name: "Attachments",
                newName: "TaskAttachments");
        }
    }
}

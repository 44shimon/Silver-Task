using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsEnabled",
                table: "UserNotificationSettings",
                newName: "InAppEnabled");

            migrationBuilder.AddColumn<string>(
                name: "DigestFrequency",
                table: "UserPreferences",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Immediately");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDigestSentAt",
                table: "UserPreferences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuietHoursEnabled",
                table: "UserPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietHoursEnd",
                table: "UserPreferences",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietHoursStart",
                table: "UserPreferences",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailEnabled",
                table: "UserNotificationSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                table: "Notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommentId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FileId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ActorUserId",
                table: "Notifications",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CommentId",
                table: "Notifications",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_FileId",
                table: "Notifications",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Type",
                table: "Notifications",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Type_EventId",
                table: "Notifications",
                columns: new[] { "UserId", "Type", "EventId" },
                unique: true,
                filter: "\"EventId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Attachments_FileId",
                table: "Notifications",
                column: "FileId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_TaskComments_CommentId",
                table: "Notifications",
                column: "CommentId",
                principalTable: "TaskComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_ActorUserId",
                table: "Notifications",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Attachments_FileId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_TaskComments_CommentId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_ActorUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ActorUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CommentId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_FileId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Type",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Type_EventId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DigestFrequency",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "LastDigestSentAt",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "QuietHoursEnabled",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "QuietHoursEnd",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "QuietHoursStart",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "EmailEnabled",
                table: "UserNotificationSettings");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CommentId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "InAppEnabled",
                table: "UserNotificationSettings",
                newName: "IsEnabled");
        }
    }
}

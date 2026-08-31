using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailDeliveryAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationsEnabled",
                table: "UserPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "EmailDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActionUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmailDeliveries_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmailDeliveries_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HeadingTemplate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BodyTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CtaText = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FooterTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplates_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_ActorUserId",
                table: "EmailDeliveries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_NotificationId",
                table: "EmailDeliveries",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_RecipientUserId",
                table: "EmailDeliveries",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_Status_NextAttemptAt",
                table: "EmailDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_NotificationType",
                table: "EmailTemplates",
                column: "NotificationType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_UpdatedByUserId",
                table: "EmailTemplates",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailDeliveries");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "EmailNotificationsEnabled",
                table: "UserPreferences");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    FilterJson = table.Column<string>(type: "text", nullable: false),
                    Columns = table.Column<string>(type: "text", nullable: true),
                    SortField = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortDescending = table.Column<bool>(type: "boolean", nullable: false),
                    GroupByField = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Layout = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViews_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSavedViewFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSavedViewFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSavedViewFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedViewShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViewShares_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedViewShares_Users_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_CreatedByUserId",
                table: "SavedViews",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewShares_SavedViewId_SharedWithUserId",
                table: "SavedViewShares",
                columns: new[] { "SavedViewId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewShares_SharedWithUserId",
                table: "SavedViewShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedViewFavorites_UserId_SavedViewId",
                table: "UserSavedViewFavorites",
                columns: new[] { "UserId", "SavedViewId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedViewShares");

            migrationBuilder.DropTable(
                name: "UserSavedViewFavorites");

            migrationBuilder.DropTable(
                name: "SavedViews");
        }
    }
}

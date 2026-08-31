using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Silver_Task.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDigestScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- UserPreferences: digest scheduling ---
            // DigestFrequency (global Immediately/Daily/Never) is fully superseded by the new
            // per-notification-type EmailDeliveryMode below — not preserved, per the Phase 46
            // plan's own "replace, don't duplicate, the digest mechanism" decision. LastDigestSentAt
            // tracked the OLD daily-only sweep; renaming it to either new column would misrepresent
            // its meaning (it was never a weekly timestamp, and the new Daily bookmark deserves a
            // clean start under the new per-type semantics), so it's dropped outright — every
            // existing user's first Phase 46 digest simply covers the last 24h/7d, never a
            // duplicate (see DigestGenerationService's own doc comment on the null-LastDigestAt
            // fallback).
            migrationBuilder.DropColumn(name: "DigestFrequency", table: "UserPreferences");
            migrationBuilder.DropColumn(name: "LastDigestSentAt", table: "UserPreferences");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DailyDigestTime", table: "UserPreferences",
                type: "time without time zone", nullable: false, defaultValue: new TimeOnly(8, 0, 0));
            migrationBuilder.AddColumn<string>(
                name: "WeeklyDigestDay", table: "UserPreferences",
                type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Monday");
            migrationBuilder.AddColumn<TimeOnly>(
                name: "WeeklyDigestTime", table: "UserPreferences",
                type: "time without time zone", nullable: false, defaultValue: new TimeOnly(8, 0, 0));
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDailyDigestAt", table: "UserPreferences",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "LastWeeklyDigestAt", table: "UserPreferences",
                type: "timestamp with time zone", nullable: true);

            // --- UserNotificationSettings: EmailEnabled (bool) -> EmailDeliveryMode (string) ---
            // Added with a real default ("Immediately") rather than empty string, then backfilled
            // from the still-present EmailEnabled column BEFORE it's dropped (the generated
            // migration had this backwards — dropping EmailEnabled first would make the backfill
            // impossible). true -> "Immediately", false -> "Off": the simplest mapping that
            // guarantees nobody who was getting email stops, and nobody who wasn't starts (spec's
            // own "existing users must receive safe default values, do not break existing
            // preferences" requirement) — see Common.NotificationDeliveryModes.
            migrationBuilder.AddColumn<string>(
                name: "EmailDeliveryMode", table: "UserNotificationSettings",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Immediately");

            migrationBuilder.Sql("""
                UPDATE "UserNotificationSettings"
                SET "EmailDeliveryMode" = CASE WHEN "EmailEnabled" THEN 'Immediately' ELSE 'Off' END;
                """);

            migrationBuilder.DropColumn(name: "EmailEnabled", table: "UserNotificationSettings");

            // --- EmailDeliveries: pre-rendered digest content (Phase 46) ---
            migrationBuilder.AddColumn<string>(
                name: "RenderedSubject", table: "EmailDeliveries",
                type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "RenderedHtmlBody", table: "EmailDeliveries",
                type: "text", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RenderedSubject", table: "EmailDeliveries");
            migrationBuilder.DropColumn(name: "RenderedHtmlBody", table: "EmailDeliveries");

            migrationBuilder.AddColumn<bool>(
                name: "EmailEnabled", table: "UserNotificationSettings",
                type: "boolean", nullable: false, defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE "UserNotificationSettings"
                SET "EmailEnabled" = ("EmailDeliveryMode" <> 'Off');
                """);

            migrationBuilder.DropColumn(name: "EmailDeliveryMode", table: "UserNotificationSettings");

            migrationBuilder.DropColumn(name: "DailyDigestTime", table: "UserPreferences");
            migrationBuilder.DropColumn(name: "WeeklyDigestDay", table: "UserPreferences");
            migrationBuilder.DropColumn(name: "WeeklyDigestTime", table: "UserPreferences");
            migrationBuilder.DropColumn(name: "LastDailyDigestAt", table: "UserPreferences");
            migrationBuilder.DropColumn(name: "LastWeeklyDigestAt", table: "UserPreferences");

            migrationBuilder.AddColumn<string>(
                name: "DigestFrequency", table: "UserPreferences",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Immediately");
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDigestSentAt", table: "UserPreferences",
                type: "timestamp with time zone", nullable: true);
        }
    }
}

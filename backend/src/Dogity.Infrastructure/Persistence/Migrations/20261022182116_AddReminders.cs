using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReminderSessions",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderTracks",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderTrainerSummary",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainerSummaryDays",
                table: "user_preferences",
                type: "integer",
                nullable: false,
                defaultValue: 64);

            migrationBuilder.AddColumn<int>(
                name: "TrainerSummaryHour",
                table: "user_preferences",
                type: "integer",
                nullable: false,
                defaultValue: 18);

            migrationBuilder.CreateTable(
                name: "sent_reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayKey = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sent_reminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sent_reminders_CreatedAt",
                table: "sent_reminders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_sent_reminders_UserId_Type_ReferenceId_DayKey",
                table: "sent_reminders",
                columns: new[] { "UserId", "Type", "ReferenceId", "DayKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sent_reminders");

            migrationBuilder.DropColumn(
                name: "ReminderSessions",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "ReminderTracks",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "ReminderTrainerSummary",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "TrainerSummaryDays",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "TrainerSummaryHour",
                table: "user_preferences");
        }
    }
}

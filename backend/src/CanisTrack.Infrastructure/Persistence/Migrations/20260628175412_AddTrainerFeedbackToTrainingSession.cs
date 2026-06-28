using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanisTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerFeedbackToTrainingSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FeedbackAt",
                table: "training_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FeedbackByTrainerId",
                table: "training_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainerFeedback",
                table: "training_sessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedbackAt",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "FeedbackByTrainerId",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "TrainerFeedback",
                table: "training_sessions");
        }
    }
}

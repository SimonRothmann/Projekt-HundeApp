using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionResponsesAndFeedbackReply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerReaction",
                table: "training_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerReply",
                table: "training_sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OwnerReplyAt",
                table: "training_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_training_session_responses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupTrainingSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAttending = table.Column<bool>(type: "boolean", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_training_session_responses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_training_session_responses_group_training_sessions_Gr~",
                        column: x => x.GroupTrainingSessionId,
                        principalTable: "group_training_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_training_session_responses_GroupTrainingSessionId_Use~",
                table: "group_training_session_responses",
                columns: new[] { "GroupTrainingSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_training_session_responses_UserId",
                table: "group_training_session_responses",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_training_session_responses");

            migrationBuilder.DropColumn(
                name: "OwnerReaction",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "OwnerReply",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "OwnerReplyAt",
                table: "training_sessions");
        }
    }
}

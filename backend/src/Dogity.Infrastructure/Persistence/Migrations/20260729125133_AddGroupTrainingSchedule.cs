using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupTrainingSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group_training_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_training_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_training_sessions_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_training_sessions_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_training_session_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupTrainingSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupTrainingExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FreeText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_training_session_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_training_session_items_group_training_exercises_Group~",
                        column: x => x.GroupTrainingExerciseId,
                        principalTable: "group_training_exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_training_session_items_group_training_sessions_GroupT~",
                        column: x => x.GroupTrainingSessionId,
                        principalTable: "group_training_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_training_session_trainers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupTrainingSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_training_session_trainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_training_session_trainers_group_training_sessions_Gro~",
                        column: x => x.GroupTrainingSessionId,
                        principalTable: "group_training_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_training_session_items_GroupTrainingExerciseId",
                table: "group_training_session_items",
                column: "GroupTrainingExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_group_training_session_items_GroupTrainingSessionId",
                table: "group_training_session_items",
                column: "GroupTrainingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_group_training_session_trainers_GroupTrainingSessionId_User~",
                table: "group_training_session_trainers",
                columns: new[] { "GroupTrainingSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_training_session_trainers_UserId",
                table: "group_training_session_trainers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_group_training_sessions_ClubId_StartsAt",
                table: "group_training_sessions",
                columns: new[] { "ClubId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_group_training_sessions_GroupId_StartsAt",
                table: "group_training_sessions",
                columns: new[] { "GroupId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_training_session_items");

            migrationBuilder.DropTable(
                name: "group_training_session_trainers");

            migrationBuilder.DropTable(
                name: "group_training_sessions");
        }
    }
}

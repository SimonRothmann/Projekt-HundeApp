using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubTrainerAndExerciseClubScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClubId",
                table: "exercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "club_trainers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_trainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_trainers_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercises_ClubId",
                table: "exercises",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_club_trainers_ClubId_UserId",
                table: "club_trainers",
                columns: new[] { "ClubId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_club_trainers_UserId",
                table: "club_trainers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club_trainers");

            migrationBuilder.DropIndex(
                name: "IX_exercises_ClubId",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "ClubId",
                table: "exercises");
        }
    }
}

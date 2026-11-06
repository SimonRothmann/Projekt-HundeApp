using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubWorkGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GuestId",
                table: "club_work_participations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "club_work_guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_work_guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_work_guests_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_club_work_participations_ClubWorkEventId_GuestId",
                table: "club_work_participations",
                columns: new[] { "ClubWorkEventId", "GuestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_club_work_participations_GuestId",
                table: "club_work_participations",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_club_work_guests_ClubId",
                table: "club_work_guests",
                column: "ClubId");

            migrationBuilder.AddForeignKey(
                name: "FK_club_work_participations_club_work_guests_GuestId",
                table: "club_work_participations",
                column: "GuestId",
                principalTable: "club_work_guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_club_work_participations_club_work_guests_GuestId",
                table: "club_work_participations");

            migrationBuilder.DropTable(
                name: "club_work_guests");

            migrationBuilder.DropIndex(
                name: "IX_club_work_participations_ClubWorkEventId_GuestId",
                table: "club_work_participations");

            migrationBuilder.DropIndex(
                name: "IX_club_work_participations_GuestId",
                table: "club_work_participations");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "club_work_participations");
        }
    }
}

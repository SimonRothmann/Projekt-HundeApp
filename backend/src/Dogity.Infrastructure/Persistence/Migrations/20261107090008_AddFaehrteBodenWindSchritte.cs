using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaehrteBodenWindSchritte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StepLengthCm",
                table: "user_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroundCondition",
                table: "gps_tracks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LaidWindDirectionDeg",
                table: "gps_tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SearchWindDirectionDeg",
                table: "gps_tracks",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StepLengthCm",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "GroundCondition",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "LaidWindDirectionDeg",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "SearchWindDirectionDeg",
                table: "gps_tracks");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWetterStehzeit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "StandRegenMm",
                table: "gps_tracks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StandTempMaxC",
                table: "gps_tracks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StandTempMinC",
                table: "gps_tracks",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandRegenMm",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "StandTempMaxC",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "StandTempMinC",
                table: "gps_tracks");
        }
    }
}

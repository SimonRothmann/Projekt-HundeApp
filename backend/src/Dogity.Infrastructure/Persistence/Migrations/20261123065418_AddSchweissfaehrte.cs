using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchweissfaehrte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SchweissMl",
                table: "gps_tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StehzeitStunden",
                table: "gps_tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Verfahren",
                table: "gps_tracks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Wildart",
                table: "gps_tracks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchweissMl",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "StehzeitStunden",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "Verfahren",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "Wildart",
                table: "gps_tracks");
        }
    }
}

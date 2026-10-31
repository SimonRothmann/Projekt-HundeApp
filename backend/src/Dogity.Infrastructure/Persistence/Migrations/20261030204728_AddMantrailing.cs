using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMantrailing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Indication",
                table: "gps_walk_runs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PersonFound",
                table: "gps_walk_runs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DoubleBlind",
                table: "gps_tracks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "gps_tracks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Faehrte");

            migrationBuilder.AddColumn<bool>(
                name: "NegativeTrail",
                table: "gps_tracks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScentArticle",
                table: "gps_tracks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Indication",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "PersonFound",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "DoubleBlind",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "NegativeTrail",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "ScentArticle",
                table: "gps_tracks");
        }
    }
}

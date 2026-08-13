using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArticlesFound",
                table: "gps_walk_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArticlesTotal",
                table: "gps_walk_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AvgDeviationMeters",
                table: "gps_walk_runs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EvaluatedAt",
                table: "gps_walk_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxDeviationMeters",
                table: "gps_walk_runs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OnTrackPercent",
                table: "gps_walk_runs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeviationMeters",
                table: "gps_walk_points",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarkerType",
                table: "gps_points",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "gps_walk_stops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalkRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MarkerLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_walk_stops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gps_walk_stops_gps_walk_runs_WalkRunId",
                        column: x => x.WalkRunId,
                        principalTable: "gps_walk_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gps_walk_stops_WalkRunId",
                table: "gps_walk_stops",
                column: "WalkRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gps_walk_stops");

            migrationBuilder.DropColumn(
                name: "ArticlesFound",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "ArticlesTotal",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "AvgDeviationMeters",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "EvaluatedAt",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "MaxDeviationMeters",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "OnTrackPercent",
                table: "gps_walk_runs");

            migrationBuilder.DropColumn(
                name: "DeviationMeters",
                table: "gps_walk_points");

            migrationBuilder.DropColumn(
                name: "MarkerType",
                table: "gps_points");
        }
    }
}

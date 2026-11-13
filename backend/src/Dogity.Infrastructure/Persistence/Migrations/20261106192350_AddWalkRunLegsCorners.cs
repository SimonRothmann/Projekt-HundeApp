using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkRunLegsCorners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvaluationVersion",
                table: "gps_walk_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "gps_walk_run_corners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalkRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sharp = table.Column<bool>(type: "boolean", nullable: false),
                    TurnDegrees = table.Column<double>(type: "double precision", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Reached = table.Column<bool>(type: "boolean", nullable: false),
                    OvershootMeters = table.Column<double>(type: "double precision", nullable: true),
                    Overshot = table.Column<bool>(type: "boolean", nullable: false),
                    HoldSeconds = table.Column<int>(type: "integer", nullable: false),
                    TimeAtCornerSeconds = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_walk_run_corners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gps_walk_run_corners_gps_walk_runs_WalkRunId",
                        column: x => x.WalkRunId,
                        principalTable: "gps_walk_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gps_walk_run_legs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalkRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    LengthMeters = table.Column<double>(type: "double precision", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    SpeedMetersPerMinute = table.Column<double>(type: "double precision", nullable: true),
                    AvgDeviationMeters = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_walk_run_legs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gps_walk_run_legs_gps_walk_runs_WalkRunId",
                        column: x => x.WalkRunId,
                        principalTable: "gps_walk_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gps_walk_run_corners_WalkRunId",
                table: "gps_walk_run_corners",
                column: "WalkRunId");

            migrationBuilder.CreateIndex(
                name: "IX_gps_walk_run_legs_WalkRunId",
                table: "gps_walk_run_legs",
                column: "WalkRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gps_walk_run_corners");

            migrationBuilder.DropTable(
                name: "gps_walk_run_legs");

            migrationBuilder.DropColumn(
                name: "EvaluationVersion",
                table: "gps_walk_runs");
        }
    }
}

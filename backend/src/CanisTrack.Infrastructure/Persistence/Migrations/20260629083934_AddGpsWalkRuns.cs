using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanisTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGpsWalkRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gps_walk_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    LengthMeters = table.Column<double>(type: "double precision", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_walk_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gps_walk_runs_gps_tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "gps_tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gps_walk_points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalkRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Accuracy = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_walk_points", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gps_walk_points_gps_walk_runs_WalkRunId",
                        column: x => x.WalkRunId,
                        principalTable: "gps_walk_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gps_walk_points_WalkRunId_Timestamp",
                table: "gps_walk_points",
                columns: new[] { "WalkRunId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_gps_walk_runs_TrackId",
                table: "gps_walk_runs",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gps_walk_points");

            migrationBuilder.DropTable(
                name: "gps_walk_runs");
        }
    }
}

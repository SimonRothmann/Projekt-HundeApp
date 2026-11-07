using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleResultsMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromStats",
                table: "gps_tracks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "gps_points",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gps_walk_run_articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalkRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_walk_run_articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gps_walk_run_articles_gps_walk_runs_WalkRunId",
                        column: x => x.WalkRunId,
                        principalTable: "gps_walk_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gps_walk_run_articles_WalkRunId_PointId",
                table: "gps_walk_run_articles",
                columns: new[] { "WalkRunId", "PointId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gps_walk_run_articles");

            migrationBuilder.DropColumn(
                name: "ExcludeFromStats",
                table: "gps_tracks");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "gps_points");
        }
    }
}

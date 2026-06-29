using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanisTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGpsPointTypeAndLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "gps_points",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointType",
                table: "gps_points",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Label",
                table: "gps_points");

            migrationBuilder.DropColumn(
                name: "PointType",
                table: "gps_points");
        }
    }
}

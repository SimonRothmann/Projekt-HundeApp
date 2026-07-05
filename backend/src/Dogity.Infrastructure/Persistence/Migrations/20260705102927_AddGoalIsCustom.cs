using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalIsCustom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCustom",
                table: "goals",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCustom",
                table: "goals");
        }
    }
}

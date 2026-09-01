using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "quiz_options",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Answer");

            migrationBuilder.AddColumn<string>(
                name: "MatchKey",
                table: "quiz_options",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "quiz_options");

            migrationBuilder.DropColumn(
                name: "MatchKey",
                table: "quiz_options");
        }
    }
}

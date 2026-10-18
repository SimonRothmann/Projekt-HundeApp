using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogEnglishTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "regulations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringNotesEn",
                table: "regulation_exercises",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringCriteriaEn",
                table: "exercises",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "regulations");

            migrationBuilder.DropColumn(
                name: "ScoringNotesEn",
                table: "regulation_exercises");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "ScoringCriteriaEn",
                table: "exercises");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulationSeedKeyAndEditedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EditedAt",
                table: "regulations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeedKey",
                table: "regulations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EditedAt",
                table: "regulation_exercises",
                type: "timestamp with time zone",
                nullable: true);

            // Der Seeder findet seine Ordnungen künftig über SeedKey. Bestehende
            // globale Ordnungen (Sportart ohne Verein) tragen bis jetzt den
            // Seed-Namen - Vereinsordnungen bleiben ohne Schlüssel. Wer vor dieser
            // Migration umbenannt hat, bekommt den heutigen Namen als Schlüssel;
            // der Seeder fällt dann auf den Namen zurück und legt wie bisher neu an.
            migrationBuilder.Sql("""
                UPDATE regulations SET "SeedKey" = "Name"
                WHERE "SportId" IN (SELECT "Id" FROM sports WHERE "ClubId" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "regulations");

            migrationBuilder.DropColumn(
                name: "SeedKey",
                table: "regulations");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "regulation_exercises");
        }
    }
}

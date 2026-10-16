using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogEnglishNamesAndFciScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "sports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "regulations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "exercises",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            // Geltungsbereich richtigstellen (Paket L1, Betreiberentscheidung
            // 2026-10-16). Die FCI-Ordnung "International Utility Dog Regulations
            // (FCI-IGP)" gilt laut ihrem Abschnitt "Validity" für alle FCI-
            // Mitglieder: ihre Prüfungsordnungen gelten überall (CountryCode
            // null). Der CountrySeeder hatte den Altbestand einmalig komplett
            // Deutschland zugeordnet - Hoopers (VDH) war dabei als einzige
            // durchgerutscht und stand auf null.
            //
            // Als Migration und nicht im Seeder, weil sie genau einmal laufen
            // muss: Ein Seeder, der das bei jedem Start setzt, nähme eine
            // spätere bewusste Änderung im Katalog-Editor jedes Mal zurück.
            // Zugeordnet wird über den Sport-Code der Ordnung, nicht über ihren
            // Namen.
            migrationBuilder.Sql("""
                UPDATE regulations SET "CountryCode" = NULL
                WHERE "SportId" IN (
                    SELECT "Id" FROM sports
                    WHERE "ClubId" IS NULL
                      AND "Code" IN ('BH', 'IBGH1', 'IBGH2', 'IBGH3', 'IGP1', 'IGP2', 'IGP3',
                                     'FAERTE', 'FPR', 'UPR', 'SPR', 'GPR', 'STOEPR', 'IAD'));
                """);

            migrationBuilder.Sql("""
                UPDATE regulations SET "CountryCode" = 'DE'
                WHERE "SportId" IN (
                    SELECT "Id" FROM sports
                    WHERE "ClubId" IS NULL
                      AND "Code" IN ('AGILITY', 'THS', 'HOOPERS'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Zustand davor: alles Deutschland, nur Hoopers ohne Land. Das war
            // der Ist-Stand vor L1; die Zuordnung der FCI-Ordnungen davor zurück
            // zu drehen, hieße sie wieder auf "DE" zu setzen.
            migrationBuilder.Sql("""
                UPDATE regulations SET "CountryCode" = 'DE'
                WHERE "CountryCode" IS NULL
                  AND "SportId" IN (
                    SELECT "Id" FROM sports
                    WHERE "ClubId" IS NULL
                      AND "Code" IN ('BH', 'IBGH1', 'IBGH2', 'IBGH3', 'IGP1', 'IGP2', 'IGP3',
                                     'FAERTE', 'FPR', 'UPR', 'SPR', 'GPR', 'STOEPR', 'IAD'));
                """);

            migrationBuilder.Sql("""
                UPDATE regulations SET "CountryCode" = NULL
                WHERE "SportId" IN (SELECT "Id" FROM sports WHERE "ClubId" IS NULL AND "Code" = 'HOOPERS');
                """);

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "sports");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "regulations");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "exercises");
        }
    }
}

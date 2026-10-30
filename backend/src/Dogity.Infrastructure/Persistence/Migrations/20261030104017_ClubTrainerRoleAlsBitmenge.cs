using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClubTrainerRoleAlsBitmenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Die Rolle wird eine Bitmenge (Training = 1, Verwaltung = 2, ...). Die alten Texte
            // gehen über eine Umrechnung statt über eine automatische Umwandlung: Aus
            // "Training" wird 1 und aus "Verwaltung" 2 - nicht 0 und 1, wie die Aufzählung
            // früher zählte. Jede bestehende Zeile bleibt damit eine gültige Rollenmenge;
            // alles Unbekannte fällt auf Training zurück, die schwächste Rolle.
            // Der Text-Standard der Spalte ('') ließe sich nicht in eine Zahl umwandeln - er
            // fällt weg; neue Zeilen bekommen ihre Rolle immer vom Anwendungscode.
            migrationBuilder.Sql("""
                ALTER TABLE club_trainers ALTER COLUMN "Role" DROP DEFAULT;
                ALTER TABLE club_trainers
                    ALTER COLUMN "Role" TYPE integer
                    USING (CASE "Role" WHEN 'Verwaltung' THEN 2 ELSE 1 END);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Verlustbehaftet: Die neuen Rollen kennt der alte Stand nicht. Wer Verwaltung hat,
            // bleibt Verwaltung, alle anderen werden Training.
            migrationBuilder.Sql("""
                ALTER TABLE club_trainers
                    ALTER COLUMN "Role" TYPE character varying(20)
                    USING (CASE WHEN ("Role" & 2) <> 0 THEN 'Verwaltung' ELSE 'Training' END);
                ALTER TABLE club_trainers ALTER COLUMN "Role" SET DEFAULT '';
                """);
        }
    }
}

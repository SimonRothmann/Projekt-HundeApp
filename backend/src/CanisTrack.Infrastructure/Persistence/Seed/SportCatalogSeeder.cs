using CanisTrack.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanisTrack.Infrastructure.Persistence.Seed;

/// <summary>
/// Legt die Start-Sportarten aus PRODUCT_REQUIREMENTS.md MVP-Scope an
/// (BH, IBGH1-3, Fährte) inkl. Übungen mit Bewertungskriterien und
/// Prüfungsordnungen (Regulation/RegulationVersion/RegulationExercise).
///
/// Wichtig: Es werden keine Inhalte von offiziellen Prüfungsordnungen
/// (VDH/Landesverbände) kopiert - diese sind urheberrechtlich geschützt.
/// Die hier hinterlegten Übungsnamen und Bewertungskriterien sind eigene,
/// fachlich an gängige Hundesport-Standards angelehnte Beschreibungen.
/// <see cref="Regulation.SourceUrl"/> kann später von einem Admin auf die
/// offizielle Quelle verweisen, ohne deren Text zu speichern.
///
/// Idempotent auf Ebene einzelner Übungen/Prüfungsordnungen (nicht nur
/// pro Sportart), damit der Katalog auch nach dem ersten Start ergänzt
/// werden kann, ohne Duplikate zu erzeugen.
/// </summary>
public static class SportCatalogSeeder
{
    private sealed record ExerciseSeed(string Name, ExerciseDifficulty Difficulty, string Category, string ScoringCriteria);

    private sealed record RegulationExerciseSeed(string ExerciseName, bool IsMandatory, int MaxPoints, string ScoringNotes);

    private sealed record RegulationSeed(string Name, string VersionLabel, DateOnly ValidFrom, RegulationExerciseSeed[] Exercises);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        var bh = await SeedSportAsync(db, "BH", "Begleithundeprüfung",
        [
            new("Leinenführigkeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund läuft eng und aufmerksam neben dem Hundeführer, auch bei Tempo- und Richtungswechseln, ohne Leinenspannung."),
            new("Sitz aus der Bewegung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund setzt sich auf Kommando sofort und bleibt sitzen, während der Hundeführer ohne Tempoveränderung weitergeht."),
            new("Ablegen mit Abrufen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund legt sich ab und bleibt liegen, kommt auf Kommando zügig und freudig zum Hundeführer."),
            new("Verhalten im Verkehr", ExerciseDifficulty.Intermediate, "Verhalten",
                "Hund bleibt ruhig bei vorbeifahrenden Fahrzeugen und Radfahrern, zeigt keine Anzeichen von Angst oder Aggression."),
            new("Begegnung mit Personengruppe", ExerciseDifficulty.Intermediate, "Verhalten",
                "Hund bleibt ruhig und unaufgeregt beim Passieren einer Gruppe von Personen."),
            new("Verhalten gegenüber anderen Hunden", ExerciseDifficulty.Intermediate, "Verhalten",
                "Hund zeigt keine aggressive oder ängstliche Reaktion beim Begegnen eines fremden Hundes."),
            new("Zurücklassen des Hundes", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund bleibt an der vereinbarten Stelle, bis der Hundeführer zurückkehrt, ohne der Gruppe zu folgen."),
        ]);

        var ibgh1 = await SeedSportAsync(db, "IBGH1", "Internationale Begleithundeprüfung 1",
        [
            new("Fußarbeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund läuft konzentriert in Grundstellung neben dem Hundeführer, auch bei Wendungen und Tempowechseln."),
            new("Abrufen", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund kommt zügig und direkt auf Kommando zum Hundeführer und setzt sich vor diesem."),
            new("Sitz mit Ablenkung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund bleibt sitzen, auch wenn der Hundeführer sich entfernt und Ablenkungen auftreten."),
            new("Bleib in Grundstellung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund verbleibt ruhig in der Grundstellung neben dem Hundeführer ohne Anzeichen von Unruhe."),
        ]);

        var ibgh2 = await SeedSportAsync(db, "IBGH2", "Internationale Begleithundeprüfung 2",
        [
            new("Fußarbeit mit Richtungswechsel", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund folgt zügigen Richtungs- und Tempowechseln des Hundeführers ohne Verzögerung."),
            new("Voraus mit Abliegen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund läuft geradlinig voraus und legt sich auf Kommando sofort ab."),
            new("Apportieren auf der Ebene", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund bringt den Gegenstand zügig, hält ihn ruhig im Fang und übergibt ihn auf Kommando."),
            new("Frontsitz nach Abrufen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund setzt sich nach dem Abrufen gerade und nah vor den Hundeführer."),
        ]);

        var ibgh3 = await SeedSportAsync(db, "IBGH3", "Internationale Begleithundeprüfung 3",
        [
            new("Fußarbeit ohne Leine", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund läuft ohne Leine konzentriert und korrekt in Grundstellung, auch durch eine Personengruppe."),
            new("Gruppe mit Ablenkung", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund bleibt in einer Gruppe ruhig liegen, auch bei Ablenkungen durch andere Hunde oder Personen."),
            new("Voraus mit Hinlegen und Abrufen", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund läuft geradlinig und weit voraus, legt sich ab und kommt anschließend zügig zurück."),
            new("Begleiten ohne Sichtkontakt", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund bleibt auch bei kurzzeitig fehlendem Sichtkontakt zum Hundeführer ruhig in Position."),
        ]);

        var faerte = await SeedSportAsync(db, "FAERTE", "Fährte",
        [
            new("Fährtenaufnahme", ExerciseDifficulty.Beginner, "Fährte",
                "Hund nimmt am Anfangspunkt selbstständig und sicher die Fährte auf und beginnt zügig mit der Ausarbeitung."),
            new("Winkelarbeit", ExerciseDifficulty.Intermediate, "Fährte",
                "Hund arbeitet Winkel sicher und ohne große Bogenbildung aus, ohne die Fährte zu verlieren."),
            new("Gegenstände verweisen", ExerciseDifficulty.Intermediate, "Fährte",
                "Hund zeigt gefundene Gegenstände eindeutig an (verweisen/aufnehmen) und bleibt dabei ruhig."),
            new("Eigenfährte vertiefen", ExerciseDifficulty.Beginner, "Fährte",
                "Hund festigt die Fährtenarbeit auf der eigenen, kurz gelegten Fährte als Vorbereitung auf längere Fährten."),
            new("Fremde Fährte folgen", ExerciseDifficulty.Advanced, "Fährte",
                "Hund nimmt eine von einer fremden Person gelegte Fährte sicher auf und arbeitet sie konzentriert aus."),
        ]);

        await SeedRegulationAsync(db, bh, new RegulationSeed("BH", "2024", new DateOnly(2024, 1, 1),
        [
            new("Leinenführigkeit", true, 0, "Auf Wegen, Plätzen und im Verkehr; keine durchgehende Leinenspannung."),
            new("Sitz aus der Bewegung", true, 0, "Aus normalem Gehen, ohne Geschwindigkeitsänderung des Hundeführers."),
            new("Ablegen mit Abrufen", true, 0, "Hund bleibt liegen, bis er abgerufen wird."),
            new("Verhalten im Verkehr", true, 0, "Begegnung mit mind. einem Fahrzeug und Radfahrer."),
            new("Begegnung mit Personengruppe", true, 0, "Gruppe aus mind. 6 Personen, normales Tempo."),
            new("Verhalten gegenüber anderen Hunden", true, 0, "Begegnung mit einem fremden, angeleinten Hund."),
            new("Zurücklassen des Hundes", true, 0, "Hundeführer entfernt sich außer Sichtweite für ca. 1 Minute."),
        ]));

        await SeedRegulationAsync(db, ibgh1, new RegulationSeed("IBGH1", "2024", new DateOnly(2024, 1, 1),
        [
            new("Fußarbeit", true, 15, "Grundstellung, Wendungen, Tempowechsel (Schritt/Lauf)."),
            new("Abrufen", true, 10, "Aus der Ferne, freudig und ohne Zögern."),
            new("Sitz mit Ablenkung", true, 10, "Hundeführer entfernt sich ca. 15 Schritte."),
            new("Bleib in Grundstellung", true, 5, "Kurze Haltephase ohne Kommandowiederholung."),
        ]));

        await SeedRegulationAsync(db, ibgh2, new RegulationSeed("IBGH2", "2024", new DateOnly(2024, 1, 1),
        [
            new("Fußarbeit mit Richtungswechsel", true, 15, "Inkl. Kehrtwendungen und Tempowechsel ohne Leine."),
            new("Voraus mit Abliegen", true, 15, "Mind. 10 Schritte geradlinig voraus."),
            new("Apportieren auf der Ebene", true, 10, "Gegenstand wird vom Hundeführer geworfen."),
            new("Frontsitz nach Abrufen", true, 10, "Enges, gerades Sitzen vor dem Hundeführer."),
        ]));

        await SeedRegulationAsync(db, ibgh3, new RegulationSeed("IBGH3", "2024", new DateOnly(2024, 1, 1),
        [
            new("Fußarbeit ohne Leine", true, 15, "Durch eine Personengruppe, ohne Leine."),
            new("Gruppe mit Ablenkung", true, 10, "Liegen in der Gruppe für ca. 2 Minuten."),
            new("Voraus mit Hinlegen und Abrufen", true, 15, "Mind. 20 Schritte voraus, danach ablegen."),
            new("Begleiten ohne Sichtkontakt", true, 10, "Kurzzeitiger Sichtverlust durch Hindernis."),
        ]));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("Fährte A", "2024", new DateOnly(2024, 1, 1),
        [
            new("Fährtenaufnahme", true, 0, "Eigene Fährte, ca. 300 Schritte, 3 gerade Schenkel, 2 Winkel, Fährtenalter ca. 20 Minuten."),
            new("Winkelarbeit", true, 0, "2 Winkel auf der Fährte."),
            new("Gegenstände verweisen", true, 0, "2 Gegenstände auf der Fährte."),
        ]));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("Fährte B", "2024", new DateOnly(2024, 1, 1),
        [
            new("Eigenfährte vertiefen", true, 0, "Eigene Fährte, ca. 400 Schritte, 4 Schenkel, Fährtenalter ca. 30 Minuten."),
            new("Winkelarbeit", true, 0, "3 Winkel auf der Fährte."),
            new("Gegenstände verweisen", true, 0, "3 Gegenstände auf der Fährte."),
        ]));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("Fährte C (Fremdfährte)", "2024", new DateOnly(2024, 1, 1),
        [
            new("Fremde Fährte folgen", true, 0, "Fremde Fährte, ca. 600 Schritte, 5 Schenkel, 5 Winkel, Fährtenalter ca. 60 Minuten."),
            new("Winkelarbeit", true, 0, "5 Winkel auf der Fährte."),
            new("Gegenstände verweisen", true, 0, "4 Gegenstände auf der Fährte."),
        ]));

        await db.SaveChangesAsync();
    }

    private static async Task<Sport> SeedSportAsync(ApplicationDbContext db, string code, string name, ExerciseSeed[] exercises)
    {
        var sport = await db.Sports.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Code == code);
        if (sport is null)
        {
            sport = new Sport { Code = code, Name = name };
            db.Sports.Add(sport);
            await db.SaveChangesAsync();
        }

        foreach (var seed in exercises)
        {
            var existing = await db.Exercises.FirstOrDefaultAsync(e => e.SportId == sport.Id && e.Name == seed.Name);
            if (existing is not null)
            {
                // Bewertungskriterien älterer Seed-Durchläufe nachpflegen,
                // ohne von Hand geänderte Inhalte sonst zu berühren.
                if (existing.ScoringCriteria is null)
                    existing.ScoringCriteria = seed.ScoringCriteria;
                continue;
            }

            db.Exercises.Add(new Exercise
            {
                SportId = sport.Id,
                Name = seed.Name,
                Difficulty = seed.Difficulty,
                Category = seed.Category,
                ScoringCriteria = seed.ScoringCriteria
            });
        }

        await db.SaveChangesAsync();
        return sport;
    }

    private static async Task SeedRegulationAsync(ApplicationDbContext db, Sport sport, RegulationSeed seed)
    {
        var regulation = await db.Regulations.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.SportId == sport.Id && r.Name == seed.Name);
        if (regulation is null)
        {
            regulation = new Regulation { SportId = sport.Id, Name = seed.Name };
            db.Regulations.Add(regulation);
            await db.SaveChangesAsync();
        }

        var version = await db.RegulationVersions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.RegulationId == regulation.Id && v.VersionLabel == seed.VersionLabel);
        if (version is null)
        {
            version = new RegulationVersion
            {
                RegulationId = regulation.Id,
                VersionLabel = seed.VersionLabel,
                ValidFrom = seed.ValidFrom
            };
            db.RegulationVersions.Add(version);
            await db.SaveChangesAsync();
        }

        foreach (var exerciseSeed in seed.Exercises)
        {
            var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.SportId == sport.Id && e.Name == exerciseSeed.ExerciseName);
            if (exercise is null)
                continue;

            var exists = await db.RegulationExercises.AnyAsync(re => re.RegulationVersionId == version.Id && re.ExerciseId == exercise.Id);
            if (exists)
                continue;

            db.RegulationExercises.Add(new RegulationExercise
            {
                RegulationVersionId = version.Id,
                ExerciseId = exercise.Id,
                IsMandatory = exerciseSeed.IsMandatory,
                MaxPoints = exerciseSeed.MaxPoints,
                ScoringNotes = exerciseSeed.ScoringNotes
            });
        }

        await db.SaveChangesAsync();
    }
}

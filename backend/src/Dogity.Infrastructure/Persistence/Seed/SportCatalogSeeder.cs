using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Legt die Start-Sportarten aus PRODUCT_REQUIREMENTS.md MVP-Scope an
/// (BH, IBGH1-3, Fährte) inkl. Übungen mit Bewertungskriterien und
/// Prüfungsordnungen (Regulation/RegulationVersion/RegulationExercise).
///
/// Wichtig: Für BH/IBGH1-3/Fährte werden keine Inhalte von offiziellen
/// Prüfungsordnungen (VDH/Landesverbände) kopiert - diese sind
/// urheberrechtlich geschützt. Die dort hinterlegten Übungsnamen und
/// Bewertungskriterien sind eigene, fachlich an gängige
/// Hundesport-Standards angelehnte Beschreibungen.
///
/// Ausnahme IGP1-3: Übungsnamen und Punktzahlen sind direkt der FCI/VDH
/// Prüfungsordnung 2025 (UTI-REG-IGP-de-2025) entnommen. Dies erfolgt mit
/// expliziter Genehmigung des Auftraggebers in seiner Funktion als
/// VDH-Vorstand (siehe TODO.md), abweichend von der sonst im Projekt
/// geltenden Vorsichtsregel. Die Punktaufteilung der Abteilung B/C basiert
/// auf einer manuellen Durchsicht des PDF-Texts; einzelne Werte aus
/// mehrspaltigen Tabellen waren beim Extrahieren nicht zweifelsfrei einer
/// Prüfungsstufe zuzuordnen und sind daher als Näherung zu verstehen -
/// vor Produktiveinsatz durch den Auftraggeber zu prüfen.
///
/// <see cref="Regulation.SourceUrl"/> kann später von einem Admin auf die
/// offizielle Quelle verweisen.
///
/// Idempotent auf Ebene einzelner Übungen/Prüfungsordnungen (nicht nur
/// pro Sportart), damit der Katalog auch nach dem ersten Start ergänzt
/// werden kann, ohne Duplikate zu erzeugen.
/// </summary>
public static class SportCatalogSeeder
{
    private sealed record ExerciseSeed(string Name, ExerciseDifficulty Difficulty, string Category, string ScoringCriteria);

    private sealed record RegulationExerciseSeed(string ExerciseName, bool IsMandatory, int MaxPoints, string ScoringNotes);

    private sealed record RegulationSeed(string Name, string VersionLabel, DateOnly ValidFrom, RegulationExerciseSeed[] Exercises, string? Description = null);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        // Übungsstruktur folgt der tatsächlichen VDH-BH/VT (Teil A: 5 bewertete
        // Übungen à 15/15/10/10/10 = 60 Punkte, bestanden ab 42; Teil B ohne
        // Einzelpunkte). "Freifolge" fehlte in früheren Seed-Durchläufen komplett,
        // "Sitz aus der Bewegung"/"Ablegen mit Abrufen" trugen inoffizielle Namen -
        // mit Genehmigung des Auftraggebers als VDH-Vorstand korrigiert (analog
        // IBGH/IGP, siehe Klassenkommentar oben).
        var bh = await SeedSportAsync(db, "BH", "Begleithundeprüfung",
        [
            new("Leinenführigkeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund läuft eng und aufmerksam neben dem Hundeführer, auch bei Tempo- und Richtungswechseln, ohne Leinenspannung."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie Leinenführigkeit, jedoch ohne Leine - Hund bleibt auch beim Durchschreiten der Personengruppe aufmerksam beim Hundeführer."),
            new("Sitzübung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund setzt sich aus der Bewegung auf ein Hörzeichen sofort hin und bleibt ruhig sitzen, während der Hundeführer sich mindestens 15 Schritte entfernt."),
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund legt sich aus der Bewegung ab, bleibt liegen und kommt auf Hörzeichen zügig und freudig zum Hundeführer."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund bleibt während der Vorführung des anderen Hundes ruhig in der Ablage liegen, ohne Einwirkung des Hundeführers."),
            new("Verhalten im Verkehr", ExerciseDifficulty.Intermediate, "Verhalten",
                "Hund bleibt ruhig bei vorbeifahrenden Fahrzeugen und Radfahrern, zeigt keine Anzeichen von Angst oder Aggression."),
            new("Begegnung mit Personengruppe", ExerciseDifficulty.Intermediate, "Verhalten",
                "Hund bleibt ruhig und unaufgeregt beim Passieren einer Gruppe von Personen."),
            new("Verhalten gegenüber anderen Hunden", ExerciseDifficulty.Intermediate, "Verhalten",
                "Hund zeigt keine aggressive oder ängstliche Reaktion beim Begegnen eines fremden Hundes."),
            new("Zurücklassen des Hundes", ExerciseDifficulty.Advanced, "Verhalten",
                "Hund bleibt angeleint an der vereinbarten Stelle ruhig, während der Hundeführer außer Sicht ist und ein anderer Hund vorbeigeführt wird."),
        ]);

        // Übungsnamen/Punkte aus früheren Seed-Durchläufen waren frei erfunden, nicht die
        // tatsächliche FCI-IBGH-Struktur (siehe TODO.md) - mit expliziter Genehmigung
        // des Auftraggebers als VDH-Vorstand (analog IGP1-3, siehe Klassenkommentar oben)
        // durch die echten Übungsnamen/Punkte aus der FCI-Prüfungsordnung 2025 ersetzt.
        // Die alten, falsch benannten Übungen bleiben als ungenutzte Exercise-Zeilen
        // bestehen (kein Hard-Delete von ggf. bereits referenzierten Altdaten), werden
        // aber ab der neuen RegulationVersion "2025" (siehe unten) nicht mehr verwendet.
        var ibgh1 = await SeedSportAsync(db, "IBGH1", "Internationale Begleithundeprüfung 1",
        [
            new("Leinenführigkeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund folgt dem Hundeführer aus der Grundstellung auf das HZ \"Fuß\" freudig und konzentriert an lockerer Leine, bleibt mit dem Schulterblatt in Kniehöhe an dessen linker Seite, auch bei Tempo- und Richtungswechseln."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie Leinenführigkeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund setzt sich aus der Bewegung heraus auf das HZ \"Sitz\" sofort und gerade hin, ohne dass der Hundeführer seine Bewegung verändert."),
            new("Ablegen aus der Bewegung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund legt sich aus der Bewegung heraus auf das HZ \"Platz\" sofort und gerade hin, ohne dass der Hundeführer seine Bewegung verändert."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund bleibt während der Vorführung des anderen Hundes ruhig in der Ablage liegen, ohne Einwirkung des Hundeführers."),
        ]);

        var ibgh2 = await SeedSportAsync(db, "IBGH2", "Internationale Begleithundeprüfung 2",
        [
            new("Leinenführigkeit", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie IBGH1, mit höheren Anforderungen an Konzentration und Tempowechsel."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie Leinenführigkeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie IBGH1, mit höheren Anforderungen."),
            new("Ablegen aus der Bewegung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie IBGH1, mit höheren Anforderungen."),
            new("Bringen auf ebener Erde", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund bringt den geworfenen Gegenstand zügig und übergibt ihn in der Grundstellung."),
            new("Voraussenden mit Hinlegen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund läuft auf HZ zielstrebig voraus und legt sich auf das HZ \"Platz\" sofort hin."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund bleibt während der Vorführung des anderen Hundes ruhig in der Ablage liegen, ohne Einwirkung des Hundeführers."),
        ]);

        var ibgh3 = await SeedSportAsync(db, "IBGH3", "Internationale Begleithundeprüfung 3",
        [
            new("Freifolge", ExerciseDifficulty.Advanced, "Unterordnung",
                "Wie IBGH2, mit höheren Anforderungen, ohne Leine geführt."),
            new("Absitzen aus der Bewegung", ExerciseDifficulty.Advanced, "Unterordnung",
                "Wie IBGH2, mit höheren Anforderungen."),
            new("Ablegen aus der Bewegung", ExerciseDifficulty.Advanced, "Unterordnung",
                "Wie IBGH2, mit höheren Anforderungen."),
            new("Steh aus dem Schritt", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund bleibt aus dem Schritt heraus auf das HZ \"Steh\" sofort und gerade stehen, ohne dass der Hundeführer seinen Bewegungsablauf verändert."),
            new("Bringen auf ebener Erde", ExerciseDifficulty.Advanced, "Unterordnung",
                "Wie IBGH2, mit höheren Anforderungen."),
            new("Bringen über die Schrägwand", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund überwindet die 140 cm hohe Schrägwand mit Kletterspringen und bringt dabei das Bringholz zügig zum Hundeführer."),
            new("Voraussenden mit Hinlegen", ExerciseDifficulty.Advanced, "Unterordnung",
                "Wie IBGH2, mit größerer Distanz."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Advanced, "Unterordnung",
                "Hund bleibt während der Vorführung des anderen Hundes ruhig in der Ablage liegen, ohne Einwirkung des Hundeführers."),
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

        // Korrekte VDH-BH/VT-Struktur (Teil A: 60 Punkte, bestanden ab 42 = 70%;
        // Teil B ohne Einzelpunkte, nur Gesamteindruck "bestanden/nicht
        // bestanden"). Ersetzt die fehlerhafte "2024"-Version (Leinenführigkeit
        // 30 statt 15, Freifolge fehlte komplett, Ablage 5 statt 10) - siehe
        // RemoveSupersededVersionAsync-Aufruf unten.
        await SeedRegulationAsync(db, bh, new RegulationSeed("BH", "2025", new DateOnly(2025, 1, 1),
        [
            new("Leinenführigkeit", true, 15, "Normalschritt, Laufschritt, langsamer Schritt, Wendungen und Durchschreiten der Personengruppe - an lockerer Leine."),
            new("Freifolge", true, 15, "Gleicher Ablauf wie Leinenführigkeit, jedoch ohne Leine, inkl. Personengruppe."),
            new("Sitzübung", true, 10, "Aus der Bewegung; Hundeführer entfernt sich mind. 15 Schritte, Hund bleibt ruhig sitzen."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus der Bewegung ablegen, mind. 30 Schritte Entfernung, Abrufen mit Hörzeichen, Endgrundstellung."),
            new("Ablegen unter Ablenkung", true, 10, "Während der Teil-A-Vorführung des anderen Hundes; Hundeführer ca. 30 Schritte entfernt in Sichtweite, Rücken zum Hund."),
            new("Verhalten im Verkehr", true, 0, "Teil B - Begegnung mit Fußgängern, Fahrzeugen, Radfahrer und Jogger; keine Einzelpunkte, Gesamteindruck entscheidet."),
            new("Begegnung mit Personengruppe", true, 0, "Teil B - unbefangenes Verhalten in einer dichten Personengruppe."),
            new("Verhalten gegenüber anderen Hunden", true, 0, "Teil B - Begegnung mit einem fremden, angeleinten Hund ohne aggressive Reaktion."),
            new("Zurücklassen des Hundes", true, 0, "Teil B - Hund wird angeleint zurückgelassen, Hundeführer außer Sicht, ein anderer Hund wird vorbeigeführt."),
        ],
        Description: "VDH-Begleithundprüfung mit Verhaltenstest (BH/VT).\n" +
            "Teil A (Übungsplatz): 5 bewertete Übungen, 60 Punkte gesamt - bestanden ab 42 Punkten (70 %).\n" +
            "Teil B (öffentlicher Verkehrsraum): keine Einzelpunkte, der Leistungsrichter beurteilt den Gesamteindruck.\n" +
            "Voraussetzungen: Mindestalter des Hundes 15 Monate, Sachkundenachweis des Hundeführers, Identitätsnachweis (Chip/Tätowierung).\n" +
            "Teil B wird nur geprüft, wenn Teil A bestanden wurde."));

        // Echte FCI-IBGH-Pflichtübungsliste (UTI-REG-IGP-de-2025, S. 26). Die
        // ursprüngliche, frei erfundene "2024"-Version (Übungen wie
        // "Fußarbeit"/"Abrufen") wurde inzwischen aus DB und Code entfernt,
        // nachdem RemoveOrphanedExercisesAsync (siehe unten) bestätigt hatte,
        // dass keine echten Trainingsdaten mehr darauf verweisen.
        await SeedRegulationAsync(db, ibgh1, new RegulationSeed("IBGH1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Leinenführigkeit", true, 30, "Aufmerksam, freudig, gerade und schnell an lockerer Leine, auch bei Tempo- und Richtungswechseln."),
            new("Freifolge", true, 30, "Wie Leinenführigkeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", true, 15, "Aus 10-15 Schritten Entwicklung, sofort und gerade."),
            new("Ablegen aus der Bewegung", true, 15, "Aus 10-15 Schritten Entwicklung, sofort und gerade."),
            new("Ablegen unter Ablenkung", true, 10, "Während der Vorführung des anderen Hundes, Hundeführer mindestens 10 Schritte entfernt in Sichtweite."),
        ],
        Description: "FCI-Internationale Begleithundprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "5 Übungen der Unterordnung: Leinenführigkeit (30), Freifolge (30), Absitzen (15), Ablegen (15), Ablage unter Ablenkung (10).\n" +
            "Voraussetzung: bestandene BH/VT.\n" +
            "Hinweis: keine Schussgleichgültigkeitsprüfung, kein Bringen - reine Unterordnungsprüfung."));

        await SeedRegulationAsync(db, ibgh2, new RegulationSeed("IBGH2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Leinenführigkeit", true, 20, "Wie IBGH1, mit höheren Anforderungen."),
            new("Freifolge", true, 20, "Wie Leinenführigkeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", true, 15, "Wie IBGH1, mit höheren Anforderungen."),
            new("Ablegen aus der Bewegung", true, 15, "Wie IBGH1, mit höheren Anforderungen."),
            new("Bringen auf ebener Erde", true, 10, "Gegenstand wird vom Hundeführer geworfen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mindestens 30 Schritte voraus, danach Ablegen auf HZ."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mit dem Rücken zum Hund, mindestens 20 Schritte entfernt in Sichtweite."),
        ],
        Description: "FCI-Internationale Begleithundprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "7 Übungen: Leinenführigkeit (20), Freifolge (20), Absitzen (15), Ablegen (15), Bringen (10), Voraussenden (10), Ablage (10).\n" +
            "Neu gegenüber IBGH 1: Bringen auf ebener Erde und Voraussenden mit Hinlegen.\n" +
            "Voraussetzung: bestandene IBGH 1 oder BH/VT."));

        await SeedRegulationAsync(db, ibgh3, new RegulationSeed("IBGH3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 20, "Ohne Leine, wie IBGH2 mit höheren Anforderungen."),
            new("Absitzen aus der Bewegung", true, 10, "Wie IBGH2, mit höheren Anforderungen."),
            new("Ablegen aus der Bewegung", true, 10, "Wie IBGH2, mit höheren Anforderungen."),
            new("Steh aus dem Schritt", true, 10, "Aus 10-15 Schritten Entwicklung, sofort und gerade stehenbleiben."),
            new("Bringen auf ebener Erde", true, 15, "Wie IBGH2, mit höheren Anforderungen."),
            new("Bringen über die Schrägwand", true, 15, "140 cm hohe Schrägwand, mindestens ein Klettersprung mit Bringholz."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größerer Distanz als IBGH2."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 30 Meter entfernt, außer Sicht des Hundes."),
        ],
        Description: "FCI-Internationale Begleithundprüfung Stufe 3 - höchste IBGH-Stufe (100 Punkte, bestanden ab 70).\n" +
            "8 Übungen: Freifolge (20), Absitzen (10), Ablegen (10), Steh aus dem Schritt (10), Bringen (15), Bringen über Schrägwand (15), Voraussenden (10), Ablage (10).\n" +
            "Neu gegenüber IBGH 2: Steh aus dem Schritt und Bringen über die 140-cm-Schrägwand; komplette Arbeit ohne Leine.\n" +
            "Voraussetzung: bestandene IBGH 2 oder BH/VT."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("Fährte A", "2024", new DateOnly(2024, 1, 1),
        [
            new("Fährtenaufnahme", true, 0, "Eigene Fährte, ca. 300 Schritte, 3 gerade Schenkel, 2 Winkel, Fährtenalter ca. 20 Minuten."),
            new("Winkelarbeit", true, 0, "2 Winkel auf der Fährte."),
            new("Gegenstände verweisen", true, 0, "2 Gegenstände auf der Fährte."),
        ],
        Description: "Vereinsinterne Einsteiger-Fährtenprüfung (Trainingsstufe).\n" +
            "Fährte: Eigenfährte, ca. 300 Schritte, 3 Schenkel, 2 Winkel (ca. 90°).\n" +
            "Gegenstände: 2 eigene Gegenstände.\n" +
            "Fährtenalter: ca. 20 Minuten.\n" +
            "Ziel: sichere Fährtenaufnahme und ruhige, konzentrierte Nasenarbeit auf kurzer Strecke."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("Fährte B", "2024", new DateOnly(2024, 1, 1),
        [
            new("Eigenfährte vertiefen", true, 0, "Eigene Fährte, ca. 400 Schritte, 4 Schenkel, Fährtenalter ca. 30 Minuten."),
            new("Winkelarbeit", true, 0, "3 Winkel auf der Fährte."),
            new("Gegenstände verweisen", true, 0, "3 Gegenstände auf der Fährte."),
        ],
        Description: "Vereinsinterne Aufbau-Fährtenprüfung (Trainingsstufe).\n" +
            "Fährte: Eigenfährte, ca. 400 Schritte, 4 Schenkel, 3 Winkel.\n" +
            "Gegenstände: 3 eigene Gegenstände.\n" +
            "Fährtenalter: ca. 30 Minuten.\n" +
            "Ziel: längere Konzentrationsphasen und sauberes Ausarbeiten mehrerer Winkel."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("Fährte C (Fremdfährte)", "2024", new DateOnly(2024, 1, 1),
        [
            new("Fremde Fährte folgen", true, 0, "Fremde Fährte, ca. 600 Schritte, 5 Schenkel, 5 Winkel, Fährtenalter ca. 60 Minuten."),
            new("Winkelarbeit", true, 0, "5 Winkel auf der Fährte."),
            new("Gegenstände verweisen", true, 0, "4 Gegenstände auf der Fährte."),
        ],
        Description: "Vereinsinterne Fortgeschrittenen-Fährtenprüfung (Trainingsstufe).\n" +
            "Fährte: Fremdfährte, ca. 600 Schritte, 5 Schenkel, 5 Winkel.\n" +
            "Gegenstände: 4 fremde Gegenstände.\n" +
            "Fährtenalter: ca. 60 Minuten.\n" +
            "Ziel: Übergang zur Fremdfährte als Vorbereitung auf FCI-IFH 1."));

        // FCI-Fährtenhundprüfungen (FCI-IFH 1-3, UTI-REG-IGP-de-2025 S. 69-79) -
        // eigenständige Prüfungsordnungen derselben Sportart "Fährte" (wie schon
        // Fährte A/B/C), deutlich anspruchsvoller als diese Vereinsprüfungen
        // (800-1800 statt 300-600 Schritte, bis zu 8 statt 5 Schenkel). Nutzt
        // dieselben drei Übungen wie Fährte A/B/C (Fährtenaufnahme/Winkelarbeit/
        // Gegenstände verweisen), da die FCI-PO die Fährtenarbeit als eine
        // zusammenhängende Leistung bewertet statt in einzelne Übungen
        // aufzuteilen - die Punkteverteilung zwischen Fährtenaufnahme und
        // Winkelarbeit ist daher für Trainingszwecke vereinfacht; nur die
        // Gegenstände-Punktzahl (3 x 7 / 3x5+1x6 / 7x3 = jeweils 21 Punkte)
        // entspricht exakt der offiziellen Bewertungstabelle (S. 72).
        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenaufnahme", true, 40, "Eigenfährte, min. 800 Schritte, 5 Schenkel, 4 Winkel ca. 90°, Fährtenalter min. 90 Minuten, Ausarbeitungszeit max. 30 Minuten."),
            new("Winkelarbeit", true, 39, "4 Winkel mit ca. 90° auf der Fährte, Abstand zwischen den Winkeln min. 50 Schritte."),
            new("Gegenstände verweisen", true, 21, "3 dem Hundeführer gehörende Gegenstände, je 7 Punkte. Voraussetzung: bestandene FCI-BH/VT."),
        ],
        Description: "FCI-Fährtenhundprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "Fährte: Eigenfährte, min. 800 Schritte, 5 Schenkel, 4 Winkel (ca. 90°).\n" +
            "Gegenstände: 3 eigene Gegenstände (je 7 Punkte).\n" +
            "Fährtenalter: min. 90 Minuten - Ausarbeitungszeit: max. 30 Minuten.\n" +
            "Voraussetzung: bestandene BH/VT."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenaufnahme", true, 40, "Fremdfährte, min. 1200 Schritte, 7 Schenkel, Fährtenalter min. 120 Minuten, Ausarbeitungszeit max. 30 Minuten, 2 Verleitungen 30 Minuten vor dem Ansatz. Voraussetzung: FCI-IFH 1."),
            new("Winkelarbeit", true, 39, "6 Winkel: die ersten 5 mit ca. 90°, der letzte als spitzer Winkel mit 30°-60°."),
            new("Gegenstände verweisen", true, 21, "4 fremde Gegenstände, 3 x 5 und 1 x 6 Punkte."),
        ],
        Description: "FCI-Fährtenhundprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "Fährte: Fremdfährte, min. 1200 Schritte, 7 Schenkel, 6 Winkel (5 x ca. 90°, 1 spitzer Winkel 30-60°).\n" +
            "Gegenstände: 4 fremde Gegenstände (3 x 5 + 1 x 6 Punkte).\n" +
            "Fährtenalter: min. 120 Minuten - Ausarbeitungszeit: max. 30 Minuten.\n" +
            "Besonderheit: 2 Verleitungen, 30 Minuten vor dem Ansatz gelegt.\n" +
            "Voraussetzung: bestandene FCI-IFH 1."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenaufnahme", true, 40, "Fremdfährte, min. 1800 Schritte, 8 Schenkel (einer als Halbkreis mit ca. 30 Meter Radius), Fährtenalter min. 180 Minuten, Ausarbeitungszeit max. 45 Minuten, Verleitungen 30 Minuten vor dem Ansatz. Voraussetzung: FCI-IFH 2."),
            new("Winkelarbeit", true, 39, "7 Winkel: 2 spitze Winkel zwischen 30° und 60°, die übrigen ca. 90°."),
            new("Gegenstände verweisen", true, 21, "7 fremde Gegenstände, je 3 Punkte."),
        ],
        Description: "FCI-Fährtenhundprüfung Stufe 3 - höchste Fährtenstufe (100 Punkte, bestanden ab 70).\n" +
            "Fährte: Fremdfährte, min. 1800 Schritte, 8 Schenkel, davon einer als Halbkreis (ca. 30 m Radius).\n" +
            "Winkel: 7, davon 2 spitze Winkel (30-60°).\n" +
            "Gegenstände: 7 fremde Gegenstände (je 3 Punkte).\n" +
            "Fährtenalter: min. 180 Minuten - Ausarbeitungszeit: max. 45 Minuten.\n" +
            "Besonderheit: Verleitungen 30 Minuten vor dem Ansatz.\n" +
            "Voraussetzung: bestandene FCI-IFH 2."));

        var igp1 = await SeedSportAsync(db, "IGP1", "FCI-Internationale Gebrauchshundeprüfung 1",
        [
            new("Fährtenarbeit (Eigenfährte)", ExerciseDifficulty.Beginner, "Abteilung A",
                "Eigene Fährte, min. 300 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 20 Minuten, 3 eigene Gegenstände."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund folgt ohne Leine konzentriert in Grundstellung, auch bei Tempo- und Richtungswechseln."),
            new("Sitz aus der Bewegung", ExerciseDifficulty.Beginner, "Abteilung B",
                "Aus dem Normalschritt, Hund setzt sich sofort und bleibt sitzen."),
            new("Bringen auf ebener Erde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund bringt den geworfenen Gegenstand zügig und übergibt ihn in der Grundstellung."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Zwei Sprünge über die Hürde ohne Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Ein Klettersprung über die Schrägwand ohne Bringen."),
            new("Voraussenden mit Hinlegen", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund läuft geradlinig voraus und legt sich auf Kommando ab."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Advanced, "Abteilung B",
                "Hund bleibt während der Übung eines anderen Teams ruhig in der Ablage liegen."),
            new("Stellen und Verbellen", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund findet den Helfer im Versteck und verbellt ihn anhaltend und konzentriert, ohne zu beißen."),
            new("Bewachen nach Rückkehr des Hundeführers", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund bewacht den Helfer aufmerksam und selbstsicher, bis der Hundeführer zurückkehrt."),
            new("Abwehr eines Angriffs aus dem Stand", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich gegen den Angriff des Helfers mit energischem, festem Zufassen."),
            new("Seitentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund begleitet Helfer und Hundeführer aufmerksam, ohne zu bedrängen oder anzuspringen."),
            new("Angriff auf den Hund während des Transports", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich gegen den erneuten Angriff während des Transports."),
            new("Angriff auf den Hund aus der Bewegung", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich gegen den Angriff aus der Bewegung mit vollem, ruhigem Griff und bewacht danach selbstsicher."),
            // Ergänzungen ab hier: in einem früheren Seed-Durchlauf fehlende
            // Pflichtübungen bzw. mit erfundenen statt den offiziellen Namen
            // angelegte Übungen (siehe RegulationSeed "FCI-IGP 1" 2025-2 unten).
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Beginner, "Abteilung B",
                "Hund legt sich aus der Bewegung sofort und gerade hin, wird nach mind. 30 Schritten Entfernung des Hundeführers herangerufen und nimmt die Endgrundstellung ein."),
            new("Revieren", ExerciseDifficulty.Intermediate, "Abteilung C",
                "Hund durchsucht zielstrebig und konzentriert die vorgegebene Fläche nach dem Helfer und zeigt diesen durch Stellen und Verbellen an."),
            new("Verhinderung eines Fluchtversuches", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verhindert einen Fluchtversuch des Helfers durch energisches und entschlossenes Zufassen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich nach der Bewachungsphase gegen einen Angriff des Helfers durch festen, ruhigen Griff."),
        ]);

        var igp2 = await SeedSportAsync(db, "IGP2", "FCI-Internationale Gebrauchshundeprüfung 2",
        [
            new("Fährtenarbeit (Fremdfährte)", ExerciseDifficulty.Intermediate, "Abteilung A",
                "Fremde Fährte, min. 400 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 30 Minuten, 3 fremde Gegenstände."),
            new("Freifolge mit Leine", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund läuft eng und aufmerksam neben dem Hundeführer, auch bei Tempo- und Richtungswechseln."),
            new("Sitz aus der Bewegung", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Aus dem Normalschritt, mit größerer Ablenkung als in IGP1."),
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Aus dem Normalschritt mit Abholen des Hundes."),
            new("Steh aus der Bewegung", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund bleibt auf Kommando aus der Bewegung sofort stehen."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hin- und Rücksprung mit Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Ein Klettersprung über die Schrägwand ohne Bringen."),
            new("Voraussenden mit Hinlegen", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Mit größerer Distanz als in IGP1."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Advanced, "Abteilung B",
                "Mit größerer Ablenkung als in IGP1."),
            new("Stellen und Verbellen", ExerciseDifficulty.Advanced, "Abteilung C",
                "Wie IGP1, mit höheren Anforderungen an Selbstsicherheit."),
            new("Bewachen nach Rückkehr des Hundeführers", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus dem Stand", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Seitentransport", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Angriff auf den Hund während des Transports", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Angriff auf den Hund aus der Bewegung", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Distanzangriff", ExerciseDifficulty.Advanced, "Abteilung C",
                "Zusätzlich zu IGP1: Markierung für den Hundeführer für den Angriff über größere Distanz."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund folgt ohne Leine konzentriert in Grundstellung, auch bei Tempo- und Richtungswechseln, mit größerer Ablenkung als in IGP1."),
            new("Bringen auf ebener Erde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund bringt den geworfenen Gegenstand zügig und übergibt ihn in der Grundstellung."),
            new("Revieren", ExerciseDifficulty.Intermediate, "Abteilung C",
                "Hund durchsucht zielstrebig und konzentriert die vorgegebene Fläche nach dem Helfer und zeigt diesen durch Stellen und Verbellen an."),
            new("Verhinderung eines Fluchtversuches", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verhindert einen Fluchtversuch des Helfers durch energisches und entschlossenes Zufassen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich nach der Bewachungsphase gegen einen Angriff des Helfers durch festen, ruhigen Griff."),
            new("Rückentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund begleitet Hundeführer und Helfer beim Rücktransport aufmerksam am Helfer, ohne zu bedrängen oder anzuspringen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich am Ende des Schutzdienstes gegen einen erneuten Angriff des Helfers durch festen, ruhigen Griff."),
        ]);

        var igp3 = await SeedSportAsync(db, "IGP3", "FCI-Internationale Gebrauchshundeprüfung 3",
        [
            new("Fährtenarbeit (Fremdfährte)", ExerciseDifficulty.Advanced, "Abteilung A",
                "Fremde Fährte, min. 600 Schritte, 5 Schenkel, 4 Winkel ca. 90°, Fährtenalter min. 60 Minuten, 3 fremde Gegenstände."),
            new("Freifolge ohne Leine", ExerciseDifficulty.Advanced, "Abteilung B",
                "Höchste Stufe, auch durch eine Personengruppe."),
            new("Sitz aus dem Laufschritt", ExerciseDifficulty.Advanced, "Abteilung B", "Höchste Stufe, aus dem Laufschritt statt Normalschritt."),
            new("Ablegen in Verbindung mit Herankommen aus dem Laufschritt", ExerciseDifficulty.Advanced, "Abteilung B", "Höchste Stufe, aus dem Laufschritt statt Normalschritt."),
            new("Steh aus dem Laufschritt mit Heranrufen des Hundes", ExerciseDifficulty.Advanced, "Abteilung B", "Höchste Stufe, aus dem Laufschritt statt Normalschritt."),
            new("Freisprünge / Hin- und Rückklettersprung mit Bringen", ExerciseDifficulty.Advanced, "Abteilung B", "Hin- und Rückklettersprung mit Bringen."),
            new("Voraussenden mit Hinlegen", ExerciseDifficulty.Advanced, "Abteilung B",
                "Mit größter Distanz und Ablenkung der drei Stufen."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Advanced, "Abteilung B",
                "Höchste Ablenkungsstufe (z.B. Übung eines anderen Teams direkt nebenan)."),
            new("Stellen und Verbellen", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Bewachen nach Rückkehr des Hundeführers", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus dem Stand", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Seitentransport", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Angriff auf den Hund während des Transports", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Angriff auf den Hund aus der Bewegung", ExerciseDifficulty.Advanced, "Abteilung C", "Wie in der vorherigen Stufe, mit höheren Anforderungen."),
            new("Distanzangriff", ExerciseDifficulty.Advanced, "Abteilung C",
                "Größte Distanz der drei Stufen."),
            new("Sitz aus der Bewegung", ExerciseDifficulty.Advanced, "Abteilung B",
                "Aus dem Laufschritt, höchste Ablenkungsstufe der drei Stufen."),
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Advanced, "Abteilung B",
                "Aus dem Laufschritt mit Abholen des Hundes, höchste Ablenkungsstufe der drei Stufen."),
            new("Steh aus der Bewegung", ExerciseDifficulty.Advanced, "Abteilung B",
                "Aus dem Laufschritt mit Heranrufen des Hundes, höchste Ablenkungsstufe der drei Stufen."),
            new("Freifolge", ExerciseDifficulty.Advanced, "Abteilung B",
                "Höchste Stufe, auch durch eine Personengruppe."),
            new("Bringen auf ebener Erde", ExerciseDifficulty.Advanced, "Abteilung B",
                "Hund bringt den geworfenen Gegenstand zügig und übergibt ihn in der Grundstellung."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", ExerciseDifficulty.Advanced, "Abteilung B",
                "Hin- und Rücksprung mit Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", ExerciseDifficulty.Advanced, "Abteilung B",
                "Hin- und Rückklettersprung mit Bringen."),
            new("Revieren", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund durchsucht zielstrebig und konzentriert die vorgegebene Fläche (6 Verstecke) nach dem Helfer und zeigt diesen durch Stellen und Verbellen an."),
            new("Verhinderung eines Fluchtversuches", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verhindert einen Fluchtversuch des Helfers durch energisches und entschlossenes Zufassen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich nach der Bewachungsphase gegen einen Angriff des Helfers durch festen, ruhigen Griff."),
            new("Rückentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund begleitet Hundeführer und Helfer beim Rücktransport aufmerksam am Helfer, ohne zu bedrängen oder anzuspringen."),
            new("Überfall auf den Hund aus dem Rückentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Helfer überfällt den Hund unmittelbar aus dem Rückentransport heraus; Hund verteidigt sich durch energisches, festes Zufassen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich am Ende des Schutzdienstes gegen einen erneuten Angriff des Helfers durch festen, ruhigen Griff."),
        ]);

        // Echte Übungsnamen und Punktzahlen aus UTI-REG-IGP-de-2025 (S. 18, 44,
        // 56). Eine frühere, mit "Näherungswert" markierte Version (teils
        // falsch benannte/fehlende Abteilung-B/C-Übungen) wurde inzwischen aus
        // DB und Code entfernt, nachdem RemoveOrphanedExercisesAsync (siehe
        // unten) bestätigt hatte, dass keine echten Trainingsdaten mehr darauf
        // verweisen. VersionLabel "2025-2", da "2025" bereits historisch für
        // die entfernte Version vergeben war.
        await SeedRegulationAsync(db, igp1, new RegulationSeed("FCI-IGP 1", "2025-2", new DateOnly(2025, 2, 1),
        [
            new("Fährtenarbeit (Eigenfährte)", true, 100, "Eigenfährte, min. 300 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 20 Minuten, 3 eigene Gegenstände à 7 Punkte."),
            new("Freifolge", true, 15, "Mit Schussgleichgültigkeitsprüfung (2 Schüsse Kaliber 6mm)."),
            new("Sitz aus der Bewegung", true, 10, "Aus 10-15 Schritten Entwicklung im Normalschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus 10-15 Schritten Entwicklung im Normalschritt, Herankommen nach mind. 30 Schritten Entfernung."),
            new("Bringen auf ebener Erde", true, 15, "Bringholz 650 Gramm, geworfen in markiertes Quadrat 4x4m."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "2 Sprünge über die Hürde, ohne Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", true, 15, "Ein Klettersprung über die 191cm hohe Schrägwand, ohne Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mindestens 30 Schritte voraus, danach Ablegen auf HZ \"Platz\"."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 30 Meter entfernt, außer Sicht des Hundes."),
            new("Revieren", true, 5, "2 Verstecke, Hund läuft Mittellinie ab und umläuft die Verstecke auf HZ \"Revier\"/\"Voran\"."),
            new("Stellen und Verbellen", true, 15, "Anhaltendes, selbstbewusstes Verbellen am Versteck, ca. 20 Sekunden."),
            new("Verhinderung eines Fluchtversuches", true, 20, "Energisches und entschlossenes Verhindern der Flucht des Helfers."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 30, "Voller, fester und ruhiger Griff, Selbstsicherheit und Belastbarkeit bei Schlagandrohung mit dem Softstock."),
            new("Angriff auf den Hund aus der Bewegung", true, 30, "Helfer greift aus ca. 20 Metern Entfernung mit Vertreibungslauten frontal an."),
        ],
        Description: "FCI-Internationale Gebrauchshundprüfung Stufe 1 (300 Punkte gesamt).\n" +
            "Abteilung A - Fährte (100 Punkte): Eigenfährte, min. 300 Schritte, 3 Schenkel, 2 Winkel, Fährtenalter min. 20 Minuten, 3 eigene Gegenstände.\n" +
            "Abteilung B - Unterordnung (100 Punkte): 8 Übungen inkl. Schussgleichgültigkeitsprüfung.\n" +
            "Abteilung C - Schutzdienst (100 Punkte): 5 Übungen, 2 Verstecke beim Revieren.\n" +
            "Bestanden: mindestens 70 Punkte in JEDER Abteilung.\n" +
            "Voraussetzung: bestandene BH/VT, Mindestalter 18 Monate."));

        await SeedRegulationAsync(db, igp2, new RegulationSeed("FCI-IGP 2", "2025-2", new DateOnly(2025, 2, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 100, "Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 30 Minuten, 3 fremde Gegenstände à 7 Punkte."),
            new("Freifolge", true, 15, "Mit größerer Ablenkung als IGP1."),
            new("Sitz aus der Bewegung", true, 10, "Mit größerer Ablenkung als IGP1."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Mit Abholen des Hundes durch den Hundeführer."),
            new("Steh aus der Bewegung", true, 10, "Aus 10-15 Schritten Entwicklung, sofort und gerade stehenbleiben."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 1000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen, Bringholz 650 Gramm."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Ein Klettersprung über die Schrägwand, ohne Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größerer Distanz als IGP1."),
            new("Ablegen unter Ablenkung", true, 10, "Mit größerer Ablenkung als IGP1."),
            new("Revieren", true, 5, "4 Verstecke."),
            new("Stellen und Verbellen", true, 15, "Wie IGP1, mit höheren Anforderungen an Selbstsicherheit."),
            new("Verhinderung eines Fluchtversuches", true, 15, "Wie IGP1, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 20, "Wie IGP1, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Ca. 30 Schritte Rücktransport zum Leistungsrichter, Hund läuft beobachtend neben dem Helfer."),
            new("Angriff auf den Hund aus der Bewegung", true, 20, "Aus der Lauerstellung, mit Vertreibungslauten frontal."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 20, "Erneuter Angriff im Anschluss an \"Angriff auf den Hund aus der Bewegung\", voller fester Griff."),
        ],
        Description: "FCI-Internationale Gebrauchshundprüfung Stufe 2 (300 Punkte gesamt).\n" +
            "Abteilung A - Fährte (100 Punkte): Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel, Fährtenalter min. 30 Minuten, 3 fremde Gegenstände.\n" +
            "Abteilung B - Unterordnung (100 Punkte): 9 Übungen, zusätzlich Steh aus der Bewegung, Bringholz 1000 Gramm.\n" +
            "Abteilung C - Schutzdienst (100 Punkte): 7 Übungen, 4 Verstecke, zusätzlich Rückentransport.\n" +
            "Bestanden: mindestens 70 Punkte in JEDER Abteilung.\n" +
            "Voraussetzung: bestandene FCI-IGP 1."));

        await SeedRegulationAsync(db, igp3, new RegulationSeed("FCI-IGP 3", "2025-2", new DateOnly(2025, 2, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 100, "Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel ca. 90°, Fährtenalter min. 60 Minuten, 3 fremde Gegenstände à 7 Punkte."),
            new("Freifolge", true, 15, "Höchste Stufe, auch durch eine Personengruppe, ohne Leine."),
            new("Sitz aus der Bewegung", true, 10, "Aus dem Laufschritt, höchste Ablenkungsstufe."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus dem Laufschritt mit Abholen des Hundes, höchste Ablenkungsstufe."),
            new("Steh aus der Bewegung", true, 10, "Aus dem Laufschritt mit Heranrufen des Hundes, höchste Ablenkungsstufe."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 2000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen, Bringholz 650 Gramm."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Hin- und Rückklettersprung mit Bringen, Bringholz 650 Gramm."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größter Distanz und Ablenkung der drei Stufen."),
            new("Ablegen unter Ablenkung", true, 10, "Höchste Ablenkungsstufe der drei Stufen."),
            new("Revieren", true, 10, "6 Verstecke."),
            new("Stellen und Verbellen", true, 15, "Wie IGP2, mit höheren Anforderungen."),
            new("Verhinderung eines Fluchtversuches", true, 10, "Wie IGP2, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 15, "Wie IGP2, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Ca. 30 Schritte Rücktransport zum Leistungsrichter."),
            new("Überfall auf den Hund aus dem Rückentransport", true, 15, "Unmittelbar aus dem Rückentransport, ohne anzuhalten, mit dynamischer Wendung des Helfers."),
            new("Angriff auf den Hund aus der Bewegung", true, 15, "Helfer läuft das Vorführgelände im Laufschritt bis zur Mittellinie und greift dann frontal an."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 15, "Erneuter Angriff im Anschluss an \"Angriff auf den Hund aus der Bewegung\", voller fester Griff."),
        ],
        Description: "FCI-Internationale Gebrauchshundprüfung Stufe 3 - höchste Stufe (300 Punkte gesamt).\n" +
            "Abteilung A - Fährte (100 Punkte): Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel, Fährtenalter min. 60 Minuten, 3 fremde Gegenstände.\n" +
            "Abteilung B - Unterordnung (100 Punkte): 9 Übungen aus dem Laufschritt, Bringholz 2000 Gramm, Hin- und Rückklettersprung.\n" +
            "Abteilung C - Schutzdienst (100 Punkte): 8 Übungen, 6 Verstecke, zusätzlich Überfall aus dem Rückentransport.\n" +
            "Bestanden: mindestens 70 Punkte in JEDER Abteilung.\n" +
            "Voraussetzung: bestandene FCI-IGP 2. WM-/Championats-Stufe."));

        // Die ursprünglichen fehlerhaften RegulationVersions (BH/IBGH "2024",
        // IGP "2025") wurden inzwischen aus dem Code entfernt, nachdem die
        // einmalige Bereinigung bestätigt hatte, dass keine echten
        // Trainingsdaten mehr darauf verweisen - RemoveSupersededVersionAsync
        // ist daher hier nicht mehr nötig. Verbleibt als Hilfsfunktion für
        // künftige Prüfungsordnungs-Revisionen (siehe
        // PRUEFUNGSORDNUNG_UPDATE.md "Versions-Supersession"). Als
        // Sicherheitsnetz läuft RemoveOrphanedExercisesAsync weiterhin bei
        // jedem Start - idempotent, findet auf einer bereits bereinigten
        // Datenbank einfach nichts mehr.
        // Fehlerhafte BH-Version "2024" (Leinenführigkeit 30 statt 15,
        // Freifolge fehlte, Ablage 5 statt 10) durch die korrekte
        // "2025"-Version abgelöst - siehe RegulationSeed "BH" oben.
        await RemoveSupersededVersionAsync(db, bh, "BH", "2024");

        await RemoveOrphanedExercisesAsync(db, bh);
        await RemoveOrphanedExercisesAsync(db, ibgh1);
        await RemoveOrphanedExercisesAsync(db, ibgh2);
        await RemoveOrphanedExercisesAsync(db, ibgh3);
        await RemoveOrphanedExercisesAsync(db, igp1);
        await RemoveOrphanedExercisesAsync(db, igp2);
        await RemoveOrphanedExercisesAsync(db, igp3);

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
            regulation = new Regulation { SportId = sport.Id, Name = seed.Name, Description = seed.Description };
            db.Regulations.Add(regulation);
            await db.SaveChangesAsync();
        }
        else if (seed.Description is not null && regulation.Description != seed.Description)
        {
            // Beschreibung aus späteren Seed-Durchläufen nachpflegen - der
            // Seed ist für den globalen Katalog die Quelle der Wahrheit
            // (analog zur MaxPoints-Nachpflege unten).
            regulation.Description = seed.Description;
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
            {
                // Bewusst ein harter Fehler statt stillschweigendem Überspringen:
                // ein RegulationSeed-Eintrag, der auf eine nicht (mehr) unter
                // diesem exakten Namen existierende Übung verweist (Tippfehler,
                // vergessene Ergänzung im zugehörigen SeedSportAsync-Aufruf),
                // führte sonst dazu, dass eine ganze Pflichtübung einfach
                // unbemerkt fehlte - genau die Art von Lücke, die zum
                // Nutzer-Feedback "Bausteine fehlen" geführt hat. Läuft nur in
                // Development (siehe Program.cs), bricht also nie Production.
                throw new InvalidOperationException(
                    $"Seed-Fehler: Übung \"{exerciseSeed.ExerciseName}\" für Prüfungsordnung \"{seed.Name}\" ({seed.VersionLabel}) " +
                    $"ist nicht in der Exercise-Liste der Sportart \"{sport.Code}\" deklariert (SeedSportAsync-Aufruf prüfen - " +
                    "Name muss exakt übereinstimmen).");
            }

            var regulationExercise = await db.RegulationExercises
                .FirstOrDefaultAsync(re => re.RegulationVersionId == version.Id && re.ExerciseId == exercise.Id);
            if (regulationExercise is null)
            {
                db.RegulationExercises.Add(new RegulationExercise
                {
                    RegulationVersionId = version.Id,
                    ExerciseId = exercise.Id,
                    IsMandatory = exerciseSeed.IsMandatory,
                    MaxPoints = exerciseSeed.MaxPoints,
                    ScoringNotes = exerciseSeed.ScoringNotes
                });
            }
            else
            {
                // Werte aus späteren Seed-Durchläufen nachpflegen (z.B.
                // korrigierte Punktzahlen) - vorher wurden bestehende
                // Zeilen nie aktualisiert, nur fehlende neu angelegt, eine
                // Korrektur landete dadurch nie in bereits gestarteten
                // lokalen Entwicklungsdatenbanken.
                regulationExercise.IsMandatory = exerciseSeed.IsMandatory;
                regulationExercise.MaxPoints = exerciseSeed.MaxPoints;
                regulationExercise.ScoringNotes = exerciseSeed.ScoringNotes;
            }
        }

        await db.SaveChangesAsync();
    }

    // Entfernt eine durch eine neuere, korrigierte Version abgelöste,
    // fehlerhafte RegulationVersion (siehe PRUEFUNGSORDNUNG_UPDATE.md
    // "Versions-Supersession") - Cascade-Delete entfernt automatisch deren
    // RegulationExercise-Zeilen. Von der neuen Version weiterhin genutzte,
    // gemeinsame Exercise-Zeilen (z.B. "Freifolge", die in alter wie neuer
    // Version vorkommt) bleiben unberührt, da nur die JOIN-Zeile der alten
    // Version gelöscht wird, nicht die Übung selbst.
    private static async Task RemoveSupersededVersionAsync(ApplicationDbContext db, Sport sport, string regulationName, string oldVersionLabel)
    {
        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.SportId == sport.Id && r.Name == regulationName);
        if (regulation is null) return;

        var oldVersion = await db.RegulationVersions
            .FirstOrDefaultAsync(v => v.RegulationId == regulation.Id && v.VersionLabel == oldVersionLabel);
        if (oldVersion is null) return;

        var oldRegulationExercises = await db.RegulationExercises
            .Where(re => re.RegulationVersionId == oldVersion.Id)
            .ToListAsync();
        db.RegulationExercises.RemoveRange(oldRegulationExercises);
        db.RegulationVersions.Remove(oldVersion);
        await db.SaveChangesAsync();
    }

    // Entfernt globale (nicht vereinsspezifische) Übungen, auf die nach
    // RemoveSupersededVersionAsync keine RegulationExercise- oder
    // Trainingsdaten mehr verweisen - Rückstände aus inzwischen abgelösten,
    // fehlerhaft benannten Prüfungsordnungs-Versionen (z.B. "Fußarbeit"
    // statt "Leinenführigkeit" bei IBGH). Vereinsspezifische Übungen
    // (ClubId gesetzt) sind bewusst nie Teil einer globalen
    // Prüfungsordnung und daher hiervon ausgenommen.
    private static async Task RemoveOrphanedExercisesAsync(ApplicationDbContext db, Sport sport)
    {
        var orphaned = await db.Exercises
            .Where(e => e.SportId == sport.Id && e.ClubId == null)
            .Where(e => !db.RegulationExercises.Any(re => re.ExerciseId == e.Id))
            .Where(e => !db.TrainingExercises.Any(te => te.ExerciseId == e.Id))
            .Where(e => !db.TrainingPlanItems.Any(tpi => tpi.ExerciseId == e.Id))
            .ToListAsync();
        if (orphaned.Count == 0) return;

        db.Exercises.RemoveRange(orphaned);
        await db.SaveChangesAsync();
    }
}

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
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund bleibt während der Vorführung der Übung \"Leinenführigkeit\" des anderen Hundes ruhig in der Ablage liegen, ohne Einwirkung des Hundeführers."),
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

        await SeedRegulationAsync(db, bh, new RegulationSeed("BH", "2024", new DateOnly(2024, 1, 1),
        [
            // Leinenführigkeit/Sitz/Ablegen mit Abrufen (Teil A, auf dem Übungsplatz)
            // werden tatsächlich bewertet (siehe S. 22-23) - waren zuvor fälschlich
            // mit 0 Punkten hinterlegt. Teil B (Verkehr, ab "Verhalten im Verkehr")
            // wird laut PO bewusst NICHT einzeln gepunktet, nur als Gesamteindruck
            // beurteilt - 0 Punkte dort ist korrekt, kein Fehler.
            new("Leinenführigkeit", true, 30, "Auf Wegen, Plätzen und im Verkehr; keine durchgehende Leinenspannung."),
            new("Sitz aus der Bewegung", true, 10, "Aus normalem Gehen, ohne Geschwindigkeitsänderung des Hundeführers."),
            new("Ablegen mit Abrufen", true, 10, "Hund bleibt liegen, bis er abgerufen wird."),
            new("Verhalten im Verkehr", true, 0, "Begegnung mit mind. einem Fahrzeug und Radfahrer."),
            new("Begegnung mit Personengruppe", true, 0, "Gruppe aus mind. 6 Personen, normales Tempo."),
            new("Verhalten gegenüber anderen Hunden", true, 0, "Begegnung mit einem fremden, angeleinten Hund."),
            new("Zurücklassen des Hundes", true, 0, "Hundeführer entfernt sich außer Sichtweite für ca. 1 Minute."),
            new("Ablegen unter Ablenkung", true, 5,
                "Hundeführer entfernt sich ca. 10 Meter, Leine eingesteckt oder umgehängt, während der andere Hund die Übung \"Leinenführigkeit\" zeigt. Verlässt der Hund die Ablageposition für mehr als 3 Meter: 0 Punkte, sonst maximal 5 Punkte."),
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

        // Echte FCI-IBGH-Pflichtübungsliste (UTI-REG-IGP-de-2025, S. 26) - neuere
        // RegulationVersion statt Korrektur der "2024"-Version, damit die alten,
        // falsch benannten Übungen oben sauber abgelöst werden (siehe
        // GetRegulationDetailAsync "neueste Version per ValidFrom").
        await SeedRegulationAsync(db, ibgh1, new RegulationSeed("IBGH1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Leinenführigkeit", true, 30, "Aufmerksam, freudig, gerade und schnell an lockerer Leine, auch bei Tempo- und Richtungswechseln."),
            new("Freifolge", true, 30, "Wie Leinenführigkeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", true, 15, "Aus 10-15 Schritten Entwicklung, sofort und gerade."),
            new("Ablegen aus der Bewegung", true, 15, "Aus 10-15 Schritten Entwicklung, sofort und gerade."),
            new("Ablegen unter Ablenkung", true, 10, "Während der Vorführung des anderen Hundes, Hundeführer mindestens 10 Schritte entfernt in Sichtweite."),
        ]));

        await SeedRegulationAsync(db, ibgh2, new RegulationSeed("IBGH2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Leinenführigkeit", true, 20, "Wie IBGH1, mit höheren Anforderungen."),
            new("Freifolge", true, 20, "Wie Leinenführigkeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", true, 15, "Wie IBGH1, mit höheren Anforderungen."),
            new("Ablegen aus der Bewegung", true, 15, "Wie IBGH1, mit höheren Anforderungen."),
            new("Bringen auf ebener Erde", true, 10, "Gegenstand wird vom Hundeführer geworfen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mindestens 30 Schritte voraus, danach Ablegen auf HZ."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mit dem Rücken zum Hund, mindestens 20 Schritte entfernt in Sichtweite."),
        ]));

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
        ]));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenaufnahme", true, 40, "Fremdfährte, min. 1200 Schritte, 7 Schenkel, Fährtenalter min. 120 Minuten, Ausarbeitungszeit max. 30 Minuten, 2 Verleitungen 30 Minuten vor dem Ansatz. Voraussetzung: FCI-IFH 1."),
            new("Winkelarbeit", true, 39, "6 Winkel: die ersten 5 mit ca. 90°, der letzte als spitzer Winkel mit 30°-60°."),
            new("Gegenstände verweisen", true, 21, "4 fremde Gegenstände, 3 x 5 und 1 x 6 Punkte."),
        ]));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenaufnahme", true, 40, "Fremdfährte, min. 1800 Schritte, 8 Schenkel (einer als Halbkreis mit ca. 30 Meter Radius), Fährtenalter min. 180 Minuten, Ausarbeitungszeit max. 45 Minuten, Verleitungen 30 Minuten vor dem Ansatz. Voraussetzung: FCI-IFH 2."),
            new("Winkelarbeit", true, 39, "7 Winkel: 2 spitze Winkel zwischen 30° und 60°, die übrigen ca. 90°."),
            new("Gegenstände verweisen", true, 21, "7 fremde Gegenstände, je 3 Punkte."),
        ]));

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

        await SeedRegulationAsync(db, igp1, new RegulationSeed("FCI-IGP 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit (Eigenfährte)", true, 100, "Eigenfährte, min. 300 Schritte, 3 eigene Gegenstände à 7 Punkte."),
            new("Freifolge", true, 10, "Näherungswert - exakte Aufteilung der Abteilung B vor Produktiveinsatz prüfen."),
            new("Sitz aus der Bewegung", true, 10, "Näherungswert."),
            new("Bringen auf ebener Erde", true, 15, "Näherungswert."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Näherungswert."),
            new("Klettersprung / Bringen über die Schrägwand", true, 15, "Näherungswert."),
            new("Voraussenden mit Hinlegen", true, 10, "Näherungswert."),
            new("Ablegen unter Ablenkung", true, 10, "Näherungswert."),
            new("Stellen und Verbellen", true, 10, "Näherungswert - exakte Aufteilung der Abteilung C vor Produktiveinsatz prüfen."),
            new("Bewachen nach Rückkehr des Hundeführers", true, 10, "Näherungswert."),
            new("Abwehr eines Angriffs aus dem Stand", true, 20, "Näherungswert."),
            new("Seitentransport", true, 10, "Näherungswert."),
            new("Angriff auf den Hund während des Transports", true, 20, "Näherungswert."),
            new("Angriff auf den Hund aus der Bewegung", true, 30, "Näherungswert."),
        ]));

        await SeedRegulationAsync(db, igp2, new RegulationSeed("FCI-IGP 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 100, "Fremdfährte, min. 400 Schritte, 3 fremde Gegenstände à 7 Punkte."),
            new("Freifolge mit Leine", true, 10, "Näherungswert."),
            new("Sitz aus der Bewegung", true, 10, "Näherungswert."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Näherungswert."),
            new("Steh aus der Bewegung", true, 10, "Näherungswert."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Näherungswert."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Näherungswert."),
            new("Voraussenden mit Hinlegen", true, 10, "Näherungswert."),
            new("Ablegen unter Ablenkung", true, 10, "Näherungswert."),
            new("Stellen und Verbellen", true, 10, "Näherungswert."),
            new("Bewachen nach Rückkehr des Hundeführers", true, 10, "Näherungswert."),
            new("Abwehr eines Angriffs aus dem Stand", true, 15, "Näherungswert."),
            new("Seitentransport", true, 10, "Näherungswert."),
            new("Angriff auf den Hund während des Transports", true, 15, "Näherungswert."),
            new("Angriff auf den Hund aus der Bewegung", true, 25, "Näherungswert."),
            new("Distanzangriff", true, 15, "Näherungswert."),
        ]));

        await SeedRegulationAsync(db, igp3, new RegulationSeed("FCI-IGP 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 100, "Fremdfährte, min. 600 Schritte, 3 fremde Gegenstände à 7 Punkte."),
            new("Freifolge ohne Leine", true, 15, "Näherungswert."),
            new("Sitz aus dem Laufschritt", true, 10, "Näherungswert."),
            new("Ablegen in Verbindung mit Herankommen aus dem Laufschritt", true, 10, "Näherungswert."),
            new("Steh aus dem Laufschritt mit Heranrufen des Hundes", true, 10, "Näherungswert."),
            new("Freisprünge / Hin- und Rückklettersprung mit Bringen", true, 10, "Näherungswert."),
            new("Voraussenden mit Hinlegen", true, 10, "Näherungswert."),
            new("Ablegen unter Ablenkung", true, 10, "Näherungswert."),
            new("Stellen und Verbellen", true, 10, "Näherungswert."),
            new("Bewachen nach Rückkehr des Hundeführers", true, 10, "Näherungswert."),
            new("Abwehr eines Angriffs aus dem Stand", true, 15, "Näherungswert."),
            new("Seitentransport", true, 10, "Näherungswert."),
            new("Angriff auf den Hund während des Transports", true, 15, "Näherungswert."),
            new("Angriff auf den Hund aus der Bewegung", true, 25, "Näherungswert."),
            new("Distanzangriff", true, 15, "Näherungswert."),
        ]));

        // Korrektur der oben als "Näherungswert" markierten IGP1-3-Werte: echte
        // Übungsnamen und Punktzahlen aus UTI-REG-IGP-de-2025 (S. 18, 44, 56) -
        // neuere RegulationVersion statt Korrektur der "2025"-Version oben, damit
        // die teils falsch benannten/fehlenden Abteilung-B/C-Übungen sauber
        // abgelöst werden (siehe GetRegulationDetailAsync "neueste Version per
        // ValidFrom"). Abteilung A war bereits korrekt und wird unverändert
        // übernommen.
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
        ]));

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
        ]));

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
}

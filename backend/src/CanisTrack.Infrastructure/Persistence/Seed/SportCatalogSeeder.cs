using CanisTrack.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanisTrack.Infrastructure.Persistence.Seed;

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

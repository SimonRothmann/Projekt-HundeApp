using Dogity.Application.Abstractions;
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
/// Ausnahme IGP1-3 sowie FPr/UPr/SPr/GPr/StöPr/IGP-FH/IAD: Übungsnamen und
/// Punktzahlen sind direkt der FCI Prüfungsordnung 2025 (UTI-REG-IGP-de-2025,
/// gültig ab 01.01.2025) entnommen. Dies erfolgt mit expliziter Genehmigung
/// des Auftraggebers in seiner Funktion als VDH-Vorstand (siehe TODO.md),
/// abweichend von der sonst im Projekt geltenden Vorsichtsregel.
/// Die Punktaufteilungen der IGP-Abteilungen B/C wurden am 2026-07-16
/// seitenweise gegen das offizielle PDF verifiziert (Abt. B: S. 44,
/// Abt. C: S. 56, Fährte: S. 36) - der frühere "Näherungswert"-Vorbehalt
/// aus der ersten Text-Extraktion ist damit erledigt.
///
/// Vollständige Gegenprüfung am 2026-08-19 gegen UTI-REG-IGP-de-2025
/// (fci.be, gültig ab 01.01.2025): ALLE Punktzahlen von IGP 1-3, IBGH 1-3,
/// UPr/SPr/GPr/FPr, IFH 1-3 und IAD stimmen. Korrigiert wurden nur
/// Beschreibungstexte:
/// - Schrägwand: 191 cm ist die LÄNGE der beiden Wandteile, die senkrechte
///   Hindernishöhe beträgt in allen Stufen 160 cm (PO S. 47).
/// - "Abholen des Hundes" gehört zur Übung "Steh aus der Bewegung"
///   (IGP 2); beim "Ablegen in Verbindung mit Herankommen" wird der Hund
///   herangerufen, nicht abgeholt (PO S. 44 + S. 49).
/// - "Sitz aus der Bewegung" kennt in KEINER Stufe einen Laufschritt; die
///   Entwicklung beträgt immer 10 bis 15 Schritte (PO S. 48). Nur "Ablegen
///   in Verbindung mit Herankommen" und "Steh" laufen in der IGP 3 aus dem
///   Laufschritt.
///
/// Zweite Durchsicht am 2026-08-23: Abteilung A stand in FCI-IGP 1-3 und
/// FCI-FPr 1-3 als EINE Übung über 100 Punkte. Die PO teilt sie auf - die
/// Gegenstände zählen einzeln (S. 36: "3 x 7 Punkte" in allen drei Stufen),
/// also 21 Punkte, die restlichen 79 auf die Fährtenarbeit. Bei den
/// eigenständigen Fährten-Prüfungsordnungen stand es bereits so; die sechs
/// anderen sind jetzt nachgezogen. Die Gesamtpunktzahl bleibt gleich.
///
/// Turnierhundsport und Agility kommen NICHT aus der FCI-IGP-PO, sondern aus
/// den VDH-Prüfungsordnungen (THS gültig ab 01.01.2025, Agility ab
/// 01.01.2026). Beide werden über Zeit und Fehlerpunkte gewertet - ihre
/// Disziplinen tragen deshalb 0 Punkte, wie schon Teil B der BH und die IAD.
///
/// Vierte Durchsicht am 2026-08-31: Die Übung "Leinenführigkeit" heißt in BH
/// und IBGH jetzt "Fußarbeit" - die PO beschreibt unter diesem Titel
/// Grundstellung, Kehrtwendung, Tempowechsel und Personengruppe. Umbenannt
/// wird die bestehende Zeile (RenameExerciseAsync), damit Trainingsdaten
/// mitwandern. Die BH bekommt außerdem die Freifolge als Pflichtbestandteil
/// OHNE Punktwertung: ohne Leine gearbeitet wird nach der Fußarbeit
/// tatsächlich (Sitz und Ablegen), bewertet wird sie laut PO aber nicht.
///
/// Offen und bewusst NICHT geändert (siehe docs/PO_VERIFIKATION.md):
/// die Punktaufteilung der IFH-Übungen - sie berührt vorhandene
/// Trainingsdaten und ist eine fachliche Entscheidung des Betreibers.
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

        // Es gibt nur EINE BH/VT, und das ist die der FCI-Prüfungsordnung
        // (Betreiberauskunft als VDH-Vorstand, 2026-08-19). Teil A: 4 bewertete
        // Übungen à 30/10/10/10 = 60 Punkte, bestanden ab 42; Teil B ohne
        // Einzelpunkte.
        //
        // Zur Freifolge (Betreiberhinweis 2026-08-31, gegen die PO geprüft):
        // Eine eigenständig BEWERTETE Freifolge gibt es in der BH nicht - die
        // PO kennt für Teil A genau vier Übungen. Ohne Leine gearbeitet wird
        // aber sehr wohl: Nach der Fußarbeit wird abgeleint, Sitz und Ablegen
        // mit Herankommen werden frei gezeigt ("Danach wird der Hund angeleint
        // und zur Übung 'Ablegen unter Ablenkung' geführt", PO S. 23). Die
        // Freifolge steht deshalb mit 0 Punkten in der Prüfungsordnung - wie
        // Teil B, die IAD und die Sprint-Disziplinen des THS.
        // "Leinenführigkeit" heißt in BH und IBGH jetzt "Fußarbeit": Die
        // Prüfungsordnung beschreibt unter diesem Titel Grundstellung,
        // Kehrtwendung(en), Tempowechsel und die Personengruppe - also die
        // komplette Fußarbeit, nicht bloß "an der Leine laufen". Der
        // PO-Titel bleibt in der Beschreibung erhalten, damit man die Übung
        // in der Prüfungsordnung wiederfindet.
        //
        // Umbenannt wird die BESTEHENDE Zeile (siehe RenameExerciseAsync) -
        // Trainingseinträge, Bewertungen und Pläne hängen an der Übungs-Id.
        // Der Turnierhundsport behält seine eigene "Leinenführigkeit": dort
        // ist es tatsächlich eine andere Übung (VDH-VK1, 15 Punkte).
        await RenameExerciseAsync(db, "BH", "Leinenführigkeit", "Fußarbeit");
        await RenameExerciseAsync(db, "IBGH1", "Leinenführigkeit", "Fußarbeit");
        await RenameExerciseAsync(db, "IBGH2", "Leinenführigkeit", "Fußarbeit");

        var bh = await SeedSportAsync(db, "BH", "Begleithundeprüfung",
        [
            new("Fußarbeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "In der Prüfungsordnung \"Leinenführigkeit\": Grundstellung, mindestens 50 Schritte geradeaus, Kehrtwendung nach links, Laufschritt und langsamer Schritt (je 10-15 Schritte), zum Abschluss die Personengruppe. Der Hund bleibt mit dem Schulterblatt in Kniehöhe links und setzt sich beim Anhalten selbstständig."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Nach der Fußarbeit wird abgeleint: Sitz und Ablegen mit Herankommen werden ohne Leine gezeigt, ebenso die 10-15 Schritte Entwicklung davor. Der Hund folgt dabei frei bei Fuß."),
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
            new("Fußarbeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "In der Prüfungsordnung \"Leinenführigkeit\": aus der Grundstellung auf das HZ \"Fuß\" an lockerer Leine, Schulterblatt in Kniehöhe links, Kehrtwendung nach links, Laufschritt und langsame Schritte (je 10-15), nach der zweiten Kehrtwendung ein Halt mit selbstständigem Sitz, danach die Personengruppe in Form einer Acht."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie die Fußarbeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund setzt sich aus der Bewegung heraus auf das HZ \"Sitz\" sofort und gerade hin, ohne dass der Hundeführer seine Bewegung verändert."),
            new("Ablegen aus der Bewegung", ExerciseDifficulty.Beginner, "Unterordnung",
                "Hund legt sich aus der Bewegung heraus auf das HZ \"Platz\" sofort und gerade hin, ohne dass der Hundeführer seine Bewegung verändert."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Hund bleibt während der Vorführung des anderen Hundes ruhig in der Ablage liegen, ohne Einwirkung des Hundeführers."),
        ]);

        var ibgh2 = await SeedSportAsync(db, "IBGH2", "Internationale Begleithundeprüfung 2",
        [
            new("Fußarbeit", ExerciseDifficulty.Intermediate, "Unterordnung",
                "In der Prüfungsordnung \"Leinenführigkeit\". Wie IBGH1, mit höheren Anforderungen an Konzentration und Tempowechsel."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Wie die Fußarbeit, jedoch ohne Leine."),
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
            new("Fährtenarbeit", ExerciseDifficulty.Intermediate, "Fährte",
                "Ansatz, Ausarbeitung und Winkel als Ganzes - so bewertet die Prüfungsordnung die Fährte auch (Einzelpunkte gibt es nur für die Gegenstände)."),
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

        // Teil A: 60 Punkte, bestanden ab 42 (70 %); Teil B ohne Einzelpunkte,
        // nur Gesamteindruck. Die frühere "2024"-Version hatte mit 30 Punkten
        // für die Fußarbeit und ohne bewertete Freifolge bereits das Richtige
        // stehen; die damalige "Korrektur" auf 15+15 war der eigentliche
        // Fehler und ist am 2026-08-19 gegen die FCI-PO 2025 zurückgenommen
        // worden. An dieser Aufteilung ändert die Freifolge mit 0 Punkten
        // nichts.
        await SeedRegulationAsync(db, bh, new RegulationSeed("BH", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fußarbeit", true, 30, "In der PO \"Leinenführigkeit\". Angeleint: Grundstellung, mindestens 50 Schritte geradeaus, Kehrtwendung nach links, Laufschritt und langsamer Schritt (je 10-15 Schritte), danach eine Gruppe von mindestens 4 sich bewegenden Personen (links und rechts umrunden, einmal anhalten)."),
            new("Freifolge", true, 0, "Keine eigenständig bewertete Übung - die PO vergibt dafür keine Punkte. Nach der Fußarbeit wird abgeleint: Sitz und Ablegen mit Herankommen werden ohne Leine gezeigt, danach wird der Hund für die Ablage unter Ablenkung wieder angeleint."),
            new("Sitzübung", true, 10, "Ohne Leine. Nach 10-15 Schritten Entwicklung sofort und gerade hinsetzen; Hundeführer entfernt sich mindestens 15 Schritte."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Ohne Leine. Nach 10-15 Schritten Entwicklung ablegen, Hundeführer geht mindestens 30 Schritte weiter, Abrufen mit Hörzeichen, Vorsitz, dann Endgrundstellung."),
            new("Ablegen unter Ablenkung", true, 10, "Während der Teil-A-Vorführung des anderen Hundes; Hundeführer ca. 30 Schritte entfernt in Sichtweite, Rücken zum Hund."),
            new("Verhalten im Verkehr", true, 0, "Teil B - Begegnung mit Fußgängern, Fahrzeugen, Radfahrer und Jogger; keine Einzelpunkte, Gesamteindruck entscheidet."),
            new("Begegnung mit Personengruppe", true, 0, "Teil B - unbefangenes Verhalten in einer dichten Personengruppe."),
            new("Verhalten gegenüber anderen Hunden", true, 0, "Teil B - Begegnung mit einem fremden, angeleinten Hund ohne aggressive Reaktion."),
            new("Zurücklassen des Hundes", true, 0, "Teil B - Hund wird angeleint zurückgelassen, Hundeführer außer Sicht, ein anderer Hund wird vorbeigeführt."),
        ],
        Description: "Begleithundeprüfung mit Verkehrsteil (BH/VT) nach FCI-Prüfungsordnung, gültig ab 01.01.2025.\n" +
            "Teil A (Übungsplatz): 4 bewertete Übungen, 60 Punkte gesamt - bestanden ab 42 Punkten (70 %).\n" +
            "Fußarbeit 30, Sitz 10, Ablegen in Verbindung mit Herankommen 10, Ablegen unter Ablenkung 10.\n" +
            "Nach der Fußarbeit wird abgeleint: Sitz und Ablegen mit Herankommen werden in Freifolge gezeigt, danach wird der Hund für die Ablage unter Ablenkung wieder angeleint. Eigene Punkte gibt die Prüfungsordnung für die Freifolge nicht - sie steht hier als trainierbare Übung ohne Punktwertung.\n" +
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
            new("Fußarbeit", true, 30, "In der PO \"Leinenführigkeit\": aus der Grundstellung an lockerer Leine, Kehrtwendung nach links, Tempowechsel, Halt mit selbstständigem Sitz, Personengruppe in Form einer Acht."),
            new("Freifolge", true, 30, "Wie die Fußarbeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", true, 15, "Aus 10-15 Schritten Entwicklung, sofort und gerade."),
            new("Ablegen aus der Bewegung", true, 15, "Aus 10-15 Schritten Entwicklung, sofort und gerade."),
            new("Ablegen unter Ablenkung", true, 10, "Während der Vorführung des anderen Hundes, Hundeführer mindestens 10 Schritte entfernt in Sichtweite."),
        ],
        Description: "FCI-Internationale Begleithundprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "5 Übungen der Unterordnung: Fußarbeit an der Leine (30), Freifolge (30), Absitzen (15), Ablegen (15), Ablage unter Ablenkung (10).\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate.\n" +
            "Hinweis: keine Schussgleichgültigkeitsprüfung, kein Bringen - reine Unterordnungsprüfung."));

        await SeedRegulationAsync(db, ibgh2, new RegulationSeed("IBGH2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fußarbeit", true, 20, "In der PO \"Leinenführigkeit\". Wie IBGH1, mit höheren Anforderungen."),
            new("Freifolge", true, 20, "Wie die Fußarbeit, jedoch ohne Leine."),
            new("Absitzen aus der Bewegung", true, 15, "Wie IBGH1, mit höheren Anforderungen."),
            new("Ablegen aus der Bewegung", true, 15, "Wie IBGH1, mit höheren Anforderungen."),
            new("Bringen auf ebener Erde", true, 10, "Gegenstand wird vom Hundeführer geworfen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mindestens 30 Schritte voraus, danach Ablegen auf HZ."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mit dem Rücken zum Hund, mindestens 20 Schritte entfernt in Sichtweite."),
        ],
        Description: "FCI-Internationale Begleithundprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "7 Übungen: Fußarbeit an der Leine (20), Freifolge (20), Absitzen (15), Ablegen (15), Bringen (10), Voraussenden (10), Ablage (10).\n" +
            "Neu gegenüber IBGH 1: Bringen auf ebener Erde und Voraussenden mit Hinlegen.\n" +
            "Startvoraussetzung: bestandene FCI-IBGH 1.\n" +
            "Mindestalter: 15 Monate."));

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
            "Startvoraussetzung: bestandene FCI-IBGH 2, FCI-Obedience 1 oder FCI-IGP 1.\n" +
            "Mindestalter: 15 Monate."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("IGP 1 - Fährte", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit", true, 79, "Eigenfährte, min. 300 Schritte, 3 Schenkel, 2 Winkel ca. 90° mit min. 50 Schritten Abstand, Fährtenalter min. 20 Minuten, Ausarbeitungszeit max. 15 Minuten, Fährtenleine 5 Meter."),
            new("Gegenstände verweisen", true, 21, "3 dem Hundeführer gehörende Gegenstände, je 7 Punkte - auf dem ersten Schenkel, auf dem zweiten Schenkel und am Ende."),
        ],
        Description: "Die Fährte der FCI-IGP 1 (Abteilung A, 100 Punkte). Sie lässt sich auch einzeln laufen - dann als FCI-FPr 1.\n" +
            "Fährte: Eigenfährte, min. 300 Schritte, 3 Schenkel, 2 Winkel (ca. 90°).\n" +
            "Gegenstände: 3 eigene Gegenstände (je 7 Punkte).\n" +
            "Fährtenalter: min. 20 Minuten - Ausarbeitungszeit: max. 15 Minuten.\n" +
            "Fährtenleine: 5 Meter."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("IGP 2 - Fährte", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit", true, 79, "Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel ca. 90° mit min. 50 Schritten Abstand, Fährtenalter min. 30 Minuten, Ausarbeitungszeit max. 15 Minuten, Fährtenleine 10 Meter."),
            new("Gegenstände verweisen", true, 21, "3 fremde Gegenstände, je 7 Punkte - auf dem ersten Schenkel, auf dem zweiten Schenkel und am Ende."),
        ],
        Description: "Die Fährte der FCI-IGP 2 (Abteilung A, 100 Punkte). Sie lässt sich auch einzeln laufen - dann als FCI-FPr 2.\n" +
            "Fährte: Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel (ca. 90°).\n" +
            "Gegenstände: 3 fremde Gegenstände (je 7 Punkte).\n" +
            "Fährtenalter: min. 30 Minuten - Ausarbeitungszeit: max. 15 Minuten.\n" +
            "Fährtenleine: 10 Meter."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("IGP 3 - Fährte", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit", true, 79, "Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel ca. 90° mit min. 50 Schritten Abstand, Fährtenalter min. 60 Minuten, Ausarbeitungszeit max. 20 Minuten, Fährtenleine 10 Meter."),
            new("Gegenstände verweisen", true, 21, "3 fremde Gegenstände, je 7 Punkte - der erste nach min. 100 Schritten, der zweite auf Richteranweisung, der dritte am Ende."),
        ],
        Description: "Die Fährte der FCI-IGP 3 (Abteilung A, 100 Punkte). Sie lässt sich auch einzeln laufen - dann als FCI-FPr 3.\n" +
            "Fährte: Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel (ca. 90°).\n" +
            "Gegenstände: 3 fremde Gegenstände (je 7 Punkte).\n" +
            "Fährtenalter: min. 60 Minuten - Ausarbeitungszeit: max. 20 Minuten.\n" +
            "Fährtenleine: 10 Meter."));

        // FCI-Fährtenhundprüfungen (FCI-IFH 1-3, UTI-REG-IGP-de-2025 S. 69-79) -
        // eigenständige Prüfungsordnungen derselben Sportart "Fährte" (wie schon
        // Fährte A/B/C), deutlich anspruchsvoller als die IGP-Fährten
        // (800-1800 statt 300-600 Schritte, bis zu 8 statt 5 Schenkel).
        //
        // Die FCI-PO bewertet die Fährtenarbeit als EINE zusammenhängende
        // Leistung; Einzelpunkte gibt es nur für die Gegenstände (3 x 7 /
        // 3x5+1x6 / 7x3 = jeweils 21 Punkte, S. 72). Die restlichen 79 Punkte
        // stehen deshalb als eine Übung "Fährtenarbeit". Die frühere Aufteilung
        // in "Fährtenaufnahme 40" und "Winkelarbeit 39" war frei erfunden -
        // "Winkelarbeit" kommt in der gesamten PO nicht vor. Beide bleiben als
        // Katalogübungen zum Trainieren erhalten, tragen aber keine Punkte
        // mehr vor.
        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit", true, 79, "Eigenfährte, min. 800 Schritte, 5 Schenkel, 4 Winkel ca. 90° mit min. 50 Schritten Abstand, Fährtenalter min. 90 Minuten, Ausarbeitungszeit max. 30 Minuten."),
            new("Gegenstände verweisen", true, 21, "3 dem Hundeführer gehörende Gegenstände, je 7 Punkte. Voraussetzung: bestandene FCI-BH/VT."),
        ],
        Description: "FCI-Fährtenhundprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "Fährte: Eigenfährte, min. 800 Schritte, 5 Schenkel, 4 Winkel (ca. 90°).\n" +
            "Gegenstände: 3 eigene Gegenstände (je 7 Punkte).\n" +
            "Fährtenalter: min. 90 Minuten - Ausarbeitungszeit: max. 30 Minuten.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit", true, 79, "Fremdfährte, min. 1200 Schritte, 7 Schenkel, 6 Winkel (die ersten 5 ca. 90°, der letzte spitz mit 30°-60°), Fährtenalter min. 120 Minuten, Ausarbeitungszeit max. 30 Minuten, Verleitungen 30 Minuten vor dem Ansatz."),
            new("Gegenstände verweisen", true, 21, "4 fremde Gegenstände, 3 x 5 und 1 x 6 Punkte."),
        ],
        Description: "FCI-Fährtenhundprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "Fährte: Fremdfährte, min. 1200 Schritte, 7 Schenkel, 6 Winkel (5 x ca. 90°, 1 spitzer Winkel 30-60°).\n" +
            "Gegenstände: 4 fremde Gegenstände (3 x 5 + 1 x 6 Punkte).\n" +
            "Fährtenalter: min. 120 Minuten - Ausarbeitungszeit: max. 30 Minuten.\n" +
            "Besonderheit: 2 Verleitungen, 30 Minuten vor dem Ansatz gelegt.\n" +
            "Startvoraussetzung: bestandene FCI-IFH 1.\n" +
            "Mindestalter: 19 Monate."));

        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IFH 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit", true, 79, "Fremdfährte, min. 1800 Schritte, 8 Schenkel (einer als Halbkreis mit ca. 30 Meter Radius), 7 Winkel (2 spitz zwischen 30° und 60°), Fährtenalter min. 180 Minuten, Ausarbeitungszeit max. 45 Minuten, Verleitungen 30 Minuten vor dem Ansatz."),
            new("Gegenstände verweisen", true, 21, "7 fremde Gegenstände, je 3 Punkte."),
        ],
        Description: "FCI-Fährtenhundprüfung Stufe 3 - höchste Fährtenstufe (100 Punkte, bestanden ab 70).\n" +
            "Fährte: Fremdfährte, min. 1800 Schritte, 8 Schenkel, davon einer als Halbkreis (ca. 30 m Radius).\n" +
            "Winkel: 7, davon 2 spitze Winkel (30-60°).\n" +
            "Gegenstände: 7 fremde Gegenstände (je 3 Punkte).\n" +
            "Fährtenalter: min. 180 Minuten - Ausarbeitungszeit: max. 45 Minuten.\n" +
            "Besonderheit: Verleitungen 30 Minuten vor dem Ansatz.\n" +
            "Startvoraussetzung: bestandene FCI-IFH 2.\n" +
            "Mindestalter: 20 Monate."));

        var igp1 = await SeedSportAsync(db, "IGP1", "FCI-Internationale Gebrauchshundeprüfung 1",
        [
            new("Fährtenarbeit (Eigenfährte)", ExerciseDifficulty.Beginner, "Abteilung A",
                "Eigene Fährte, min. 300 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 20 Minuten, 3 eigene Gegenstände."),
            new("Gegenstände verweisen", ExerciseDifficulty.Intermediate, "Abteilung A",
                "Direktes, überzeugendes Verweisen der Gegenstände in Fährtenrichtung - je 7 Punkte (FCI-PO 2025, S. 36)."),
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
            new("Gegenstände verweisen", ExerciseDifficulty.Intermediate, "Abteilung A",
                "Direktes, überzeugendes Verweisen der Gegenstände in Fährtenrichtung - je 7 Punkte (FCI-PO 2025, S. 36)."),
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
            new("Gegenstände verweisen", ExerciseDifficulty.Intermediate, "Abteilung A",
                "Direktes, überzeugendes Verweisen der Gegenstände in Fährtenrichtung - je 7 Punkte (FCI-PO 2025, S. 36)."),
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
                "Entwicklung von 10 bis 15 Schritten - in allen Stufen gleich, kein Laufschritt."),
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Advanced, "Abteilung B",
                "Entwicklung im Laufschritt; der Hund wird herangerufen."),
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
            new("Fährtenarbeit (Eigenfährte)", true, 79, "Eigenfährte, min. 300 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 20 Minuten."),
            new("Gegenstände verweisen", true, 21, "3 dem Hundeführer gehörende Gegenstände, je 7 Punkte - auf dem ersten Schenkel, auf dem zweiten Schenkel und am Ende."),
            new("Freifolge", true, 15, "Mit Schussgleichgültigkeitsprüfung (2 Schüsse Kaliber 6mm)."),
            new("Sitz aus der Bewegung", true, 10, "Aus 10-15 Schritten Entwicklung im Normalschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus 10-15 Schritten Entwicklung im Normalschritt, Herankommen nach mind. 30 Schritten Entfernung."),
            new("Bringen auf ebener Erde", true, 15, "Bringholz 650 Gramm, geworfen in markiertes Quadrat 4x4m."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "2 Sprünge über die Hürde, ohne Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", true, 15, "Ein Klettersprung über die Schrägwand, ohne Bringen. Senkrechte Höhe 160 cm - die beiden Wandteile sind je 191 cm lang und schräg gegeneinander gestellt."),
            new("Voraussenden mit Hinlegen", true, 10, "Mindestens 30 Schritte voraus, danach Ablegen auf HZ \"Platz\"."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 10 Meter entfernt in Sichtweite, seitwärts zum Hund stehend."),
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
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, igp2, new RegulationSeed("FCI-IGP 2", "2025-2", new DateOnly(2025, 2, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 79, "Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 30 Minuten."),
            new("Gegenstände verweisen", true, 21, "3 fremde Gegenstände, je 7 Punkte - auf dem ersten Schenkel, auf dem zweiten Schenkel und am Ende."),
            new("Freifolge", true, 15, "Mit größerer Ablenkung als IGP1."),
            new("Sitz aus der Bewegung", true, 10, "Mit größerer Ablenkung als IGP1."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Entwicklung im Normalschritt; der Hund wird auf Richteranweisung herangerufen."),
            new("Steh aus der Bewegung", true, 10, "Entwicklung im Normalschritt, sofort und gerade stehenbleiben; der Hundeführer holt den Hund ab."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 1000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen, Bringholz 650 Gramm."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Ein Klettersprung über die Schrägwand, ohne Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größerer Distanz als IGP1."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mit dem Rücken zum Hund, mindestens 20 Meter entfernt in Sichtweite."),
            new("Revieren", true, 5, "4 Verstecke."),
            new("Stellen und Verbellen", true, 15, "Wie IGP1, mit höheren Anforderungen an Selbstsicherheit."),
            new("Verhinderung eines Fluchtversuches", true, 15, "Wie IGP1, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 20, "Wie IGP1, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Rückentransport über ca. 30 Schritte, anschließend Seitentransport zum Leistungsrichter über ca. 20 Schritte."),
            new("Angriff auf den Hund aus der Bewegung", true, 20, "Aus der Lauerstellung, mit Vertreibungslauten frontal."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 20, "Erneuter Angriff im Anschluss an \"Angriff auf den Hund aus der Bewegung\", voller fester Griff."),
        ],
        Description: "FCI-Internationale Gebrauchshundprüfung Stufe 2 (300 Punkte gesamt).\n" +
            "Abteilung A - Fährte (100 Punkte): Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel, Fährtenalter min. 30 Minuten, 3 fremde Gegenstände.\n" +
            "Abteilung B - Unterordnung (100 Punkte): 9 Übungen, zusätzlich Steh aus der Bewegung, Bringholz 1000 Gramm.\n" +
            "Abteilung C - Schutzdienst (100 Punkte): 7 Übungen, 4 Verstecke, zusätzlich Rückentransport.\n" +
            "Bestanden: mindestens 70 Punkte in JEDER Abteilung.\n" +
            "Startvoraussetzung: bestandene FCI-IGP 1.\n" +
            "Mindestalter: 19 Monate."));

        await SeedRegulationAsync(db, igp3, new RegulationSeed("FCI-IGP 3", "2025-2", new DateOnly(2025, 2, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 79, "Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel ca. 90°, Fährtenalter min. 60 Minuten."),
            new("Gegenstände verweisen", true, 21, "3 fremde Gegenstände, je 7 Punkte - der erste nach min. 100 Schritten, der zweite auf Richteranweisung, der dritte am Ende."),
            new("Freifolge", true, 15, "Höchste Stufe, auch durch eine Personengruppe, ohne Leine."),
            new("Sitz aus der Bewegung", true, 10, "Entwicklung von 10 bis 15 Schritten - in allen Stufen gleich, kein Laufschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Entwicklung im Laufschritt; der Hund wird herangerufen."),
            new("Steh aus der Bewegung", true, 10, "Aus dem Laufschritt mit Heranrufen des Hundes, höchste Ablenkungsstufe."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 2000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen, Bringholz 650 Gramm."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Hin- und Rückklettersprung mit Bringen, Bringholz 650 Gramm."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größter Distanz und Ablenkung der drei Stufen."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 30 Meter entfernt, außer Sicht des Hundes."),
            new("Revieren", true, 10, "6 Verstecke."),
            new("Stellen und Verbellen", true, 15, "Wie IGP2, mit höheren Anforderungen."),
            new("Verhinderung eines Fluchtversuches", true, 10, "Wie IGP2, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 15, "Wie IGP2, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Rückentransport über ca. 30 Schritte, endet mit dem Beginn des Überfalls aus dem Rückentransport."),
            new("Überfall auf den Hund aus dem Rückentransport", true, 15, "Unmittelbar aus dem Rückentransport, ohne anzuhalten, mit dynamischer Wendung des Helfers."),
            new("Angriff auf den Hund aus der Bewegung", true, 15, "Helfer läuft das Vorführgelände im Laufschritt bis zur Mittellinie und greift dann frontal an."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 15, "Erneuter Angriff im Anschluss an \"Angriff auf den Hund aus der Bewegung\", voller fester Griff."),
        ],
        Description: "FCI-Internationale Gebrauchshundprüfung Stufe 3 - höchste Stufe (300 Punkte gesamt).\n" +
            "Abteilung A - Fährte (100 Punkte): Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel, Fährtenalter min. 60 Minuten, 3 fremde Gegenstände.\n" +
            "Abteilung B - Unterordnung (100 Punkte): 9 Übungen; Ablegen mit Herankommen und Steh aus dem Laufschritt, Bringholz 2000 Gramm, Hin- und Rückklettersprung.\n" +
            "Abteilung C - Schutzdienst (100 Punkte): 8 Übungen, 6 Verstecke, zusätzlich Überfall aus dem Rückentransport.\n" +
            "Bestanden: mindestens 70 Punkte in JEDER Abteilung.\n" +
            "Startvoraussetzung: bestandene FCI-IGP 2.\n" +
            "Mindestalter: 20 Monate. WM-/Championats-Stufe."));

        // ---------------------------------------------------------------
        // Zusätzliche Prüfungen (UTI-REG-IGP-de-2025, S. 68): FPr/UPr/SPr
        // bestehen jeweils NUR aus der Abteilung A/B/C der entsprechenden
        // IGP-Stufe, GPr aus B+C. Punktwerte sind daher identisch mit den
        // IGP-Tabellen (Abt. B: S. 44, Abt. C: S. 56). Es wird kein
        // Ausbildungstitel im Sinne der Ausstellungs-/Zuchtordnung vergeben.
        // Modelliert wie die Fährte: EINE Sportart pro Prüfungsfamilie mit
        // einer Regulation je Stufe (statt einer Sportart pro Stufe wie bei
        // den historisch früher angelegten IGP1-3).
        // ---------------------------------------------------------------

        var fpr = await SeedSportAsync(db, "FPR", "FCI-Fährtenprüfung (FPr)",
        [
            new("Fährtenarbeit (Eigenfährte)", ExerciseDifficulty.Beginner, "Abteilung A",
                "Eigene Fährte nach den IGP-Regeln für Abteilung A: sichere Aufnahme, tiefe Nase, gleichmäßiges Tempo, überzeugendes Verweisen der Gegenstände."),
            new("Fährtenarbeit (Fremdfährte)", ExerciseDifficulty.Intermediate, "Abteilung A",
                "Fremde Fährte nach den IGP-Regeln für Abteilung A: sichere Aufnahme, tiefe Nase, gleichmäßiges Tempo, überzeugendes Verweisen der Gegenstände."),
            new("Gegenstände verweisen", ExerciseDifficulty.Intermediate, "Abteilung A",
                "Direktes, überzeugendes Verweisen der Gegenstände in Fährtenrichtung - je 7 Punkte (FCI-PO 2025, S. 36)."),
        ]);

        await SeedRegulationAsync(db, fpr, new RegulationSeed("FCI-FPr 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit (Eigenfährte)", true, 79, "Wie FCI-IGP 1 Abteilung A: Eigenfährte, min. 300 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 20 Minuten, Ausarbeitungszeit max. 15 Minuten."),
            new("Gegenstände verweisen", true, 21, "3 dem Hundeführer gehörende Gegenstände, je 7 Punkte - auf dem ersten Schenkel, auf dem zweiten Schenkel und am Ende."),
        ],
        Description: "FCI-Fährtenprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "Dies IST die Fährte (Abteilung A) der FCI-IGP 1, einzeln gelaufen - such danach ruhig als \"IGP 1 Fährte\".\n" +
            "Die Stufe ist frei wählbar; die Prüfungen müssen nicht in der Reihenfolge 1 bis 3 abgelegt werden.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate."));

        await SeedRegulationAsync(db, fpr, new RegulationSeed("FCI-FPr 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 79, "Wie FCI-IGP 2 Abteilung A: Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel ca. 90°, Fährtenalter min. 30 Minuten, Ausarbeitungszeit max. 15 Minuten."),
            new("Gegenstände verweisen", true, 21, "3 fremde Gegenstände, je 7 Punkte - auf dem ersten Schenkel, auf dem zweiten Schenkel und am Ende."),
        ],
        Description: "FCI-Fährtenprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "Dies IST die Fährte (Abteilung A) der FCI-IGP 2, einzeln gelaufen - such danach ruhig als \"IGP 2 Fährte\".\n" +
            "Die Stufe ist frei wählbar; die Prüfungen müssen nicht in der Reihenfolge 1 bis 3 abgelegt werden.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate."));

        await SeedRegulationAsync(db, fpr, new RegulationSeed("FCI-FPr 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit (Fremdfährte)", true, 79, "Wie FCI-IGP 3 Abteilung A: Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel ca. 90°, Fährtenalter min. 60 Minuten, Ausarbeitungszeit max. 20 Minuten."),
            new("Gegenstände verweisen", true, 21, "3 fremde Gegenstände, je 7 Punkte - der erste nach min. 100 Schritten, der zweite auf Richteranweisung, der dritte am Ende."),
        ],
        Description: "FCI-Fährtenprüfung Stufe 3 (100 Punkte, bestanden ab 70).\n" +
            "Dies IST die Fährte (Abteilung A) der FCI-IGP 3, einzeln gelaufen - such danach ruhig als \"IGP 3 Fährte\".\n" +
            "Die Stufe ist frei wählbar; die Prüfungen müssen nicht in der Reihenfolge 1 bis 3 abgelegt werden.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate."));

        var upr = await SeedSportAsync(db, "UPR", "FCI-Unterordnungsprüfung (UPr)",
        [
            new("Freifolge", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund folgt ohne Leine konzentriert in Grundstellung, auch bei Tempo- und Richtungswechseln, inkl. Schussgleichgültigkeit und Personengruppe."),
            new("Sitz aus der Bewegung", ExerciseDifficulty.Beginner, "Abteilung B",
                "Hund setzt sich aus der Bewegung sofort und gerade hin, ohne dass der Hundeführer seine Bewegung verändert."),
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Beginner, "Abteilung B",
                "Hund legt sich aus der Bewegung sofort hin und wird nach Entfernung des Hundeführers herangerufen."),
            new("Steh aus der Bewegung", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund bleibt auf Hörzeichen aus der Bewegung sofort und gerade stehen."),
            new("Bringen auf ebener Erde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund bringt das geworfene Bringholz zügig und übergibt es in der Grundstellung."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Sprünge über die 100 cm hohe Hürde, je nach Stufe mit oder ohne Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Klettersprünge über die Schrägwand (senkrechte Höhe 160 cm), je nach Stufe mit oder ohne Bringen."),
            new("Voraussenden mit Hinlegen", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund läuft geradlinig mindestens 30 Schritte voraus und legt sich auf Hörzeichen sofort ab."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Advanced, "Abteilung B",
                "Hund bleibt während der Vorführung des anderen Hundes ruhig in der Ablage liegen."),
        ]);

        await SeedRegulationAsync(db, upr, new RegulationSeed("FCI-UPr 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 15, "Mit Schussgleichgültigkeitsprüfung (2 Schüsse Kaliber 6mm)."),
            new("Sitz aus der Bewegung", true, 10, "Aus 10-15 Schritten Entwicklung im Normalschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus dem Normalschritt, Herankommen nach mind. 30 Schritten Entfernung."),
            new("Bringen auf ebener Erde", true, 15, "Bringholz 650 Gramm, geworfen in markiertes Quadrat 4x4m."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "2 Sprünge über die Hürde, ohne Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", true, 15, "Ein Klettersprung über die Schrägwand, ohne Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mindestens 30 Schritte voraus, danach Ablegen auf HZ \"Platz\"."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 10 Meter entfernt in Sichtweite, seitwärts zum Hund stehend."),
        ],
        Description: "FCI-Unterordnungsprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "Besteht nur aus der Abteilung B der FCI-IGP 1 (8 Übungen).\n" +
            "Die Stufe ist frei wählbar; die Prüfungen müssen nicht in der Reihenfolge 1 bis 3 abgelegt werden.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate."));

        await SeedRegulationAsync(db, upr, new RegulationSeed("FCI-UPr 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 15, "Mit größerer Ablenkung als UPr 1."),
            new("Sitz aus der Bewegung", true, 10, "Aus dem Normalschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus dem Normalschritt."),
            new("Steh aus der Bewegung", true, 10, "Aus dem Normalschritt mit Abholen des Hundes."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 1000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen, Bringholz 650 Gramm."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Ein Klettersprung über die Schrägwand, ohne Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größerer Distanz als UPr 1."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mit dem Rücken zum Hund, mindestens 20 Meter entfernt in Sichtweite."),
        ],
        Description: "FCI-Unterordnungsprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "Besteht nur aus der Abteilung B der FCI-IGP 2 (9 Übungen, zusätzlich Steh aus der Bewegung).\n" +
            "Die Stufe ist frei wählbar; die Prüfungen müssen nicht in der Reihenfolge 1 bis 3 abgelegt werden.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate."));

        await SeedRegulationAsync(db, upr, new RegulationSeed("FCI-UPr 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 15, "Höchste Stufe, auch durch eine Personengruppe."),
            new("Sitz aus der Bewegung", true, 10, "Entwicklung von 10 bis 15 Schritten - in allen Stufen gleich, kein Laufschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus dem Laufschritt."),
            new("Steh aus der Bewegung", true, 10, "Aus dem Laufschritt mit Heranrufen des Hundes."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 2000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen, Bringholz 650 Gramm."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Hin- und Rückklettersprung mit Bringen, Bringholz 650 Gramm."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größter Distanz und Ablenkung der drei Stufen."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 30 Meter entfernt, außer Sicht des Hundes."),
        ],
        Description: "FCI-Unterordnungsprüfung Stufe 3 (100 Punkte, bestanden ab 70).\n" +
            "Besteht nur aus der Abteilung B der FCI-IGP 3 (9 Übungen aus dem Laufschritt, Bringholz 2000 Gramm).\n" +
            "Die Stufe ist frei wählbar; die Prüfungen müssen nicht in der Reihenfolge 1 bis 3 abgelegt werden.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate."));

        var spr = await SeedSportAsync(db, "SPR", "FCI-Schutzdienstprüfung (SPr)",
        [
            new("Revieren", ExerciseDifficulty.Intermediate, "Abteilung C",
                "Hund durchsucht zielstrebig und konzentriert die Verstecke nach dem Helfer, eng und aufmerksam umlaufend."),
            new("Stellen und Verbellen", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund stellt den Helfer selbstbewusst und verbellt ihn anhaltend (ca. 20 Sekunden), ohne zu beißen."),
            new("Verhinderung eines Fluchtversuches", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verhindert einen Fluchtversuch des Helfers durch energisches und entschlossenes Zufassen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich nach der Bewachungsphase gegen einen Angriff des Helfers durch festen, ruhigen Griff."),
            new("Rückentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund begleitet Hundeführer und Helfer beim Rückentransport aufmerksam, ohne zu bedrängen oder anzuspringen."),
            new("Überfall auf den Hund aus dem Rückentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Helfer überfällt den Hund unmittelbar aus dem Rückentransport heraus; Hund verteidigt sich durch energisches, festes Zufassen."),
            new("Angriff auf den Hund aus der Bewegung", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich gegen den frontalen Angriff des Helfers mit vollem, ruhigem Griff und bewacht danach selbstsicher."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich am Ende des Schutzdienstes gegen einen erneuten Angriff des Helfers durch festen, ruhigen Griff."),
        ]);

        await SeedRegulationAsync(db, spr, new RegulationSeed("FCI-SPr 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Revieren", true, 5, "2 Verstecke."),
            new("Stellen und Verbellen", true, 15, "10 Punkte für das Stellen, 5 für das Verbellen (ca. 20 Sekunden)."),
            new("Verhinderung eines Fluchtversuches", true, 20, "Energisches und entschlossenes Verhindern der Flucht des Helfers."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 30, "Voller, fester und ruhiger Griff, Selbstsicherheit und Belastbarkeit bei Schlagandrohung mit dem Softstock."),
            new("Angriff auf den Hund aus der Bewegung", true, 30, "Helfer greift aus ca. 20 Metern Entfernung mit Vertreibungslauten frontal an."),
        ],
        Description: "FCI-Schutzdienstprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "Besteht nur aus der Abteilung C der FCI-IGP 1 (5 Übungen, 2 Verstecke).\n" +
            "Die Stufe ist frei wählbar; reine Schutzdienstveranstaltungen (nur Teilnehmende in der Abteilung C) sind nicht zulässig.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, spr, new RegulationSeed("FCI-SPr 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Revieren", true, 5, "4 Verstecke."),
            new("Stellen und Verbellen", true, 15, "10 Punkte für das Stellen, 5 für das Verbellen."),
            new("Verhinderung eines Fluchtversuches", true, 15, "Wie SPr 1, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 20, "Wie SPr 1, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Rückentransport über ca. 30 Schritte, anschließend Seitentransport zum Leistungsrichter über ca. 20 Schritte."),
            new("Angriff auf den Hund aus der Bewegung", true, 20, "Aus der Lauerstellung (ca. 30 Meter), mit Vertreibungslauten frontal."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 20, "Erneuter Angriff im Anschluss, voller fester Griff."),
        ],
        Description: "FCI-Schutzdienstprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "Besteht nur aus der Abteilung C der FCI-IGP 2 (7 Übungen, 4 Verstecke, zusätzlich Rückentransport).\n" +
            "Die Stufe ist frei wählbar; reine Schutzdienstveranstaltungen sind nicht zulässig.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, spr, new RegulationSeed("FCI-SPr 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Revieren", true, 10, "6 Verstecke."),
            new("Stellen und Verbellen", true, 15, "10 Punkte für das Stellen, 5 für das Verbellen."),
            new("Verhinderung eines Fluchtversuches", true, 10, "Wie SPr 2, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 15, "Wie SPr 2, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Rückentransport über ca. 30 Schritte, endet mit dem Beginn des Überfalls aus dem Rückentransport."),
            new("Überfall auf den Hund aus dem Rückentransport", true, 15, "Unmittelbar aus dem Rückentransport, ohne anzuhalten."),
            new("Angriff auf den Hund aus der Bewegung", true, 15, "Helfer läuft bis zur Mittellinie und greift frontal an, Freigabe bei ca. 50 Metern."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 15, "Erneuter Angriff im Anschluss, voller fester Griff."),
        ],
        Description: "FCI-Schutzdienstprüfung Stufe 3 (100 Punkte, bestanden ab 70).\n" +
            "Besteht nur aus der Abteilung C der FCI-IGP 3 (8 Übungen, 6 Verstecke, zusätzlich Überfall aus dem Rückentransport).\n" +
            "Die Stufe ist frei wählbar; reine Schutzdienstveranstaltungen sind nicht zulässig.\n" +
            "Kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        // GPr = Abteilungen B UND C der jeweiligen IGP-Stufe (200 Punkte).
        var gpr = await SeedSportAsync(db, "GPR", "FCI-Gebrauchshundeprüfung (GPr)",
        [
            new("Freifolge", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund folgt ohne Leine konzentriert in Grundstellung, inkl. Schussgleichgültigkeit und Personengruppe."),
            new("Sitz aus der Bewegung", ExerciseDifficulty.Beginner, "Abteilung B",
                "Hund setzt sich aus der Bewegung sofort und gerade hin."),
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Beginner, "Abteilung B",
                "Hund legt sich aus der Bewegung sofort hin und wird herangerufen."),
            new("Steh aus der Bewegung", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund bleibt auf Hörzeichen aus der Bewegung sofort und gerade stehen."),
            new("Bringen auf ebener Erde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund bringt das geworfene Bringholz zügig und übergibt es in der Grundstellung."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Sprünge über die 100 cm hohe Hürde, je nach Stufe mit oder ohne Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Klettersprünge über die Schrägwand (senkrechte Höhe 160 cm), je nach Stufe mit oder ohne Bringen."),
            new("Voraussenden mit Hinlegen", ExerciseDifficulty.Intermediate, "Abteilung B",
                "Hund läuft geradlinig mindestens 30 Schritte voraus und legt sich auf Hörzeichen ab."),
            new("Ablegen unter Ablenkung", ExerciseDifficulty.Advanced, "Abteilung B",
                "Hund bleibt während der Vorführung des anderen Hundes ruhig in der Ablage liegen."),
            new("Revieren", ExerciseDifficulty.Intermediate, "Abteilung C",
                "Hund durchsucht zielstrebig und konzentriert die Verstecke nach dem Helfer."),
            new("Stellen und Verbellen", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund stellt den Helfer selbstbewusst und verbellt ihn anhaltend, ohne zu beißen."),
            new("Verhinderung eines Fluchtversuches", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verhindert einen Fluchtversuch des Helfers durch energisches Zufassen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich gegen einen Angriff des Helfers durch festen, ruhigen Griff."),
            new("Rückentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund begleitet Hundeführer und Helfer beim Rückentransport aufmerksam."),
            new("Überfall auf den Hund aus dem Rückentransport", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich gegen den Überfall unmittelbar aus dem Rückentransport."),
            new("Angriff auf den Hund aus der Bewegung", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich gegen den frontalen Angriff mit vollem, ruhigem Griff."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", ExerciseDifficulty.Advanced, "Abteilung C",
                "Hund verteidigt sich am Ende des Schutzdienstes gegen einen erneuten Angriff."),
        ]);

        await SeedRegulationAsync(db, gpr, new RegulationSeed("FCI-GPr 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 15, "Mit Schussgleichgültigkeitsprüfung."),
            new("Sitz aus der Bewegung", true, 10, "Aus dem Normalschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus dem Normalschritt."),
            new("Bringen auf ebener Erde", true, 15, "Bringholz 650 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "2 Sprünge ohne Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", true, 15, "Ein Klettersprung ohne Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mindestens 30 Schritte voraus."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 10 Meter entfernt in Sichtweite, seitwärts zum Hund stehend."),
            new("Revieren", true, 5, "2 Verstecke."),
            new("Stellen und Verbellen", true, 15, "10 Punkte Stellen, 5 Punkte Verbellen."),
            new("Verhinderung eines Fluchtversuches", true, 20, "Energisches Verhindern der Flucht."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 30, "Voller, fester und ruhiger Griff."),
            new("Angriff auf den Hund aus der Bewegung", true, 30, "Aus ca. 20 Metern, mit Vertreibungslauten frontal."),
        ],
        Description: "FCI-Gebrauchshundeprüfung Stufe 1 (200 Punkte, bestanden ab 70 % je Abteilung).\n" +
            "Besteht aus den Abteilungen B und C der FCI-IGP 1 - ohne Fährte.\n" +
            "Die Stufe ist frei wählbar; kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, gpr, new RegulationSeed("FCI-GPr 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 15, "Mit größerer Ablenkung als GPr 1."),
            new("Sitz aus der Bewegung", true, 10, "Aus dem Normalschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus dem Normalschritt."),
            new("Steh aus der Bewegung", true, 10, "Aus dem Normalschritt mit Abholen des Hundes."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 1000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Ein Klettersprung ohne Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größerer Distanz als GPr 1."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mit dem Rücken zum Hund, mindestens 20 Meter entfernt in Sichtweite."),
            new("Revieren", true, 5, "4 Verstecke."),
            new("Stellen und Verbellen", true, 15, "10 Punkte Stellen, 5 Punkte Verbellen."),
            new("Verhinderung eines Fluchtversuches", true, 15, "Wie GPr 1, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 20, "Wie GPr 1, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Über ca. 30 Schritte, anschließend Seitentransport zum Leistungsrichter."),
            new("Angriff auf den Hund aus der Bewegung", true, 20, "Aus der Lauerstellung (ca. 30 Meter)."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 20, "Erneuter Angriff im Anschluss."),
        ],
        Description: "FCI-Gebrauchshundeprüfung Stufe 2 (200 Punkte, bestanden ab 70 % je Abteilung).\n" +
            "Besteht aus den Abteilungen B und C der FCI-IGP 2 - ohne Fährte.\n" +
            "Die Stufe ist frei wählbar; kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, gpr, new RegulationSeed("FCI-GPr 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 15, "Höchste Stufe, auch durch eine Personengruppe."),
            new("Sitz aus der Bewegung", true, 10, "Entwicklung von 10 bis 15 Schritten - in allen Stufen gleich, kein Laufschritt."),
            new("Ablegen in Verbindung mit Herankommen", true, 10, "Aus dem Laufschritt."),
            new("Steh aus der Bewegung", true, 10, "Aus dem Laufschritt mit Heranrufen des Hundes."),
            new("Bringen auf ebener Erde", true, 10, "Bringholz 2000 Gramm."),
            new("Freisprünge / Bringen über eine 1 Meter hohe Hürde", true, 15, "Hin- und Rücksprung mit Bringen."),
            new("Klettersprung / Bringen über die Schrägwand", true, 10, "Hin- und Rückklettersprung mit Bringen."),
            new("Voraussenden mit Hinlegen", true, 10, "Mit größter Distanz und Ablenkung der drei Stufen."),
            new("Ablegen unter Ablenkung", true, 10, "Hundeführer mindestens 30 Meter entfernt, außer Sicht des Hundes."),
            new("Revieren", true, 10, "6 Verstecke."),
            new("Stellen und Verbellen", true, 15, "10 Punkte Stellen, 5 Punkte Verbellen."),
            new("Verhinderung eines Fluchtversuches", true, 10, "Wie GPr 2, mit höheren Anforderungen."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)", true, 15, "Wie GPr 2, mit höheren Anforderungen."),
            new("Rückentransport", true, 5, "Über ca. 30 Schritte, endet mit dem Überfall aus dem Rückentransport."),
            new("Überfall auf den Hund aus dem Rückentransport", true, 15, "Unmittelbar aus dem Rückentransport, ohne anzuhalten."),
            new("Angriff auf den Hund aus der Bewegung", true, 15, "Helfer läuft bis zur Mittellinie, Freigabe bei ca. 50 Metern."),
            new("Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)", true, 15, "Erneuter Angriff im Anschluss."),
        ],
        Description: "FCI-Gebrauchshundeprüfung Stufe 3 (200 Punkte, bestanden ab 70 % je Abteilung).\n" +
            "Besteht aus den Abteilungen B und C der FCI-IGP 3 - ohne Fährte.\n" +
            "Die Stufe ist frei wählbar; kein Ausbildungstitel im Sinne der Ausstellungs- und Zuchtordnung.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 18 Monate."));

        // Stöberprüfung (UTI-REG-IGP-de-2025, S. 80-82): EINE Suchleistung,
        // bewertet über 5 feste Kriterien (20/20/10/10/40 = 100 Punkte) -
        // die Kriterien sind hier als Übungen modelliert, weil sie genau die
        // trainierbaren Aspekte der Stöberarbeit sind. Die Stufen
        // unterscheiden sich in Feldgröße, Gegenständen und Suchzeit.
        var stoepr = await SeedSportAsync(db, "STOEPR", "FCI-Stöberprüfung (StöPr)",
        [
            new("Führigkeit (Hör- und Sichtzeichen)", ExerciseDifficulty.Intermediate, "Stöbern",
                "Hund befolgt die Hör- und Sichtzeichen des Hundeführers sofort; der Hundeführer bewegt sich nur auf der gedachten Mittellinie."),
            new("Arbeitsintensität", ExerciseDifficulty.Intermediate, "Stöbern",
                "Entschlossenheit und Arbeitswille: konsequentes, ruhiges, flüssiges, selbstsicheres und freies Arbeiten ohne Stress."),
            new("Ausdauer der Sucharbeit", ExerciseDifficulty.Intermediate, "Stöbern",
                "Keine Unterbrechung der Sucharbeit, bis der Gegenstand gefunden ist; ausdauerndes und zielgerichtetes Arbeiten mit weiten Seitenschlägen."),
            new("Sucheinteilung und Lenken (Hundeführer)", ExerciseDifficulty.Intermediate, "Stöbern",
                "Verhalten des Hundeführers: sinnvolle Sucheinteilung und Lenken des Hundes von der Mittellinie aus."),
            new("Finden und Anzeigen der Gegenstände", ExerciseDifficulty.Advanced, "Stöbern",
                "Überzeugendes, sicheres Verweisen, Aufnehmen oder Apportieren der gefundenen Gegenstände; Gegenstand im Bereich bis 20 cm der Vorderpfoten."),
        ]);

        await SeedRegulationAsync(db, stoepr, new RegulationSeed("FCI-StöPr 1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Führigkeit (Hör- und Sichtzeichen)", true, 20, "Bewertungskriterium für die gesamte Suche."),
            new("Arbeitsintensität", true, 20, "Bewertungskriterium für die gesamte Suche."),
            new("Ausdauer der Sucharbeit", true, 10, "Bewertungskriterium für die gesamte Suche."),
            new("Sucheinteilung und Lenken (Hundeführer)", true, 10, "Bewertungskriterium für die gesamte Suche."),
            new("Finden und Anzeigen der Gegenstände", true, 40, "2 HF-eigene Gegenstände (10 x 3 x 0,5 cm, je 20 Punkte), 1 links / 1 rechts der Mittellinie."),
        ],
        Description: "FCI-Stöberprüfung Stufe 1 (100 Punkte, bestanden ab 70).\n" +
            "Stöberfeld: 20 x 30 m - Suchzeit: max. 10 Minuten.\n" +
            "Gegenstände: 2 HF-eigene Gegenstände (10 x 3 x 0,5 cm, unterschiedliches Material), je einer links und rechts der Mittellinie (je 20 Punkte).\n" +
            "Bewertung: Führigkeit (20), Arbeitsintensität (20), Ausdauer (10), Verhalten des Hundeführers (10), Finden der Gegenstände (40).\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 15 Monate."));

        await SeedRegulationAsync(db, stoepr, new RegulationSeed("FCI-StöPr 2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Führigkeit (Hör- und Sichtzeichen)", true, 20, "Bewertungskriterium für die gesamte Suche."),
            new("Arbeitsintensität", true, 20, "Bewertungskriterium für die gesamte Suche."),
            new("Ausdauer der Sucharbeit", true, 10, "Bewertungskriterium für die gesamte Suche."),
            new("Sucheinteilung und Lenken (Hundeführer)", true, 10, "Bewertungskriterium für die gesamte Suche."),
            new("Finden und Anzeigen der Gegenstände", true, 40, "4 Fremdgegenstände (10 x 3 x 0,5 cm, je 10 Punkte), 2 links / 2 rechts der Mittellinie."),
        ],
        Description: "FCI-Stöberprüfung Stufe 2 (100 Punkte, bestanden ab 70).\n" +
            "Stöberfeld: 20 x 40 m - Suchzeit: max. 12 Minuten.\n" +
            "Gegenstände: 4 Fremdgegenstände (10 x 3 x 0,5 cm, unterschiedliches Material), je 2 links und rechts der Mittellinie (je 10 Punkte).\n" +
            "Bewertung: Führigkeit (20), Arbeitsintensität (20), Ausdauer (10), Verhalten des Hundeführers (10), Finden der Gegenstände (40).\n" +
            "Startvoraussetzung: bestandene FCI-StöPr 1.\n" +
            "Mindestalter: 15 Monate."));

        await SeedRegulationAsync(db, stoepr, new RegulationSeed("FCI-StöPr 3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Führigkeit (Hör- und Sichtzeichen)", true, 20, "Bewertungskriterium für die gesamte Suche."),
            new("Arbeitsintensität", true, 20, "Bewertungskriterium für die gesamte Suche."),
            new("Ausdauer der Sucharbeit", true, 10, "Bewertungskriterium für die gesamte Suche."),
            new("Sucheinteilung und Lenken (Hundeführer)", true, 10, "Bewertungskriterium für die gesamte Suche."),
            new("Finden und Anzeigen der Gegenstände", true, 40, "5 Fremdgegenstände (kleiner: 5 x 3 x 0,5 cm, je 8 Punkte), beliebig ausgelegt."),
        ],
        Description: "FCI-Stöberprüfung Stufe 3 - höchste Stöberstufe (100 Punkte, bestanden ab 70).\n" +
            "Stöberfeld: 30 x 50 m - Suchzeit: max. 15 Minuten.\n" +
            "Gegenstände: 5 Fremdgegenstände (kleiner: 5 x 3 x 0,5 cm, unterschiedliches Material), beliebig ausgelegt (je 8 Punkte).\n" +
            "Bewertung: Führigkeit (20), Arbeitsintensität (20), Ausdauer (10), Verhalten des Hundeführers (10), Finden der Gegenstände (40).\n" +
            "Startvoraussetzung: bestandene FCI-StöPr 2.\n" +
            "Mindestalter: 15 Monate."));

        // IGP-FH (S. 70): an 2 Tagen je eine komplette FCI-IFH-3-Fährte auf
        // verschiedenem Gelände, von verschiedenen Fährtenlegern - gehört wie
        // die IFH-Stufen zur Sportart "Fährte" und nutzt deren Übungen.
        await SeedRegulationAsync(db, faerte, new RegulationSeed("FCI-IGP FH", "2025", new DateOnly(2025, 1, 1),
        [
            new("Fährtenarbeit", true, 79, "Je Fährte: Fremdfährte, min. 1800 Schritte, 8 Schenkel (einer als Halbkreis), 7 Winkel (2 spitz zwischen 30° und 60°), Fährtenalter min. 180 Minuten, Ausarbeitungszeit max. 45 Minuten."),
            new("Gegenstände verweisen", true, 21, "7 fremde Gegenstände je Fährte, je 3 Punkte."),
        ],
        Description: "FCI-IGP Fährtenhundprüfung - Königsklasse der Fährtenarbeit (2 x 100 Punkte).\n" +
            "An 2 Tagen muss jeweils eine FCI-IFH-3-Fährte bestanden werden - auf verschiedenem Gelände und von verschiedenen Fährtenlegern gelegt.\n" +
            "Bestanden: in beiden Fährten mindestens ein befriedigendes Ergebnis (70 Punkte).\n" +
            "Wertung: die höhere Einzelfährte zählt; bei Punktgleichheit gleiche Platzierung.\n" +
            "Startvoraussetzung: bestandene FCI-IFH 3.\n" +
            "Mindestalter: 20 Monate."));

        // Ausdauerprüfung (S. 83-84): keine Punkte, nur bestanden/nicht
        // bestanden - modelliert wie BH Teil B (MaxPoints 0).
        var iad = await SeedSportAsync(db, "IAD", "FCI-Ausdauerprüfung (IAD)",
        [
            new("Laufübung am Fahrrad", ExerciseDifficulty.Intermediate, "Ausdauer",
                "Hund läuft angeleint an der rechten Seite des Hundeführers in normalem Trab neben dem Fahrrad, ohne überhastetes Laufen oder ständiges Nachhängen."),
            new("Verfassungsprüfung in den Pausen", ExerciseDifficulty.Beginner, "Ausdauer",
                "Leistungsrichter kontrolliert in den Pausen Ermüdungserscheinungen und Pfoten; Hund kann sich frei und zwanglos bewegen."),
        ]);

        await SeedRegulationAsync(db, iad, new RegulationSeed("FCI-IAD", "2025", new DateOnly(2025, 1, 1),
        [
            new("Laufübung am Fahrrad", true, 0, "20 km Gesamtstrecke bei 12-15 km/h auf Straßen und Wegen: 8 km, Pause 15 Minuten, 7 km, Pause 20 Minuten, 5 km, Schlusspause 15 Minuten. Keine Punkte - nur bestanden/nicht bestanden."),
            new("Verfassungsprüfung in den Pausen", true, 0, "Kontrolle auf Ermüdung und wundgelaufene Pfoten in jeder Pause; übermüdete Hunde werden ausgeschlossen."),
        ],
        Description: "FCI-Ausdauerprüfung (IAD) - Nachweis körperlicher Fitness, keine Punktevergabe.\n" +
            "Strecke: 20 km bei 12-15 km/h am Fahrrad, auf Straßen und Wegen verschiedener Beschaffenheit.\n" +
            "Ablauf: 8 km - Pause 15 Min. - 7 km - Pause 20 Min. - 5 km - Schlusspause 15 Min. mit Verfassungsprüfung.\n" +
            "Bewertung: nur \"Bestanden\" / \"Nicht bestanden\".\n" +
            "Durchführung im Sommer nur früh vormittags oder spätnachmittags, Außentemperatur max. 22 °C.\n" +
            "Startvoraussetzung: FCI-BH/VT bzw. BH/VT (NPO).\n" +
            "Mindestalter: 16 Monate."));

        // ---------------------------------------------------------------
        // Turnierhundsport (VDH-Prüfungsordnung Turnierhundsport, gültig ab
        // 01.01.2025) - Leichtathletik mit Hund.
        //
        // Anders als die FCI-Prüfungen wird hier fast alles über die ZEIT
        // gewertet, nicht über Punkte: In den Sprint-Disziplinen entspricht
        // eine Laufsekunde einem Laufzeitpunkt, Fehler kommen als
        // Fehlerpunkte hinzu. Nur der Gehorsam des Vierkampfs hat echte
        // Übungspunkte (60). Die Sprint-Disziplinen stehen deshalb mit
        // MaxPoints 0 - wie schon BH Teil B und die IAD; ihre Bewertung
        // steht im Beschreibungstext.
        //
        // Nachgetragen am 2026-08-31 gegen das offizielle PDF (VDH-PO-THS,
        // gültig ab 01.01.2025), nachdem der Betreiber gemeldet hatte, dass
        // die Laufdisziplinen fehlen:
        // - Kapitel 9 "VDH-CaniCross" ist ein SAMMELBEGRIFF für Geländelauf,
        //   DogScooter und Bikejöring. Hier stand dafür eine einzige Übung
        //   "CaniCross" - zwei von drei Disziplinen fehlten damit ganz.
        // - Kapitel 4 "VDH-Sprint-Vierkampf" fehlte vollständig.
        // - Kapitel 2 "VDH-Vorprüfung" (VP-V, VP-CC, VP-VK) fehlte. Es war als
        //   "einmalige Zulassungshürde, kein Trainingsziel" abgetan - die
        //   VP-CC besteht aber aus acht konkret trainierbaren Aufgaben, und
        //   ohne bestandene VP-V darf gar kein Hund im THS starten.
        // Gleichzeitig korrigiert: die Bestehensregel des Vierkampfs (sie
        // greift nur, wenn BEIDE Grenzen gerissen werden), die 48 Punkte als
        // Aufstiegs- statt Bestehenskriterium, die Ausgangspunktzahl des VK3
        // und das Zusatzgerät des Shorty (Mühlacker Harfe, nicht Frankfurter
        // Kreisel).
        //
        // PARA-Klassen und Jedermann-/Fun-Klassen bleiben bewusst außen vor:
        // Varianten derselben Disziplinen.
        // ---------------------------------------------------------------
        var ths = await SeedSportAsync(db, "THS", "Turnierhundsport",
        [
            new("Leinenführigkeit", ExerciseDifficulty.Beginner, "Gehorsam",
                "Hund folgt an lockerer Leine mit dem Schulterblatt auf Kniehöhe des Hundeführers, auch bei Tempo- und Richtungswechseln."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Gehorsam",
                "Wie die Leinenführigkeit, jedoch ohne Leine - im Vierkampf die aufwendigste Gehorsamsaufgabe."),
            new("Sitz mit Abholen", ExerciseDifficulty.Beginner, "Gehorsam",
                "Hund setzt sich nach 10 bis 15 Schritten Entwicklung auf Hörzeichen sofort und bleibt sitzen, bis der Hundeführer ihn abholt."),
            new("Ablegen mit Herankommen", ExerciseDifficulty.Intermediate, "Gehorsam",
                "Hund legt sich nach 10 bis 15 Schritten Entwicklung ab und kommt auf Hörzeichen zügig und freudig zum Hundeführer."),
            new("Steh mit Herankommen", ExerciseDifficulty.Intermediate, "Gehorsam",
                "Hund bleibt nach 10 bis 15 Schritten Entwicklung stehen und kommt auf Hörzeichen zum Hundeführer."),
            new("Ablegen aus dem Laufschritt mit Herankommen", ExerciseDifficulty.Advanced, "Gehorsam",
                "Wie das Ablegen mit Herankommen, jedoch aus dem Laufschritt - deutlich schwerer, weil der Hund aus dem Tempo abstoppen muss."),
            new("Steh aus dem Laufschritt mit Herankommen", ExerciseDifficulty.Advanced, "Gehorsam",
                "Wie das Steh mit Herankommen, jedoch aus dem Laufschritt."),
            new("Hürdenlauf", ExerciseDifficulty.Intermediate, "Sprint",
                "60 m gemeinsam über vier 30 cm hohe Hürden, der Hund unmittelbar links vom Hundeführer. Wendestange im Uhrzeigersinn umlaufen."),
            new("Slalomlauf", ExerciseDifficulty.Intermediate, "Sprint",
                "Rund 55 m durch Start-, Ziel- und fünf Streckentore. Hund und Hundeführer müssen jedes Tor in Laufrichtung durchlaufen."),
            new("Hindernislauf", ExerciseDifficulty.Advanced, "Sprint",
                "75 m über acht Geräte - Hürde, Oxer, Tunnel, Laufdiel, Tonne, Durchsprunggerät, Hoch-Weit-Sprung, Hürde. Der Hundeführer läuft rechts neben der Bahn mit."),
            new("Frankfurter Kreisel", ExerciseDifficulty.Advanced, "Sprint",
                "Zusatzgerät des Combinations-Speed-Cups: der Hund umläuft den Kreisel, während der Hundeführer außen mitläuft."),
            new("Mühlacker Harfe", ExerciseDifficulty.Advanced, "Sprint",
                "Zusatzgerät des Combinations-Speed-Cups - eng gestellte Sprungfolge, die sauberes Timing zwischen Hund und Hundeführer verlangt."),
            new("Geländelauf", ExerciseDifficulty.Intermediate, "CaniCross",
                "Joggen oder Walken auf der vorgegebenen Strecke im Gelände. Der Hund läuft vorweg oder daneben und ist über eine Ruckdämpferleine (max. 2,50 m gedehnt) und ein Bauchgurtsystem mit dem Hundeführer verbunden; er gibt das Tempo vor."),
            new("DogScooter", ExerciseDifficulty.Advanced, "CaniCross",
                "Dieselbe Strecke mit dem Tretroller, der Hundeführer unterstützt durch Mittreten oder Mitlaufen. Ruckdämpferleine 2,50 bis 3,00 m, Helm, Schutzbrille und Handschuhe sind Pflicht, Radgröße mindestens 12 Zoll."),
            new("Bikejöring", ExerciseDifficulty.Advanced, "CaniCross",
                "Dieselbe Strecke mit dem geländetauglichen Fahrrad. Wegen der deutlich höheren Geschwindigkeit nur auf abgesperrten und gesicherten Strecken; Helm, Schutzbrille und Handschuhe sind Pflicht, Radgröße mindestens 26 Zoll."),
            new("Verträglichkeit im Begegnungsverkehr", ExerciseDifficulty.Beginner, "Vorprüfung",
                "Begegnungsverkehr außerhalb des Übungsplatzes. Maßgeblich ist der Gesamteindruck des Hundes - keine oder nur geringe Verträglichkeitsmängel."),
            new("Start unter Laufbereitschaft", ExerciseDifficulty.Beginner, "Vorprüfung",
                "Der Hund wartet mit hoher Laufbereitschaft auf den Start und bleibt dabei ansprechbar und dem Richter gegenüber neutral."),
            new("Hindernisse umfahren", ExerciseDifficulty.Beginner, "Vorprüfung",
                "Mit Pylonen aufgebaute Hindernisse werden umlaufen bzw. umfahren - das Team muss das Tempo drosseln können."),
            new("Begegnung der Teams", ExerciseDifficulty.Intermediate, "Vorprüfung",
                "Head-on-passing: zwei Teams laufen bzw. fahren auf getrennten Spuren aufeinander zu und passieren einander ohne Aggression."),
            new("Wenden", ExerciseDifficulty.Beginner, "Vorprüfung",
                "Ein mit Pylonen markierter Wendepunkt wird umlaufen bzw. umfahren; Roller oder Rad dürfen dabei geschoben werden."),
            new("Anhalten mit Begegnungsverkehr", ExerciseDifficulty.Intermediate, "Vorprüfung",
                "Die eigene Spur ist unterbrochen - das Team wartet ohne Aggression, bis das entgegenkommende Team passiert hat, und wechselt erst dann."),
            new("Slalom mit Richtungswechseln", ExerciseDifficulty.Beginner, "Vorprüfung",
                "Slalomstrecke mit mindestens vier sanften Richtungswechseln (max. 45 Grad, Stangenabstand mind. 20 m), zu bewältigen bei normaler Geschwindigkeit."),
            new("Ablenkung durch bellenden Hund", ExerciseDifficulty.Intermediate, "Vorprüfung",
                "Vorbeilaufen bzw. -fahren an einem am Streckenrand stehenden, im Idealfall bellenden Mensch-Hund-Team - ohne Aggressionsverhalten."),
            new("Abbiegen trotz Verleitung", ExerciseDifficulty.Advanced, "Vorprüfung",
                "Das Team biegt selbstständig vom Hauptweg auf den markierten kleineren Weg ab, obwohl dahinter ein anderes Mensch-Hund-Team zum Geradeausfahren verleitet."),
        ]);

        // Die Vorprüfungen sind keine Kür, sondern das Tor zum Sport: ohne
        // VDH-VP-V darf kein Hund im THS geführt werden, ohne VDH-VP-CC bzw.
        // VDH-VP-VK nicht in den jeweiligen Disziplinen. Sie werden nach
        // "Bestanden"/"Nicht bestanden" beurteilt, tragen also keine Punkte.
        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-VP-V", "2025", new DateOnly(2025, 1, 1),
        [
            new("Verträglichkeit im Begegnungsverkehr", true, 0, "Kein Punktesystem - maßgeblich ist der Gesamteindruck. Der Richter darf bei Zweifeln wiederholen oder variieren."),
        ],
        Description: "VDH-Verträglichkeitsprüfung - die einmalige Grundvoraussetzung für jeden Hund im Turnierhundsport.\n" +
            "Ablauf: Chipkontrolle durch den Richter, danach Begegnungsverkehr außerhalb des Übungsplatzes.\n" +
            "Bewertung: \"Bestanden\", wenn keine oder nur geringe Verträglichkeitsmängel festgestellt wurden. Bei Anzeichen von Aggression nicht bestanden.\n" +
            "Nach dem Bestehen darf der Hund sofort in weiteren Prüfungen derselben Veranstaltung geführt werden.\n" +
            "Alternativ anerkannt: FCI-BH/VT bzw. VDH-BH/VT. Hunde, die vor dem 01.01.2025 bereits im VDH-THS geführt wurden, haben Bestandsschutz."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-VP-CC", "2025", new DateOnly(2025, 1, 1),
        [
            new("Start unter Laufbereitschaft", false, 0, "Aufgabe 1. Der Richter prüft Ausrüstung, Startvorbereitung und Ansprechbarkeit."),
            new("Hindernisse umfahren", false, 0, "Aufgabe 2. Die Pylonen sind so gestellt, dass nur langsames Umfahren möglich ist."),
            new("Begegnung der Teams", false, 0, "Aufgabe 3 (Head-on-passing). Der Weg ist mittig getrennt, beidseitig bleibt Platz zum Ausweichen."),
            new("Wenden", false, 0, "Aufgabe 4. Im Fehlerfall einmal wiederholbar."),
            new("Anhalten mit Begegnungsverkehr", false, 0, "Aufgabe 5. Simuliert ein zu schmales Wegstück."),
            new("Slalom mit Richtungswechseln", false, 0, "Aufgabe 6. Im Fehlerfall einmal wiederholbar."),
            new("Ablenkung durch bellenden Hund", false, 0, "Aufgabe 7."),
            new("Abbiegen trotz Verleitung", false, 0, "Aufgabe 8."),
        ],
        Description: "VDH-Vorprüfung CaniCross - Sozialverträglichkeit in den CaniCross-Disziplinen.\n" +
            "Geprüft werden DREI der acht hier gelisteten Aufgaben; die Auswahl trifft der Richter je nach den örtlichen Gegebenheiten. Deshalb steht keine als Pflichtübung.\n" +
            "Vorgeführt wird in der Ausrüstung der Disziplin (Geländelauf, DogScooter oder Bikejöring).\n" +
            "Bestanden: keine oder nur geringe Verträglichkeitsmängel in allen drei Aufgaben. Ein wesentlicher Mangel in einer Aufgabe bedeutet \"nicht bestanden\".\n" +
            "Voraussetzung: bestandene VDH-VP-V. Pflicht für VDH-CC und den VDH-Sprint-Vierkampf."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-VP-VK", "2025", new DateOnly(2025, 1, 1),
        [
            new("Leinenführigkeit", true, 0, "Nach dem Laufschema des VDH-VK1, hier aber ohne Punktwertung."),
            new("Freifolge", true, 0, "Nach dem Laufschema des VDH-VK1, hier aber ohne Punktwertung."),
            new("Sitz mit Abholen", true, 0, "Nach dem Laufschema des VDH-VK1, hier aber ohne Punktwertung."),
            new("Ablegen mit Herankommen", true, 0, "Nach dem Laufschema des VDH-VK1, hier aber ohne Punktwertung."),
        ],
        Description: "VDH-Vorprüfung Vierkampf - die einmalige Zulassung zum VDH-Vierkampf.\n" +
            "Das Laufschema ist identisch mit dem Gehorsam des VDH-VK1, es werden aber keine Punkte vergeben.\n" +
            "Bestanden, wenn der Richter das Werturteil \"Bestanden\" vergibt.\n" +
            "Am selben Tag darf der Hund danach nicht mehr im VDH-VK geführt werden.\n" +
            "Voraussetzung: bestandene VDH-VP-V."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-VK1", "2025", new DateOnly(2025, 1, 1),
        [
            new("Leinenführigkeit", true, 15, "Nach festem Laufschema. Halbe Punkte sind möglich."),
            new("Freifolge", true, 20, "Nach festem Laufschema, ohne Leine."),
            new("Sitz mit Abholen", true, 10, "10 bis 15 Schritte Entwicklung vor dem Hörzeichen."),
            new("Ablegen mit Herankommen", true, 15, "10 bis 15 Schritte Entwicklung vor dem Hörzeichen."),
            new("Hürdenlauf", true, 0, "Zeitgewertet: eine Laufsekunde = ein Laufzeitpunkt. Unterlaufene Stange 4, abgeworfene Stange 2 Fehlerpunkte."),
            new("Slalomlauf", true, 0, "Zeitgewertet. Fehlerpunkte für ausgelassene oder falsch durchlaufene Tore."),
            new("Hindernislauf", true, 0, "Zeitgewertet. Fehlerpunkte je nicht bewältigtem Gerät."),
        ],
        Description: "VDH-Vierkampf 1 - Einstiegsstufe des Turnierhundsports.\n" +
            "Gehorsam (max. 60 Punkte): Leinenführigkeit 15, Freifolge 20, Sitz mit Abholen 10, Ablegen mit Herankommen 15.\n" +
            "Sprint-Disziplinen: Hürdenlauf (60 m), Slalomlauf (ca. 55 m), Hindernislauf (75 m) - je ein Durchgang, in Freifolge.\n" +
            "Bewertung: Gehorsamspunkte + 250 Ausgangspunkte der Sprint-Disziplinen abzüglich Laufzeiten und Fehlerpunkte.\n" +
            "Werturteil \"Ohne Bewertung\" gibt es nur, wenn BEIDES zutrifft: weniger als 42 Punkte im Gehorsam UND mehr als 18 Fehlerpunkte im Sport. Alle anderen Teams sind bestanden.\n" +
            "Mindestalter des Hundes: 15 Monate. Voraussetzung: bestandene VDH-VP-V und VDH-VP-VK."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-VK2", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 20, "Nach festem Laufschema, ohne Leine."),
            new("Sitz mit Abholen", true, 10, "10 bis 15 Schritte Entwicklung vor dem Hörzeichen."),
            new("Ablegen mit Herankommen", true, 15, "10 bis 15 Schritte Entwicklung vor dem Hörzeichen."),
            new("Steh mit Herankommen", true, 15, "Neu gegenüber VK1 - dafür entfällt die Leinenführigkeit."),
            new("Hürdenlauf", true, 0, "Zeitgewertet wie im VK1."),
            new("Slalomlauf", true, 0, "Zeitgewertet wie im VK1."),
            new("Hindernislauf", true, 0, "Zeitgewertet, mit gegenüber VK1 erhöhten Geräten."),
        ],
        Description: "VDH-Vierkampf 2 - Mittelstufe des Turnierhundsports.\n" +
            "Gehorsam (max. 60 Punkte): Freifolge 20, Sitz mit Abholen 10, Ablegen mit Herankommen 15, Steh mit Herankommen 15.\n" +
            "Gegenüber VK1: keine Leinenführigkeit mehr, dafür das Steh mit Herankommen.\n" +
            "Sprint-Disziplinen: Hürdenlauf, Slalomlauf, Hindernislauf - Ausgangspunktzahl 255.\n" +
            "Bestehensregel wie im VK1: \"Ohne Bewertung\" nur bei weniger als 42 Gehorsamspunkten UND mehr als 18 Fehlerpunkten.\n" +
            "Aufstieg aus dem VK1 hierher: mindestens 48 Punkte im Gehorsam und in derselben Prüfung eine Mindest-Gesamtpunktzahl - Jugend 230/235, Aktive 245/250, Senioren 235/240 (weiblich/männlich).\n" +
            "Mindestalter des Hundes: 15 Monate."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-VK3", "2025", new DateOnly(2025, 1, 1),
        [
            new("Freifolge", true, 20, "Nach festem Laufschema, ohne Leine."),
            new("Sitz mit Abholen", true, 10, "10 bis 15 Schritte Entwicklung vor dem Hörzeichen."),
            new("Ablegen aus dem Laufschritt mit Herankommen", true, 15, "Aus dem Laufschritt - der Hund muss aus dem Tempo abstoppen."),
            new("Steh aus dem Laufschritt mit Herankommen", true, 15, "Aus dem Laufschritt."),
            new("Hürdenlauf", true, 0, "Zeitgewertet wie im VK1."),
            new("Slalomlauf", true, 0, "Zeitgewertet wie im VK1."),
            new("Hindernislauf", true, 0, "Zeitgewertet, mit den höchsten Geräteeinstellungen."),
        ],
        Description: "VDH-Vierkampf 3 - höchste Stufe des Turnierhundsports.\n" +
            "Gehorsam (max. 60 Punkte): Freifolge 20, Sitz mit Abholen 10, Ablegen aus dem Laufschritt 15, Steh aus dem Laufschritt 15.\n" +
            "Gegenüber VK2: Ablegen und Steh werden aus dem Laufschritt verlangt.\n" +
            "Sprint-Disziplinen: Hürdenlauf, Slalomlauf, Hindernislauf - Ausgangspunktzahl 265.\n" +
            "Bestehensregel wie im VK1: \"Ohne Bewertung\" nur bei weniger als 42 Gehorsamspunkten UND mehr als 18 Fehlerpunkten.\n" +
            "Aufstieg aus dem VK2 hierher: mindestens 48 Punkte im Gehorsam und in derselben Prüfung eine Mindest-Gesamtpunktzahl - Jugend 240/245, Aktive 255/260, Senioren 245/250 (weiblich/männlich).\n" +
            "Mindestalter des Hundes: 15 Monate."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-S-VK", "2025", new DateOnly(2025, 1, 1),
        [
            new("Hürdenlauf", true, 0, "Ein Durchgang, nach den Regeln des VDH-VK3."),
            new("Slalomlauf", true, 0, "Ein Durchgang, nach den Regeln des VDH-VK3."),
            new("Hindernislauf", true, 0, "Ein Durchgang, nach den Regeln des VDH-VK3."),
            new("Geländelauf", true, 0, "1000 m. Zu Beginn (z.B. bei Hitze) oder - empfohlen - als Schlussdisziplin, dann auch als Verfolgungsrennen. Frühstart im Verfolgungsrennen: 10 Strafsekunden."),
        ],
        Description: "VDH-Sprint-Vierkampf - die drei Sprint-Disziplinen des VDH-VK3 plus 1000 m Geländelauf, ganz ohne Gehorsam.\n" +
            "Wird nur in EINER Leistungsklasse ausgetragen; je Disziplin ein Durchgang.\n" +
            "Bewertung: 265 Ausgangspunkte der Sprint-Disziplinen + 75 Ausgangspunkte für den 1000-m-Lauf, abzüglich Laufzeiten, Fehlerpunkten und der auf 1000 m umgerechneten Laufzeit.\n" +
            "Wird der Geländelauf als Verfolgungsrennen zum Abschluss gelaufen, entscheidet die Reihenfolge des Zieleinlaufs - je Sekunde Rückstand aus den Sprint-Disziplinen 10 Sekunden späterer Start (auf 1000 m Streckenlänge bezogen).\n" +
            "Es gelten die Regeln und Fehlerbewertungen des VDH-VK3 und des Geländelaufs.\n" +
            "Voraussetzung: bestandene VDH-VP-V und VDH-VP-CC."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-DK", "2025", new DateOnly(2025, 1, 1),
        [
            new("Hürdenlauf", true, 0, "Ein Durchgang, mit oder ohne Leine. In Freifolge gibt es 5 Bonuspunkte."),
            new("Slalomlauf", true, 0, "Ein Durchgang, mit oder ohne Leine. In Freifolge gibt es 5 Bonuspunkte."),
            new("Hindernislauf", true, 0, "Ein Durchgang, mit oder ohne Leine. In Freifolge gibt es 5 Bonuspunkte."),
        ],
        Description: "VDH-Dreikampf - die drei Sprint-Disziplinen des VDH-VK2 ohne Gehorsam.\n" +
            "Je ein Durchgang; der Hund darf mit oder ohne Leine geführt werden.\n" +
            "Bewertung: 240 Ausgangspunkte + Bonuspunkte (5 je in Freifolge gezeigter Disziplin) abzüglich Laufzeiten und Fehlerpunkte.\n" +
            "Der Einstieg für alle, denen der Gehorsamsteil des Vierkampfs (noch) zu viel ist."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-HL", "2025", new DateOnly(2025, 1, 1),
        [
            new("Hindernislauf", true, 0, "Zwei Durchgänge über die Bahn des VDH-VK1. Je mit Leine gezeigtem Durchgang 5 Strafsekunden."),
        ],
        Description: "VDH-Hindernislauf als eigenständige Disziplin - der Hindernislauf des VDH-VK1 in zwei Durchgängen.\n" +
            "Der Hund darf mit oder ohne Leine geführt werden; jeder Durchgang mit Leine kostet 5 Strafsekunden.\n" +
            "Bewertung: Summe der Laufzeiten + Strafsekunden + Fehlerpunkte - die niedrigste Gesamtzeit gewinnt.\n" +
            "Das Ergebnis wird nicht in den VDH-Leistungsnachweis eingetragen."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-CSC", "2025", new DateOnly(2025, 1, 1),
        [
            new("Slalomlauf", true, 0, "Sektion 1 des Staffelparcours."),
            new("Hürdenlauf", true, 0, "Sektion 2: drei 30 cm hohe Hürden nach der Wendestange, gemeinsam mit dem Hundeführer zu überspringen."),
            new("Hindernislauf", true, 0, "Sektion 3 des Staffelparcours."),
            new("Frankfurter Kreisel", true, 0, "Zusatzgerät des CSC gegenüber dem Vierkampf."),
            new("Mühlacker Harfe", true, 0, "Zusatzgerät des CSC gegenüber dem Vierkampf."),
        ],
        Description: "VDH-Combinations-Speed-Cup - Staffellauf aus den drei Laufelementen des Vierkampfs.\n" +
            "Eine Mannschaft besteht aus drei Teilnehmern mit drei verschiedenen Hunden, die den in drei Sektionen geteilten Parcours als Staffel durchlaufen.\n" +
            "Zwei Durchgänge, durchgehend in Freifolge.\n" +
            "Zusätzlich zu den Vierkampf-Geräten kommen Frankfurter Kreisel und Mühlacker Harfe hinzu.\n" +
            "Auch als Einzel-CSC möglich (ein Team läuft alle drei Sektionen) - dann ohne Eintrag in den Leistungsnachweis."));

        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-SH", "2025", new DateOnly(2025, 1, 1),
        [
            new("Hürdenlauf", true, 0, "Element der verkürzten CSC-Bahn."),
            new("Hindernislauf", true, 0, "Element der verkürzten CSC-Bahn."),
            new("Mühlacker Harfe", true, 0, "Zusatzgerät der Shorty-Bahn - zusammen mit den oberen Wertungsstangen des Oxers."),
        ],
        Description: "VDH-Shorty - Kurzbahn-Variante des Combinations-Speed-Cups mit zwei Sektionen.\n" +
            "Zwei Teams laufen die Staffel über zwei unterschiedliche Sektionen; die Hunde überwinden Hindernisse des VDH-VK1-3 zuzüglich der oberen Wertungsstangen des Oxers und der Mühlacker Harfe, der Hundeführer läuft unmittelbar rechts daneben mit.\n" +
            "Zwei Durchgänge. Beide Sektionen dürfen mit oder ohne Leine geführt werden; je mit Leine gezeigter Sektion 5 Strafsekunden.\n" +
            "Geräteanordnung und Ablauf sind bindend vorgegeben; Startfreigabe, Wechsel, Frühstart, Fehlerbewertung und Platzierung sind identisch zum VDH-CSC.\n" +
            "Der Einstieg in den Staffelgedanken auf kürzerer Strecke."));

        // VDH-CC ist laut PO der Sammelbegriff für DREI Disziplinen (Kapitel 9:
        // Geländelauf, DogScooter, Bikejöring) - vorher stand hier eine einzige
        // Übung "CaniCross". Genau das war die vom Nutzer gemeldete Lücke.
        // Keine der drei ist Pflicht: gestartet wird in einer davon.
        await SeedRegulationAsync(db, ths, new RegulationSeed("VDH-CC", "2025", new DateOnly(2025, 1, 1),
        [
            new("Geländelauf", false, 0, "VDH-GL. Laufen oder Walken; Einzel-, Massen- oder Verfolgungsstart sowie Staffel möglich. Empfohlener Startabstand im Einzelstart 30 Sekunden."),
            new("DogScooter", false, 0, "VDH-DS. Nur Einzelstart, Verfolgungsrennen oder Staffel - kein Massenstart. Empfohlener Startabstand 1 Minute."),
            new("Bikejöring", false, 0, "VDH-BJ. Nur auf abgesperrten Strecken; Wege höchstens zu 10 % asphaltiert. Empfohlener Startabstand mindestens 1 Minute."),
        ],
        Description: "VDH-CaniCross - Sammelbegriff für die drei Zugsport-Disziplinen: Geländelauf (VDH-GL), DogScooter (VDH-DS) und Bikejöring (VDH-BJ).\n" +
            "Ein Team besteht immer aus einem Hundeführer und einem Hund; der Hund muss durchgehend angeleint sein (Ableinen = Disqualifikation) und gibt das Tempo vor.\n" +
            "Streckenkategorien: Sprintstrecke 400 bis 1000 m, Kurzstrecke über 1000 bis 4000 m, Langstrecke über 4000 m (max. 8000 m).\n" +
            "Reine Zeitwertung auf Sekunden genau; im Leistungsnachweis wird die Streckenlänge mitgeführt (z.B. VDH-GL-1000, VDH-BJ-1250).\n" +
            "Mindestalter Hundeführer: Geländelauf 6 (Sprint) / 8 (Kurz) / 11 Jahre (Lang), DogScooter und Bikejöring 15 Jahre.\n" +
            "Mindestalter Hund: 15 Monate, ab 5 km Langstrecke sowie im DogScooter und Bikejöring 18 Monate. Höchstens 8 km je Wettkampftag.\n" +
            "Ab 15 °C darf der Richter die Langstrecke kürzen oder streichen, ab 20 °C auf maximal 2000 m verkürzen.\n" +
            "Voraussetzung: bestandene VDH-VP-V und VDH-VP-CC."));

        // ---------------------------------------------------------------
        // Agility (VDH-Prüfungsordnung Agility, gültig ab 01.01.2026, als
        // nationale Ergänzung zur FCI-Wettkampfordnung Agility).
        //
        // Auch hier gibt es KEINE Übungspunkte: gewertet wird über
        // Fehlerpunkte (alles in Fünfer-Schritten) und Zeitfehler gegen die
        // Standardzeit. Die "Übungen" sind deshalb die Geräte - genau das,
        // was man einzeln trainiert und im Tagebuch festhält.
        //
        // Geräteliste nach der aktuellen FCI-Wettkampfordnung (vom FCI-
        // Vorstand im Mai 2025 beschlossen): der Tisch und der Stofftunnel
        // sind nicht mehr dabei. Ältere deutschsprachige Fassungen führen
        // beide noch - wer danach seedet, trägt abgeschaffte Geräte ein.
        // ---------------------------------------------------------------
        var agility = await SeedSportAsync(db, "AGILITY", "Agility",
        [
            new("Hürde", ExerciseDifficulty.Beginner, "Sprünge",
                "Einfachsprung mit lose aufgelegter Stange. Ein Standard-Parcours enthält mindestens 14 Hürden."),
            new("Doppelsprung (Spread)", ExerciseDifficulty.Intermediate, "Sprünge",
                "Zwei zusammengestellte Hürden. In der Prüfungsstufe 1 nicht erlaubt."),
            new("Mauer", ExerciseDifficulty.Beginner, "Sprünge",
                "Sprung über eine geschlossene Fläche mit abnehmbaren Elementen auf der Oberkante."),
            new("Reifen", ExerciseDifficulty.Intermediate, "Sprünge",
                "Sprung durch den Reifen. Muss immer gerade angelaufen werden können."),
            new("Weitsprung", ExerciseDifficulty.Intermediate, "Sprünge",
                "Mehrere flache Elemente hintereinander. Muss immer gerade angelaufen werden können."),
            new("Laufsteg", ExerciseDifficulty.Intermediate, "Kontaktzonengeräte",
                "120 bis 135 cm hoch. Die farbigen Kontaktzonen müssen mit mindestens einer Pfote berührt werden."),
            new("Wippe", ExerciseDifficulty.Advanced, "Kontaktzonengeräte",
                "Der Hund darf die Wippe erst verlassen, wenn sie den Boden berührt hat."),
            new("Schrägwand (A-Wand)", ExerciseDifficulty.Intermediate, "Kontaktzonengeräte",
                "Zwei zu einem A gestellte Wandteile, mit Kontaktzonen an beiden Enden."),
            new("Slalom", ExerciseDifficulty.Advanced, "Sonstige",
                "Der erste Stab bleibt links vom Hund. Jeder falsche Eintritt ist eine Verweigerung; weitere Slalomfehler werden insgesamt nur einmal geahndet."),
            new("Tunnel", ExerciseDifficulty.Beginner, "Sonstige",
                "Fester Tunnel. Bis zu vier Stück im Parcours, einer davon 3 bis 4 Meter lang."),
        ]);

        await SeedRegulationAsync(db, agility, new RegulationSeed("Agility 0 (A0)", "2026", new DateOnly(2026, 1, 1),
        [
            new("Hürde", true, 0, "Fehlerbewertung: Abwurf 5 Fehlerpunkte."),
            new("Mauer", true, 0, "Fehlerbewertung: Abwurf 5 Fehlerpunkte."),
            new("Reifen", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Weitsprung", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Laufsteg", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte je Vorkommnis."),
            new("Wippe", true, 0, "Verlässt der Hund die Wippe vor Bodenkontakt: 5 Fehlerpunkte."),
            new("Schrägwand (A-Wand)", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte je Vorkommnis."),
            new("Slalom", true, 0, "Falscher Eintritt = Verweigerung. Weitere Slalomfehler werden zusammen nur einmal mit 5 Fehlerpunkten geahndet."),
            new("Tunnel", true, 0, "Verweigerung 5 Fehlerpunkte."),
        ],
        Description: "Agility 0 - nationale Einstiegsklasse des VDH (in der FCI gibt es sie nicht).\n" +
            "Bewertung: Fehlerpunkte in Fünfer-Schritten (Abwurf, Verweigerung, verfehlte Kontaktzone je 5) zuzüglich Zeitfehler.\n" +
            "Zeitfehler: je Sekunde über der Standardzeit ein Fehlerpunkt, Zehntel und Hundertstel anteilig.\n" +
            "Aufstieg nach A1: dreimal fehlerfrei (0,00 Fehlerpunkte) oder zweimal fehlerfrei und zweimal bis 5,00 Fehlerpunkte - nur A-Läufe zählen.\n" +
            "Im Ausland erzielte A0-Ergebnisse werden nicht anerkannt, da A0 eine reine VDH-Klasse ist.\n" +
            "Größenklassen: S bis 34,99 cm, M ab 35 cm, I ab 43 cm, L ab 48 cm Widerristhöhe.\n" +
            "Mindestalter: 18 Monate. Voraussetzung: FCI-BH/VT + Sachkundenachweis."));

        await SeedRegulationAsync(db, agility, new RegulationSeed("Agility 1 (A1)", "2026", new DateOnly(2026, 1, 1),
        [
            new("Hürde", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Mauer", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Reifen", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Weitsprung", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Laufsteg", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte."),
            new("Wippe", true, 0, "Verlässt der Hund die Wippe vor Bodenkontakt: 5 Fehlerpunkte."),
            new("Schrägwand (A-Wand)", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte."),
            new("Slalom", true, 0, "Falscher Eintritt = Verweigerung."),
            new("Tunnel", true, 0, "Verweigerung 5 Fehlerpunkte."),
        ],
        Description: "Agility 1 - erste FCI-Prüfungsstufe.\n" +
            "Der Doppelsprung ist in dieser Stufe nicht erlaubt.\n" +
            "Bewertung: Fehlerpunkte in Fünfer-Schritten zuzüglich Zeitfehler gegen die Standardzeit.\n" +
            "Aufstieg nach A2: dreimal eine Platzierung (Platz 1-3) mit 0,00 Fehlerpunkten im A-Lauf, unter mindestens zwei verschiedenen Richtern.\n" +
            "Ein Verbleib in A1 ist freiwillig möglich, ohne dass erlaufene Qualifikationen verfallen.\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, agility, new RegulationSeed("Agility 2 (A2)", "2026", new DateOnly(2026, 1, 1),
        [
            new("Hürde", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Doppelsprung (Spread)", true, 0, "Ab dieser Stufe zugelassen. Abwurf 5 Fehlerpunkte."),
            new("Mauer", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Reifen", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Weitsprung", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Laufsteg", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte."),
            new("Wippe", true, 0, "Verlässt der Hund die Wippe vor Bodenkontakt: 5 Fehlerpunkte."),
            new("Schrägwand (A-Wand)", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte."),
            new("Slalom", true, 0, "Falscher Eintritt = Verweigerung."),
            new("Tunnel", true, 0, "Verweigerung 5 Fehlerpunkte."),
        ],
        Description: "Agility 2 - zweite FCI-Prüfungsstufe.\n" +
            "Mindestlaufgeschwindigkeit: 3,25 m/s im A-Lauf, 3,75 m/s im Jumping.\n" +
            "Aufstieg nach A3: fünfmal eine Platzierung (Platz 1-3) mit 0,00 Fehlerpunkten unter mindestens zwei verschiedenen Richtern, davon mindestens dreimal im A-Lauf.\n" +
            "Ein freiwilliger Abstieg nach A1 ist jederzeit möglich und wird im Leistungsnachweis dokumentiert.\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, agility, new RegulationSeed("Agility 3 (A3)", "2026", new DateOnly(2026, 1, 1),
        [
            new("Hürde", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Doppelsprung (Spread)", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Mauer", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Reifen", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Weitsprung", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Laufsteg", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte."),
            new("Wippe", true, 0, "Verlässt der Hund die Wippe vor Bodenkontakt: 5 Fehlerpunkte."),
            new("Schrägwand (A-Wand)", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte."),
            new("Slalom", true, 0, "Falscher Eintritt = Verweigerung."),
            new("Tunnel", true, 0, "Verweigerung 5 Fehlerpunkte."),
        ],
        Description: "Agility 3 - höchste FCI-Prüfungsstufe.\n" +
            "Mindestlaufgeschwindigkeit: 4,0 m/s im A-Lauf, 4,25 m/s im Jumping 3; Aufrechnungsfaktor 1,2.\n" +
            "Verbleib in A3: im vergangenen Kalenderjahr mindestens drei fehlerfreie Ergebnisse (0,00 Fehlerpunkte), davon mindestens eines im A-Lauf.\n" +
            "Hündinnen mit Ausfallzeit durch Wurf oder Belegung sind von dieser Nachweispflicht befreit.\n" +
            "Ein freiwilliger Abstieg nach A2 ist jederzeit möglich.\n" +
            "Mindestalter: 18 Monate."));

        await SeedRegulationAsync(db, agility, new RegulationSeed("Jumping (JP0-JP3)", "2026", new DateOnly(2026, 1, 1),
        [
            new("Hürde", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Doppelsprung (Spread)", true, 0, "Ab JP2. Abwurf 5 Fehlerpunkte."),
            new("Mauer", true, 0, "Abwurf 5 Fehlerpunkte."),
            new("Reifen", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Weitsprung", true, 0, "Verweigerung 5 Fehlerpunkte."),
            new("Slalom", true, 0, "Falscher Eintritt = Verweigerung."),
            new("Tunnel", true, 0, "Verweigerung 5 Fehlerpunkte."),
        ],
        Description: "Jumping - Parcours OHNE Kontaktzonengeräte, also ohne Laufsteg, Wippe und Schrägwand.\n" +
            "Wird in allen Prüfungsstufen als JP0 bis JP3 gelaufen, parallel zur jeweiligen A-Klasse.\n" +
            "Dadurch schneller als der A-Lauf - entsprechend höher liegen die Mindestlaufgeschwindigkeiten.\n" +
            "Für den Aufstieg zählen in der Regel nur die A-Läufe; Jumping-Ergebnisse zählen ab A2 anteilig mit.\n" +
            "Mindestalter: 18 Monate."));

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

        // Fährte A/B/C waren als "vereinsinterne Trainingsstufen" mit teils
        // falschen Werten hinterlegt (Fährte B als Eigenfährte statt
        // Fremdfährte, falsche Schenkel-/Winkel-/Gegenstandszahlen). Es sind
        // in Wahrheit die Fährten der FCI-IGP 1-3, die sich auch einzeln
        // laufen lassen (dann als FCI-FPr 1-3) - korrigiert am 2026-08-19 und
        // in "IGP 1/2/3 - Fährte" umbenannt.
        // Zugleich umbenannt: "Fährte A/B/C" sagte niemandem, worum es geht.
        // "Fährte C (Fremdfährte)" war zusätzlich ein Doppeleintrag zu
        // "Fährte C" und erzeugte eine zweite, gleichlautende Seite.
        foreach (var veraltet in new[] { "Fährte A", "Fährte B", "Fährte C", "Fährte C (Fremdfährte)" })
            await RemoveRegulationAsync(db, faerte, veraltet);

        // Bewusst KEIN RemoveOrphanedExercisesAsync für "Fährte": "Winkelarbeit"
        // und "Fährtenaufnahme" zählen nicht mehr zur Prüfung, sind zum
        // Trainieren aber weiterhin sinnvoll - erfunden war die Punktzahl,
        // nicht die Übung.
        await RemoveOrphanedExercisesAsync(db, bh);
        await RemoveOrphanedExercisesAsync(db, ibgh1);
        await RemoveOrphanedExercisesAsync(db, ibgh2);
        await RemoveOrphanedExercisesAsync(db, ibgh3);
        await RemoveOrphanedExercisesAsync(db, igp1);
        await RemoveOrphanedExercisesAsync(db, igp2);
        await RemoveOrphanedExercisesAsync(db, igp3);

        // "IGP 1/2/3 - Fährte" und "FCI-FPr 1-3" waren DIESELBE Prüfung unter
        // zwei Namen (PO S. 68: "Die Fährtenprüfungen bestehen nur aus der
        // Abteilung A der entsprechenden Prüfungsstufen") - mit identischen
        // Übungen und Punkten, doppelt zu pflegen und doppelt in der Sitemap.
        // Es bleibt die FPr: Das ist der Name, unter dem die Prüfung
        // tatsächlich gelaufen wird; "IGP x - Fährte" war eine Erfindung
        // dieses Projekts. Der IGP-Bezug steht jetzt in der Beschreibung der
        // FPr, damit man sie auch darüber findet.
        //
        // RemoveRegulationAsync bricht ab, sobald ein Trainingsziel darauf
        // verweist - ein stillschweigend gelöschtes Ziel wäre schlimmer als
        // ein überzähliger Katalogeintrag.
        foreach (var doppelt in new[] { "IGP 1 - Fährte", "IGP 2 - Fährte", "IGP 3 - Fährte" })
            await RemoveRegulationAsync(db, faerte, doppelt);
        await RemoveOrphanedExercisesAsync(db, faerte);
        // Räumt die abgelöste Sammel-Übung "CaniCross" weg, sobald keine
        // Trainingsdaten mehr daran hängen - an ihre Stelle sind Geländelauf,
        // DogScooter und Bikejöring getreten.
        await RemoveOrphanedExercisesAsync(db, ths);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Benennt eine Übung IN DER BESTEHENDEN ZEILE um (gleiche Id).
    ///
    /// Der Seeder findet Übungen über ihren Namen: Stünde in der Seed-Liste
    /// einfach der neue Name, legte er eine ZWEITE Übung an und ließe die alte
    /// verwaist zurück - mitsamt allen Trainingseinträgen, Bewertungen,
    /// Wiedervorlage-Ständen und Plan-Zielen, die daran hängen. Deshalb wird
    /// vor dem Seeden umbenannt.
    ///
    /// Muss VOR <see cref="SeedSportAsync"/> derselben Sportart laufen. Auf
    /// einer frischen Datenbank passiert nichts (die Sportart gibt es noch
    /// nicht) - dort entsteht die Übung gleich unter dem neuen Namen.
    /// Idempotent: läuft nur, solange der alte Name existiert und der neue
    /// noch nicht.
    /// </summary>
    private static async Task RenameExerciseAsync(ApplicationDbContext db, string sportCode, string oldName, string newName)
    {
        var sport = await db.Sports.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Code == sportCode);
        if (sport is null) return;

        var existing = await db.Exercises.FirstOrDefaultAsync(e => e.SportId == sport.Id && e.Name == oldName);
        if (existing is null) return;

        // Gäbe es den neuen Namen schon, entstünde ein Duplikat - dann lieber
        // nichts tun und die alte Zeile stehen lassen, als zwei Übungen mit
        // demselben Namen zu erzeugen.
        if (await db.Exercises.AnyAsync(e => e.SportId == sport.Id && e.Name == newName)) return;

        existing.Name = newName;
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

        // Die Position im Array IST die Reihenfolge der Prüfungsordnung (Abt. A
        // vor B vor C, innerhalb einer Abteilung Übung 1 bis n) - sie wird
        // deshalb als SortOrder mitgeschrieben und bei jedem Seed-Durchlauf
        // nachgepflegt. Ohne das lieferte die Datenbank die Übungen in
        // beliebiger Reihenfolge aus (siehe SportCatalogService).
        for (var index = 0; index < seed.Exercises.Length; index++)
        {
            var exerciseSeed = seed.Exercises[index];
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

            // Auch entfernte Zeilen ansehen: das Entfernen einer Übung aus einer
            // Prüfungsordnung ist ein Soft-Delete, der eindeutige Index kennt
            // aber kein DeletedAt. Ohne das legte der Seeder hier eine zweite
            // Zeile mit derselben Kombination an - die Datenbank weist sie
            // zurück, und weil der Seeder VOR app.Run() läuft, käme die Instanz
            // nach einem von Hand entfernten Seed-Eintrag gar nicht mehr hoch.
            var regulationExercise = await db.FindLinkIncludingRemovedAsync(version.Id, exercise.Id);
            if (regulationExercise is null)
            {
                db.RegulationExercises.Add(new RegulationExercise
                {
                    RegulationVersionId = version.Id,
                    ExerciseId = exercise.Id,
                    IsMandatory = exerciseSeed.IsMandatory,
                    MaxPoints = exerciseSeed.MaxPoints,
                    ScoringNotes = exerciseSeed.ScoringNotes,
                    SortOrder = index
                });
            }
            else
            {
                // Werte aus späteren Seed-Durchläufen nachpflegen (z.B.
                // korrigierte Punktzahlen) - vorher wurden bestehende
                // Zeilen nie aktualisiert, nur fehlende neu angelegt, eine
                // Korrektur landete dadurch nie in bereits gestarteten
                // lokalen Entwicklungsdatenbanken.
                // Wiederbeleben, falls die Zeile von Hand entfernt wurde: der
                // Seed ist für den globalen Katalog die Quelle der Wahrheit,
                // und die Übung steht ja wieder darin.
                regulationExercise.DeletedAt = null;
                regulationExercise.IsMandatory = exerciseSeed.IsMandatory;
                regulationExercise.MaxPoints = exerciseSeed.MaxPoints;
                regulationExercise.ScoringNotes = exerciseSeed.ScoringNotes;
                regulationExercise.SortOrder = index;
            }
        }

        await db.SaveChangesAsync();

        // Übungen entfernen, die NICHT MEHR im Seed stehen. Ohne das bliebe eine
        // gestrichene Übung für immer an der Prüfungsordnung hängen: Der Seed
        // legte bisher nur an und pflegte nach, räumte aber nie auf - eine
        // Korrektur wie "die BH hat keine eigenständig bewertete Freifolge"
        // käme in bereits laufenden Datenbanken (Test, Produktion) nie an.
        //
        // Entfernt wird nur die VERKNÜPFUNG zur Prüfungsordnung, nicht die
        // Übung selbst: Trainingseinträge, Bewertungen und Trainingspläne
        // verweisen auf Exercise, nicht auf RegulationExercise. Vorhandene
        // Aufzeichnungen bleiben damit unangetastet, die Übung bleibt
        // trainierbar - sie zählt nur nicht mehr zur Prüfung.
        var seededExerciseNames = seed.Exercises.Select(e => e.ExerciseName).ToList();
        var obsolete = await db.RegulationExercises
            .Where(re => re.RegulationVersionId == version.Id)
            .Include(re => re.Exercise)
            .Where(re => !seededExerciseNames.Contains(re.Exercise!.Name))
            .ToListAsync();

        if (obsolete.Count > 0)
        {
            db.RegulationExercises.RemoveRange(obsolete);
            await db.SaveChangesAsync();
        }
    }

    // Entfernt eine durch eine neuere, korrigierte Version abgelöste,
    // fehlerhafte RegulationVersion (siehe PRUEFUNGSORDNUNG_UPDATE.md
    // "Versions-Supersession") - Cascade-Delete entfernt automatisch deren
    // RegulationExercise-Zeilen. Von der neuen Version weiterhin genutzte,
    // gemeinsame Exercise-Zeilen (z.B. "Freifolge", die in alter wie neuer
    // Version vorkommt) bleiben unberührt, da nur die JOIN-Zeile der alten
    // Version gelöscht wird, nicht die Übung selbst.
    /// <summary>
    /// Entfernt eine komplette Prüfungsordnung samt ihrer Versionen - für
    /// Doppel- oder Fehleinträge, die es gar nicht geben sollte.
    ///
    /// Abgebrochen wird, sobald ein Trainingsziel darauf verweist: ein
    /// stillschweigend gelöschtes Ziel wäre für den Nutzer schlimmer als ein
    /// überzähliger Katalogeintrag.
    /// </summary>
    private static async Task RemoveRegulationAsync(ApplicationDbContext db, Sport sport, string regulationName)
    {
        var regulation = await db.Regulations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.SportId == sport.Id && r.Name == regulationName);
        if (regulation is null) return;

        var versions = await db.RegulationVersions
            .Where(v => v.RegulationId == regulation.Id)
            .ToListAsync();
        var versionIds = versions.Select(v => v.Id).ToList();

        if (await db.Goals.AnyAsync(g => g.RegulationId == regulation.Id)) return;

        var exercises = await db.RegulationExercises
            .Where(re => versionIds.Contains(re.RegulationVersionId))
            .ToListAsync();

        db.RegulationExercises.RemoveRange(exercises);
        db.RegulationVersions.RemoveRange(versions);
        db.Regulations.Remove(regulation);
        await db.SaveChangesAsync();
    }

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

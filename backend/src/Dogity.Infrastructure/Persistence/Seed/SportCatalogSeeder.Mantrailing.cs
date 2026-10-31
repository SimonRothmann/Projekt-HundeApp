using Dogity.Domain.Sports;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Mantrailing (Paket M1, 2026-10-30): die Prüfungsordnungen des DVG (MT-I bis MT-III) und des
/// SV (SV-MT 1 bis 3). Eine Sportart <c>MANTRAILING</c> für alle sechs Ordnungen.
///
/// Die Übungen sind die Bewertungsbereiche der Ordnungen. Sie sind je Verband eigene, weil die
/// Bereiche verschieden zugeschnitten sind (DVG: sieben Bereiche, SV: sechs, teils
/// zusammengefasst); innerhalb eines Verbands tragen die drei Stufen dieselben Bereiche mit
/// denselben Höchstpunkten. Gewertet wird wie bei BH und IGP über Punkte (100 je Stufe).
///
/// Quellen und Fassungen (siehe docs/PO_VERIFIKATION.md, Abschnitt "Mantrailing"): Der DVG
/// untersagt die Veröffentlichung seines Regelwerks ohne Genehmigung. Hinterlegt sind deshalb nur
/// Fakten (Stufen, Bewertungsbereiche mit Punkten, Länge, Trailalter, Gelände, Richtungswechsel,
/// Suchzeit, Zulassung) und eigene Beschreibungen - keine Textpassagen. Für den SV gilt das
/// ebenso, obwohl das Dokument frei zugänglich ist.
///
/// Land: beide Verbände sind deutsche Verbände, die Ordnungen gelten also für Deutschland ("DE",
/// siehe <see cref="FciKatalog.GeltungsbereichVon"/>). Eine FCI-Ordnung für Mantrailing führt der
/// Katalog nicht: Der SV verweist zwar auf die FCI-PO-SAR (RH MT), sie ist aber kein Gegenstand
/// dieses Pakets. Englische Namen gibt es deshalb nicht - der Name "Mantrailing" und die Kürzel
/// der Ordnungen sind in beiden Sprachen gleich.
/// </summary>
public static partial class SportCatalogSeeder
{
    /// <summary>Sportart-Code von Mantrailing - auch das Frontend (lib/mantrailing.ts) und die Fährten-Liste der Erinnerungen kennen ihn.</summary>
    public const string MantrailingCode = "MANTRAILING";

    private const string QuelleDvgMantrailing = "https://dvg-hundesport.de/file/aa81840850cc35dc0151686954f41767.de.0/dvg_po_mantrailing.pdf";

    private const string QuelleSvMantrailing = "https://www.schaeferhunde.de/index.php?eID=dumpFile&f=6342&t=f&token=7088031d8c791c82cd9357f5df9acb9c5a3c2868";

    private static async Task SeedMantrailingAsync(ApplicationDbContext db)
    {
        var mantrailing = await SeedSportAsync(db, MantrailingCode, "Mantrailing",
        [
            // ---- DVG ----
            new("Aufnahme vom Geruch", ExerciseDifficulty.Beginner, "Ansatz",
                "Wie der Hund am Start den Geruch aus dem Geruchsartikel aufnimmt: ruhig, interessiert und gezielt am Artikel."),
            new("Aufnahme der Geruchsspur", ExerciseDifficulty.Beginner, "Ansatz",
                "Wie der Hund vom Geruchsartikel zur Spur der Versteckperson findet und sie annimmt, auch wenn er zunächst in die falsche Richtung läuft und sich korrigiert."),
            new("Verfolgen und Halten der Geruchsspur", ExerciseDifficulty.Advanced, "Trailarbeit",
                "Wie sicher der Hund der Spur über den ganzen Trail folgt und sie hält, auch an Richtungswechseln, Kreuzungen und Wechseln des Untergrunds. Gearbeitet wird im normalen Schritt."),
            new("Arbeitsbereitschaft", ExerciseDifficulty.Intermediate, "Trailarbeit",
                "Motivation und Ausdauer des Hundes während der Suche - vom Start bis zum Ende des Trails, auch nach einer Pause."),
            new("Teamwork", ExerciseDifficulty.Intermediate, "Team",
                "Zusammenarbeit von Hundeführer und Hund: Leinenführung, Abstand zum Hund, Lesen des Hundes und Unterstützung ohne Beeinflussung."),
            new("Auffinden der Versteckperson", ExerciseDifficulty.Intermediate, "Finden und Anzeigen",
                "Ob und wie sicher das Team die Versteckperson am Ende des Trails innerhalb der Suchzeit findet."),
            new("Anzeigeverhalten bei der Versteckperson", ExerciseDifficulty.Intermediate, "Finden und Anzeigen",
                "Wie eindeutig der Hund die gefundene Person anzeigt - im vorher genannten Verhalten (verbellen, vorsitzen, vorstehen, vorliegen oder Rückverweisen) und ohne Hilfe, bis der Hundeführer da ist."),

            // ---- SV ----
            new("Lageerfassung und Aufnahme der Geruchsspur", ExerciseDifficulty.Beginner, "Ansatz",
                "Der Start der Suche: Wie der Hundeführer sich ein Bild der Lage macht (Befragung, Gelände, Gefahren) und wie der Hund den Geruch aufnimmt und in die Spur findet."),
            new("Zusammenarbeit mit dem Hund und Lesen des Hundes", ExerciseDifficulty.Intermediate, "Team",
                "Wie gut der Hundeführer seinen Hund liest und mit ihm zusammenarbeitet: Leinenhandling, Pausen, Wasser, Erkennen nachlassender Kondition."),
            new("Selbständigkeit, Kontrollierbarkeit und soziale Verträglichkeit", ExerciseDifficulty.Beginner, "Team",
                "Ob der Hund selbständig sucht, in der Hand des Hundeführers bleibt und sich gegenüber Personen und Hunden unterwegs verträglich verhält."),
            new("Halten der Geruchsspur, Arbeitsbereitschaft und Suchmotivation", ExerciseDifficulty.Intermediate, "Trailarbeit",
                "Wie durchgehend und motiviert der Hund die Spur hält - von Anfang an kontinuierlich, ohne nachzulassen."),
            new("Verfolgen der Geruchsspur und Umgang mit Schwierigkeiten", ExerciseDifficulty.Advanced, "Trailarbeit",
                "Wie der Hund dem Verlauf der Spur folgt und Schwierigkeiten löst: Wegkreuzungen, Richtungswechsel, Wechsel von Untergrund und Bewuchs, Ablenkungen."),
            new("Anzeige der Person", ExerciseDifficulty.Intermediate, "Finden und Anzeigen",
                "Wie eindeutig der Hund die Versteckperson identifiziert und anzeigt, in dem Verhalten, das der Hundeführer vor dem Start dem Richter genannt hat."),
        ]);

        // ---- DVG: Prüfungsordnung Mantrailing, gültig ab 06.05.2024 ----
        const string dvgBewertung =
            "Bewertung: 100 Punkte - Aufnahme vom Geruch 5, Aufnahme der Geruchsspur 5, Verfolgen und Halten 30, Arbeitsbereitschaft 10, Teamwork 10, Auffinden der Versteckperson 30, Anzeigeverhalten 10.\n" +
            "Werturteil: Vorzüglich ab 96 Punkten, Sehr gut 90-95, Gut 80-89, Befriedigend 70-79, Mangelhaft unter 70 - bestanden ab 70 Punkten.\n" +
            "Zulassung: Mindestalter 15 Monate, bestandene VDH BH-VT.\n" +
            "Gesucht wird eine Versteckperson (nach Verlaufsplan, GPS-Aufzeichnung ist zulässig).\n" +
            "Sportprüfung, keine Einsatzprüfung. Fassung: DVG-Prüfungsordnung Mantrailing, gültig ab 06.05.2024 (Stand 05.2024).";

        // Der DVG erlaubt die Veröffentlichung seines Regelwerks nur mit Genehmigung: hier stehen deshalb nur
        // Fakten (Bereiche, Punkte, Suchzeit), keine Einzelregeln wie Abzüge oder Abbruchbedingungen.
        RegulationExerciseSeed[] DvgUebungen(string suchzeit) =>
        [
            new("Aufnahme vom Geruch", true, 5, ""),
            new("Aufnahme der Geruchsspur", true, 5, ""),
            new("Verfolgen und Halten der Geruchsspur", true, 30, ""),
            new("Arbeitsbereitschaft", true, 10, ""),
            new("Teamwork", true, 10, ""),
            new("Auffinden der Versteckperson", true, 30, suchzeit),
            new("Anzeigeverhalten bei der Versteckperson", true, 10, ""),
        ];

        await SeedRegulationAsync(db, mantrailing, new RegulationSeed("DVG MT-I", "05/2024", new DateOnly(2024, 5, 6),
            DvgUebungen("Das Team findet die Versteckperson am Ende des Trails innerhalb der Suchzeit von 15 Minuten."),
            Description: "DVG-Mantrailing Stufe I (MT-I) - die Einstiegsstufe.\n" +
                "Trail: ca. 300 m, Trailalter mindestens 2 Stunden, ruhiges Wohngebiet, Wald oder Wiese, mindestens 1 Richtungswechsel. Suchzeit 15 Minuten.\n" +
                dvgBewertung,
            SourceUrl: QuelleDvgMantrailing));

        await SeedRegulationAsync(db, mantrailing, new RegulationSeed("DVG MT-II", "05/2024", new DateOnly(2024, 5, 6),
            DvgUebungen("Das Team findet die Versteckperson am Ende des Trails innerhalb der Suchzeit von 30 Minuten."),
            Description: "DVG-Mantrailing Stufe II (MT-II).\n" +
                "Trail: ca. 600 m, Trailalter mindestens 4 Stunden, belebtes Wohngebiet, Wald oder Wiese, Kreuzungen, mindestens 2 Richtungswechsel. Suchzeit 30 Minuten.\n" +
                dvgBewertung,
            SourceUrl: QuelleDvgMantrailing));

        await SeedRegulationAsync(db, mantrailing, new RegulationSeed("DVG MT-III", "05/2024", new DateOnly(2024, 5, 6),
            DvgUebungen("Das Team findet die Versteckperson am Ende des Trails innerhalb der Suchzeit von 45 Minuten."),
            Description: "DVG-Mantrailing Stufe III (MT-III) - die höchste Stufe.\n" +
                "Trail: ca. 1000 m, Trailalter mindestens 15 Stunden, belebtes Wohngebiet, Wald oder Wiese, Kreuzungen, zusätzlich befahrener Straßenbereich, mindestens 4 Richtungswechsel. Suchzeit 45 Minuten.\n" +
                dvgBewertung,
            SourceUrl: QuelleDvgMantrailing));

        // ---- SV: Prüfungsordnung Personenspürhundesport Mantrailing, Fassung 2026 ----
        // Das Dokument nennt weder Prädikate noch eine Punktegrenze zum Bestehen. Bestanden ist eine
        // Stufe laut Ordnung nur, wenn der Hund kontinuierlich arbeitet, die Person findet und eindeutig
        // anzeigt und die Suchzeit eingehalten wird; eine Punktzahl dafür steht nicht darin. Wie bei
        // den anderen 100-Punkte-Prüfungen des Katalogs wird hier daher keine Zahl erfunden.
        const string svBewertung =
            "Bewertung: 100 Punkte - Lageerfassung und Aufnahme der Geruchsspur 10, Zusammenarbeit mit dem Hund und Lesen des Hundes 10, Selbständigkeit/Kontrollierbarkeit/soziale Verträglichkeit 10, Halten der Geruchsspur/Arbeitsbereitschaft/Suchmotivation 20, Verfolgen der Geruchsspur/Umgang mit Schwierigkeiten 30, Anzeige der Person 20.\n" +
            "Bestehen: Die Ordnung nennt keine Punktegrenze. Bestanden ist die Stufe nur, wenn der Hund vom Start an kontinuierlich arbeitet, die Person findet und eindeutig anzeigt; wird die Suchzeit überschritten, ist die Prüfung nicht bestanden.\n" +
            "Der Hund arbeitet an einer Führleine von 5 bis 10 m. Die Suchzeit beginnt mit der Übergabe des Geruchsartikels durch den Leistungsrichter; die Spur der Versteckperson wird skizziert oder mit einem GPS-Gerät festgehalten.\n" +
            "Fassung: SV-PO Mantrailing (Personenspürhunde), Fassung 2026. Sportprüfung, keine Einsatzüberprüfung.";

        RegulationExerciseSeed[] SvUebungen(string lage) =>
        [
            new("Lageerfassung und Aufnahme der Geruchsspur", true, 10, lage),
            new("Zusammenarbeit mit dem Hund und Lesen des Hundes", true, 10, "Bewertet werden auch das Handling der Leine an Schwierigkeiten sowie Wasser und Pause für den Hund."),
            new("Selbständigkeit, Kontrollierbarkeit und soziale Verträglichkeit", true, 10, "Der Richter kann die Suche abbrechen, wenn der Hund die Arbeit verweigert oder aus eigener Kraft nicht fortsetzen kann."),
            new("Halten der Geruchsspur, Arbeitsbereitschaft und Suchmotivation", true, 20, "Der Hund arbeitet vom Start weg kontinuierlich; zeigt er trotz mehrfachen Ansetzens kein Suchverhalten, ist die Aufgabe nicht erfüllt."),
            new("Verfolgen der Geruchsspur und Umgang mit Schwierigkeiten", true, 30, "Wegkreuzungen und Richtungswechsel auszuarbeiten ist nicht fehlerhaft, solange der Hund wieder die Laufrichtung der Versteckperson findet. Das Ansetzen an einer Stelle, an der die Spur noch eindeutig war, ist erlaubt."),
            new("Anzeige der Person", true, 20, "Das Anzeigeverhalten nennt der Hundeführer vor dem Start dem Leistungsrichter; die Aufgabe gilt als erfüllt, wenn das Team den Endpunkt erreicht und die Person dem Richter übergeben hat."),
        ];

        await SeedRegulationAsync(db, mantrailing, new RegulationSeed("SV-MT 1", "2026", new DateOnly(2026, 1, 1),
            SvUebungen("Der Hundeführer erhält den letzten Aufenthaltsort der Person an einem markanten Punkt und die Richtung, in die sie gegangen ist."),
            Description: "SV-Mantrailing Stufe 1 (SV-MT 1) - die Einstiegsstufe.\n" +
                "Trail: ca. 300 m, ruhiges, urbanes Wohngebiet oder Feld-, Wald- und Wiesenwege, mindestens 2 Richtungswechsel. Liegezeit mindestens 1 Stunde, Suchzeit 25 Minuten.\n" +
                "Zulassung: Mindestalter 15 Monate, bestandene FCI BH/VT.\n" +
                svBewertung,
            SourceUrl: QuelleSvMantrailing));

        await SeedRegulationAsync(db, mantrailing, new RegulationSeed("SV-MT 2", "2026", new DateOnly(2026, 1, 1),
            SvUebungen("Der Hundeführer erhält den letzten Aufenthaltsort in der Nähe eines markanten Punktes und mindestens zwei mögliche Richtungen."),
            Description: "SV-Mantrailing Stufe 2 (SV-MT 2).\n" +
                "Trail: ca. 600 m, aus einem belebten, urbanen Wohngebiet in Feld-, Wald- oder Wiesenwege, mindestens 4 Richtungswechsel. Liegezeit mindestens 3 Stunden, Suchzeit 35 Minuten.\n" +
                "Zulassung: Mindestalter 18 Monate, bestandene SV-MT 1 oder FCI RH MT V.\n" +
                svBewertung,
            SourceUrl: QuelleSvMantrailing));

        await SeedRegulationAsync(db, mantrailing, new RegulationSeed("SV-MT 3", "2026", new DateOnly(2026, 1, 1),
            SvUebungen("Der Hundeführer erhält nur den letzten Aufenthaltsort, keinen Hinweis auf die Richtung - sie zu finden ist Teil der Aufgabe."),
            Description: "SV-Mantrailing Stufe 3 (SV-MT 3) - die höchste Stufe.\n" +
                "Trail: ca. 1000 m, urbanes Wohngebiet mit mindestens einem Kreuzungsbereich in befahrbaren Straßen, mindestens 6 Richtungswechsel einschließlich Straßenkreuzungen und -querungen. Liegezeit ca. 24 Stunden, Suchzeit 45 Minuten.\n" +
                "Zulassung: Mindestalter 24 Monate, bestandene SV-MT 2 oder FCI RH MT A.\n" +
                svBewertung,
            SourceUrl: QuelleSvMantrailing));
    }
}

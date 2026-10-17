using Dogity.Domain.Sports;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Die österreichischen Prüfungsordnungen des ÖKV (Paket L2, 2026-10-17):
/// Mensch-Hund-Team-Prüfung (BH/VT), Agility, Hoopers und Breitensport.
///
/// Eigene Datei, damit der Hauptseeder nicht weiter wächst. Alles hier trägt
/// <c>CountryCode</c> "AT" - auch unter den Sportarten BH, Agility und
/// Hoopers, die es zusätzlich mit FCI- bzw. VDH-Ordnungen gibt.
///
/// Quellen (offizielle ÖKV-Dokumente, siehe docs/PO_VERIFIKATION.md,
/// Abschnitt "Österreich"). Jede Ordnung und Übung nennt die Seite des
/// Dokuments, in Klammern wie im PDF gezählt ("S. 14"). Die Texte sind eigene
/// Beschreibungen, keine Kopie der Ordnungen.
///
/// Bewertet wird, wie bei den deutschen Ordnungen derselben Art: Agility,
/// Hoopers und Läufe über Fehlerpunkte und Zeit (Übungen mit 0 Punkten), nur
/// die Gehorsamsübungen von BH/VT und Breitensport haben Übungspunkte.
///
/// Unklares ist nicht ergänzt, sondern in den Beschreibungen so benannt, wie
/// es im Dokument steht (z. B. fehlende Fehlerbewertung der Hürden im
/// Hürdenlauf).
/// </summary>
public static partial class SportCatalogSeeder
{
    /// <summary>Geltungsbereich der ÖKV-Ordnungen (ISO 3166-1 alpha-2).</summary>
    private const string Oesterreich = FciKatalog.Oesterreich;

    private const string QuelleOepo = "https://oekv.at/media/upload/editor/files/%C3%96KV/Leistungsreferat/%C3%96PO_2024%20HP.pdf";
    private const string QuelleAgility = "https://www.oekv.at/media/upload/editor/files/%C3%96KV/Leistungsreferat/%C3%96KV-Agilityreglement_2026_g%C3%BCltig%20ab%201.1.2026.pdf";
    private const string QuelleHoopers = "https://www.oekv.at/media/upload/editor/files/%C3%96KV/Leistungsreferat/%C3%96KV%20PO%20Hoopers%20Stand%2010-2023.pdf";
    private const string QuelleBreitensport = "https://breitensport.oekv.at/media/upload/editor/files/Breitensport/Allgemeines/BSP_PO_2025.pdf";

    /// <summary>Sportart-Code der österreichischen Breitensport-Ordnung.</summary>
    public const string BreitensportCode = "BSP";

    private static async Task SeedOesterreichAsync(ApplicationDbContext db, Sport bh, Sport agility, Sport hoopers)
    {
        await SeedOekvBegleithundAsync(db, bh);
        await SeedOekvAgilityAsync(db, agility);
        await SeedOekvHoopersAsync(db, hoopers);
        await SeedOekvBreitensportAsync(db);
    }

    // -------------------------------------------------------------------
    // Mensch-Hund-Team-Prüfung / BH-VT (ÖPO, Ausgabe 2022, beschlossen am
    // 24.11.2021, in Kraft seit 01.01.2022)
    //
    // Anders als die FCI-BH/VT (60 Punkte, bestanden ab 42): 100 Punkte in
    // sechs Übungen, bestanden ab 60 %, und die Punktzahl wird nicht
    // bekanntgegeben - nur das Prädikat. Freifolge und Freilaufen sind
    // freiwillig. Die Verkehrsübungen tragen wie bei der deutschen BH keine
    // Einzelpunkte; sie sind hier zusammengefasst, wo die deutsche BH sie
    // ebenfalls zusammenfasst ("nicht einzeln zu üben", Betreiberauskunft
    // 2026-08-19).
    // -------------------------------------------------------------------
    private static async Task SeedOekvBegleithundAsync(ApplicationDbContext db, Sport bh)
    {
        // Übungen der BH, die es noch nicht gibt. Der Rest wird wiederverwendet:
        // Fußarbeit (= "Folgen an der Leine"), Freifolge, Sitzübung, Ablegen in
        // Verbindung mit Herankommen, Ablegen unter Ablenkung, Verhalten im
        // Verkehr, Begegnung mit Personengruppe, Verhalten gegenüber anderen
        // Hunden und Zurücklassen des Hundes sind fachlich dieselben Übungen.
        await SeedSportAsync(db, bh.Code, bh.Name,
        [
            new("Freilaufen mit Heranrufen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Der abgeleinte Hund läuft frei. Hat er sich etwa 10 Meter entfernt, bekommt er das Signal zum Herankommen; er kommt freudig zum Hundehalter und lässt sich wieder anleinen."),
            new("Ortsabhängige Begegnung", ExerciseDifficulty.Intermediate, "Verhalten",
                "Eine zusätzliche Begegnung, die der Prüfer mit dem Hundehalter nach den örtlichen Gegebenheiten abstimmt: Pferde mit Reitern, öffentliche Verkehrsmittel, Aufzug, Menschen ohne Ausweichmöglichkeit (z. B. Baustelle), Park mit Spielplatz oder Hundezone. Gefragt sind neutrales Verhalten des Hundes und verantwortungsvolles Handeln des Hundehalters."),
        ]);

        await SeedRegulationAsync(db, bh, new RegulationSeed("ÖKV BH/VT (Mensch-Hund-Team-Prüfung)", "2022", new DateOnly(2022, 1, 1),
        [
            new("Fußarbeit", true, 20, "In der ÖPO „Folgen an der Leine“ (S. 14). Angeleint mindestens 30 m geradeaus, dann mindestens eine Kehrtwendung, ein Richtungswechsel links, einer rechts und ein Tempowechsel (Laufschritt, langsamer Schritt); einmal stehen bleiben, der Hund nimmt selbstständig die Grundstellung ein. Zum Schluss durch eine Gruppe von mindestens vier Personen (ca. 3 m Abstand): eine Person rechts, eine links umgehen, z. B. in Form einer Acht, mindestens einmal darin anhalten."),
            new("Freifolge", false, 10, "In der ÖPO „Frei Folgen“ (S. 14 f.), freiwillig. Nach der Gruppe wird in der Grundstellung abgeleint; der Hund folgt etwa 30 m frei, dann Grundstellung und wieder anleinen. Wird sie nicht gezeigt, ist trotz sonst fehlerfreier Prüfung kein „Ausgezeichnet“ möglich. Entfernt sich der Hund mehr als 3 m und kommt nach fünf Signalen nicht zurück, wird die Prüfung abgebrochen."),
            new("Sitzübung", true, 10, "In der ÖPO „Absetzen“ bzw. Sitzübung (S. 15). Angeleint einige Schritte gehen, Signal zum Hinsetzen, Leine auf den Boden legen, sich mindestens 10 Schritte entfernen, umdrehen und zurückgehen. Wechselt der Hund die Position, bleibt aber am Ort: mindestens 3 Punkte Abzug; verlässt er den Ort: 0 Punkte."),
            new("Ablegen in Verbindung mit Herankommen", true, 20, "In der ÖPO „Ablegen mit Heranrufen“ (S. 15). Der Hund wird angeleint abgelegt, dann abgeleint; der Hundehalter geht ca. 10 Schritte, dreht sich um und ruft den Hund auf Zeichen des Prüfers. Vorsitzen und Endgrundstellung sind nicht verlangt. Positionswechsel am Ort: mindestens 3 Punkte Abzug; läuft der Hund vor dem Signal nach: 10 Punkte Abzug; kommt er nach höchstens 5 Signalen nicht: Abbruch."),
            new("Ablegen unter Ablenkung", true, 20, "S. 16. Der Hund liegt an lockerer Leine neben dem stehenden Hundehalter, während das andere Team seine Abschnitte zeigt. Verlässt er die Liegeposition, bleibt aber ruhig beim Hundehalter: 5 Punkte Abzug; entzieht er sich dem Einfluss des Hundehalters: 15 Punkte Abzug; nimmt er die Position zu Beginn nicht ein: 0 Punkte."),
            new("Freilaufen mit Heranrufen", false, 20, "S. 16, freiwillig. Nachdem das andere Team das Gelände verlassen hat, wird der Hund abgeleint und läuft frei; nach ca. 10 m Entfernung folgt das Signal zum Herankommen. Kommt er nach dem fünften Signal nicht, wird die Prüfung abgebrochen."),
            new("Begegnung mit Personengruppe", true, 0, "Verkehrsteil ohne Einzelpunkte (S. 17): Der Gesamteindruck entscheidet. Auf dem Gehweg wird das Team von einem Passanten überholt und geht durch eine aufgelockerte Gruppe von mindestens 6 Personen, von der eine den Hundehalter anspricht und mit Handreichen begrüßt (S. 18). Dazu die Begegnung mit einer Person mit Gehhilfe (S. 19)."),
            new("Verhalten im Verkehr", true, 0, "Verkehrsteil ohne Einzelpunkte (S. 17). Hier zusammengefasst: Radfahrer (überholt von hinten, wendet, kommt entgegen), Autos (ein Motor startet, eine Tür schlägt zu, ein Auto hält und der Fahrer fragt nach dem Weg), Jogger oder Inline-Skater (überholt, kommt entgegen) und eine Person mit Kinderwagen (S. 18 f.). Verlangt ist neutrales Verhalten; Sitz oder Platz auf Signal ist erlaubt."),
            new("Verhalten gegenüber anderen Hunden", true, 0, "Verkehrsteil ohne Einzelpunkte (S. 19). Ein anderer Hund mit Hundehalter überholt und kommt entgegen, Mindestabstand 5 m. Der Hundehalter darf stehen bleiben und dem Hund eine Ruheposition anweisen."),
            new("Zurücklassen des Hundes", false, 0, "Eine der zusätzlichen Überprüfungen (S. 19): Der Hund wird kurzfristig im Verkehr angeleint allein gelassen; geprüft wird sein Verhalten gegenüber Tieren und Menschen, Maulkorb erforderlich. Von den zusätzlichen Überprüfungen müssen mindestens eine und höchstens zwei absolviert werden."),
            new("Ortsabhängige Begegnung", false, 0, "Zusätzliche Überprüfung nach den örtlichen Gegebenheiten (S. 19), abgestimmt mit dem Hundehalter: Pferde und Reiter, öffentliche Verkehrsmittel, Aufzug mit anderen Menschen, Menschen ohne Ausweichmöglichkeit, Park mit Spielplatz, Hundezone - teils mit Maulkorb. Mindestens eine, höchstens zwei zusätzliche Überprüfungen sind zu absolvieren."),
        ],
        Description: "Mensch-Hund-Team-Prüfung (BH/VT, Begleithundeprüfung mit Verhaltensteil) nach der Österreichischen Prüfungsordnung des ÖKV (ÖPO, Ausgabe 2022, in Kraft seit 01.01.2022).\n" +
            "Gilt als Voraussetzung für alle weiteren Prüfungen nach ÖKV- und FCI-Ordnung (S. 10).\n" +
            "Ablauf: 1. Sachkunde-Nachweis (Vortrag von Dogaudit-geprüften oder ÖKV-anerkannten Personen, oder der Nachweis nach der Hundehalteverordnung des Bundeslandes), 2. Unbefangenheit samt Chipkontrolle, 3. einfache Gehorsamsübungen, 4. Prüfungsteil im öffentlichen Verkehr. Der Verkehrsteil wird nur nach bestandenem Gehorsam geprüft (S. 10 f.).\n" +
            "Gehorsam, 100 Punkte: Folgen an der Leine 20, Folgen ohne Leine 10, Absetzen 10, Ablegen mit Heranrufen 20, Ablegen unter Ablenkung 20, Freilaufen mit Heranrufen 20. Folgen ohne Leine und Freilaufen sind freiwillig; ohne Folgen ohne Leine ist kein „Ausgezeichnet“ möglich (S. 11, 14).\n" +
            "Bestanden mit mindestens 60 % der Punkte, „Ausgezeichnet“ ab 90 %. Bekanntgegeben wird nur das Prädikat, nicht die Punktzahl (S. 11).\n" +
            "Verkehrsteil (S. 17-19): keine Einzelpunkte, der Gesamteindruck entscheidet. Verlangt ist neutrales Verhalten; bei Aggression oder unbeherrschbarer Angst ist die ganze Prüfung nicht bestanden. Dazu kommen mindestens eine und höchstens zwei zusätzliche Begegnungen nach Ort.\n" +
            "Mindestalter des Hundes: 12 Monate. Vorzulegen: Haftpflichtnachweis, Impfpass mit Chipnummer, Registrierungsnachweis (S. 7, 10).\n" +
            "Unterschied zur FCI-BH/VT: dort 60 Punkte, bestanden ab 42, Mindestalter 15 Monate.",
        Land: Oesterreich,
        SourceUrl: QuelleOepo));
    }

    // -------------------------------------------------------------------
    // Agility (ÖKV-Agilityreglement, Ausgabe 2026, gültig ab 01.01.2026,
    // beschlossen am 30.10.2025)
    //
    // Wie die deutschen A0-A3 und das Jumping: je Leistungsklasse eine
    // Ordnung für den Agility-Lauf, dazu ein Jumping für alle drei. Gewertet
    // wird über Fehlerpunkte und Zeit, die Geräte tragen 0 Punkte.
    //
    // Jugend, Oldies und ParAgility heißen im Reglement zwar "Leistungsklasse"
    // (LK JUG, LK OL, LK ParA), sind aber Teilnehmerklassen: Sie hängen am
    // Alter des Hundeführers, am Alter des Hundes oder an einer Behinderung,
    // haben keinen eigenen Aufstieg und werden nach dem Schwierigkeitsgrad der
    // LK 1 bzw. LK 2 gerichtet (S. 13-17). Eine eigene Ordnung wäre eine
    // Stufe, die es nicht gibt - sie stehen als Hinweis in der Beschreibung.
    // -------------------------------------------------------------------
    private const string AgilityBewertung =
        "Bewertung: Fehler und Verweigerungen je 5 Fehlerpunkte, Zeitfehler 1 Fehlerpunkt je Sekunde über der Standardzeit (auf hundertstel Sekunden). Gesamtfehlerpunkte 0-5,99 Vorzüglich, 6-15,99 Sehr gut, 16-25,99 Gut, darüber Befriedigend (S. 26, 32).\n" +
        "Disqualifikation u. a. bei der dritten Verweigerung, Überschreiten der Höchstzeit (1,5- bis 2-fache Standardzeit) und Hindernissen außerhalb der Reihenfolge (S. 7, 30).\n" +
        "Größenklassen: S bis 34,99 cm, M 35-42,99, I 43-47,99, L ab 48 cm Widerristhöhe. Mindestalter 18 Monate. Start nur mit gültiger digitaler ÖKV-Agility-Lizenz; zum Erstantritt gehört eine bestandene BH-VT (S. 4, 9 f.).";

    private const string AgilityTeilnehmerklassenKurz =
        "Jugend, Oldies und ParAgility sind Teilnehmerklassen (Alter des Hundeführers, Alter des Hundes, Behinderung), keine Leistungsstufen - siehe ÖKV Agility LK 1.";

    private static async Task SeedOekvAgilityAsync(ApplicationDbContext db, Sport agility)
    {
        var datum = new DateOnly(2026, 1, 1);

        // Die Geräte der Leistungsklassen. Doppelhürden (im Katalog
        // "Doppelsprung (Spread)") sind in der LK 1 nicht gestattet (S. 14, 22).
        RegulationExerciseSeed[] Geraete(bool mitDoppelsprung, bool mitKontaktzonen)
        {
            var liste = new List<RegulationExerciseSeed>
            {
                new("Hürde", true, 0, "Abwurf 5 Fehlerpunkte; Unterlaufen oder Überspringen des Seitenteils in Laufrichtung: Verweigerung (5). Sprunghöhe L 55-60, I 45-50, M 35-40, S 25-30 cm (S. 20, 27 f.)."),
            };
            if (mitDoppelsprung)
                liste.Add(new("Doppelsprung (Spread)", true, 0, "In der ÖKV-Ordnung „Doppelhürde“: Abwurf 5 Fehlerpunkte. Gesamttiefe höchstens L 50, I 45, M 40, S 30 cm; im Parcours nur einmal (S. 6, 22)."));
            liste.Add(new("Mauer", true, 0, "Abwurf eines Elements 5 Fehlerpunkte; Sprunghöhe inklusive Ziegel wie bei der Hürde; im Parcours nur einmal (S. 6, 22)."));
            liste.Add(new("Reifen", true, 0, "Unterlaufen: Verweigerung (5); öffnet der Hund den Reifen beim Durchspringen: Fehler (5). Mitte L 80, I 70, M/S 55 cm über dem Boden; im Parcours nur einmal (S. 25, 29 f.)."));
            liste.Add(new("Weitsprung", true, 0, "Überspringen in der Breite oder seitliches Ein- oder Ausspringen: Verweigerung (5); Umwerfen eines Elements: Fehler (5); Berühren der Begrenzungsstangen ist kein Fehler. Länge L 120-150 (4 Elemente), I 90-110, M 70-90, S 40-50 cm (S. 25 f., 30)."));
            if (mitKontaktzonen)
            {
                liste.Add(new("Laufsteg", true, 0, "Kontaktzone beim Abgang nicht berührt: 5 Fehlerpunkte; Absprung, ohne mit allen Pfoten den absteigenden Teil berührt zu haben: Verweigerung (5); Aufgang nicht mit allen vier Pfoten: Disqualifikation. Höhe 120-130 cm (S. 23, 29)."));
                liste.Add(new("Wippe", true, 0, "Kontaktzone nicht berührt: 5 Fehlerpunkte; Absprung vor Bodenkontakt: Fehler (5), ohne vorher die Kontaktzone betreten zu haben 10; Absprung, ohne den Kipppunkt überschritten zu haben: Verweigerung (5). Höhe am Kipppunkt 60 cm (S. 23, 28 f.)."));
                liste.Add(new("Schrägwand (A-Wand)", true, 0, "Kontaktzone beim Abgang nicht berührt: 5 Fehlerpunkte; Absprung ohne alle vier Pfoten auf dem absteigenden Teil: Verweigerung (5); Aufgang nicht mit allen vier Pfoten oder Überspringen des Scheitels: Disqualifikation. Höhe 170 cm in allen Größenklassen (S. 24, 29)."));
            }
            liste.Add(new("Slalom", true, 0, "12 Stangen. Falscher Eintritt (erste Stange nicht links vom Hund): Verweigerung (5); ausgelassene Stange: Fehler (5), weitere Torfehler werden nicht zusätzlich geahndet, müssen aber korrigiert werden; Zurückwedeln um mehr als 2 Tore: Disqualifikation. Im Parcours nur einmal (S. 6, 24, 29)."));
            liste.Add(new("Tunnel", true, 0, "Zurückziehen von Kopf oder Pfote, Umkehren im Tunnel oder Überspringen: Verweigerung (5). Durchmesser 60 cm, Länge 3-6 m, höchstens fünfmal im Parcours (S. 6, 25, 29)."));
            return [.. liste];
        }

        await SeedRegulationAsync(db, agility, new RegulationSeed("ÖKV Agility LK 1 (A 1)", "2026", datum,
            Geraete(mitDoppelsprung: false, mitKontaktzonen: true),
            Description: "ÖKV-Agility, Leistungsklasse 1: Agility-Läufe (A 1) und Jumping (J 1). Im Reglement heißt es „Agility-Lauf der LK 1“ - Agility ist Wettkampf, keine „Prüfung“ (S. 13).\n" +
                "In der LK 1 starten Hunde, die noch nicht in die LK 2 aufgestiegen sind oder es nicht wollen. Mindestgeschwindigkeit der Standardzeit: A 1 2,5 m/s, J 1 3,0 m/s (S. 6).\n" +
                "Parcours: höchstens 20 Hindernisse, im Agility-Lauf höchstens 3 mit Kontaktzone, keine Doppelhürden, der Slalom ist Pflicht (S. 14). Ein Agility-Lauf braucht mindestens zwei verschiedene Kontaktzonengeräte (S. 13).\n" +
                "Aufstieg nach LK 2: drei A-1-Läufe mit Vorzüglich bei 0,00 Fehlerpunkten unter mindestens zwei verschiedenen Richtern und ein J-1-Lauf mit Vorzüglich bei 0,00; Platzierung und Starterzahl sind unerheblich (S. 14).\n" +
                "Teilnehmerklassen: Jugend (Hundeführer im Antrittsjahr höchstens 15 Jahre), gerichtet wie LK 1 (S. 13 f.); Oldies (Hund ab dem 7. Geburtstag), gerichtet wie LK 2 mit höherer Standardzeit, Sprunghöhe L/I 40 cm, M/S 20 cm, der Wechsel ist endgültig (S. 15 f.); ParAgility (Hundeführer mit belegbarer Behinderung), ohne Standard- und Höchstzeit (S. 16 f.). Sie sind keine Leistungsstufen und haben keinen eigenen Aufstieg.\n" +
                AgilityBewertung,
            Land: Oesterreich,
            SourceUrl: QuelleAgility));

        await SeedRegulationAsync(db, agility, new RegulationSeed("ÖKV Agility LK 2 (A 2)", "2026", datum,
            Geraete(mitDoppelsprung: true, mitKontaktzonen: true),
            Description: "ÖKV-Agility, Leistungsklasse 2: Agility-Läufe (A 2) und Jumping (J 2). Doppelhürden sind ab dieser Klasse zugelassen (S. 22).\n" +
                "Mindestgeschwindigkeit der Standardzeit: A 2 3,3 m/s, J 2 3,6 m/s (S. 6).\n" +
                "Parcours: höchstens 22 Hindernisse, im Agility-Lauf höchstens 4 mit Kontaktzone, der Slalom ist Pflicht (S. 15).\n" +
                "Aufstieg nach LK 3: drei A-2-Läufe mit Vorzüglich bei 0,00 Fehlerpunkten und Platz 1 bis 3 unter mindestens zwei verschiedenen Richtern und ein Jumping-Lauf mit Vorzüglich bei 0,00 und Platz 1 bis 3 (im Reglement als „J1“ bezeichnet). Eine Platzierung zählt bei Rang 1 mit mindestens 2, bei Rang 2 mit mindestens 3 und bei Rang 3 mit mindestens 4 gemeldeten und angetretenen Startern (S. 15).\n" +
                "Solange der ÖKV den Wechsel in die LK 3 nicht bestätigt hat, bleibt der Hund in der LK 2. Ein freiwilliger Abstieg nach LK 1 ist möglich, wenn der Hund noch nicht in der LK 3 war (S. 15).\n" +
                AgilityTeilnehmerklassenKurz + "\n" +
                AgilityBewertung,
            Land: Oesterreich,
            SourceUrl: QuelleAgility));

        await SeedRegulationAsync(db, agility, new RegulationSeed("ÖKV Agility LK 3 (A 3)", "2026", datum,
            Geraete(mitDoppelsprung: true, mitKontaktzonen: true),
            Description: "ÖKV-Agility, Leistungsklasse 3: Agility-Läufe (A 3) und Jumping (J 3) - die höchste Klasse.\n" +
                "Mindestgeschwindigkeit der Standardzeit: A 3 3,5 m/s, J 3 3,8 m/s (S. 6).\n" +
                "Parcours: höchstens 22 Hindernisse, im Agility-Lauf höchstens 4 mit Kontaktzone, der Slalom ist Pflicht (S. 15).\n" +
                "Ein freiwilliger Abstieg ist nur nach LK 2 möglich, nicht weiter nach LK 1. Für den erneuten Aufstieg gelten die Bedingungen der LK 2 (S. 15).\n" +
                "Auf- und abstiegsrelevante Einzelheiten prüft die Fachkommission Agility jährlich (S. 17).\n" +
                AgilityTeilnehmerklassenKurz + "\n" +
                AgilityBewertung,
            Land: Oesterreich,
            SourceUrl: QuelleAgility));

        await SeedRegulationAsync(db, agility, new RegulationSeed("ÖKV Jumping (J 1 - J 3)", "2026", datum,
            Geraete(mitDoppelsprung: true, mitKontaktzonen: false),
            Description: "ÖKV-Jumping - dasselbe Reglement wie beim Agility, aber ohne Kontaktzonengeräte, also ohne Laufsteg, Wippe und Schrägwand (S. 13).\n" +
                "Gelaufen in den Leistungsklassen als J 1, J 2 und J 3. Mindestgeschwindigkeit der Standardzeit: J 1 3,0 m/s, J 2 3,6 m/s, J 3 3,8 m/s (S. 6). Doppelhürden gibt es erst ab der LK 2 (S. 22).\n" +
                "Für den Aufstieg zählen Agility- und Jumping-Läufe der geforderten Leistungsklasse (S. 13); die Bedingungen stehen bei LK 1 und LK 2.\n" +
                "Wenn zwei Agility- oder zwei Jumping-Läufe an einem Tag angeboten werden, gilt nur ein Aufstiegsnuller pro Tag (S. 18).\n" +
                AgilityTeilnehmerklassenKurz + "\n" +
                AgilityBewertung,
            Land: Oesterreich,
            SourceUrl: QuelleAgility));
    }

    // -------------------------------------------------------------------
    // Hoopers (ÖKV Prüfungsordnung Hoopers, gültig ab 01.01.2024)
    //
    // Klassen H1 bis H3, gewertet nur über Fehlerpunkte. Die Übungen der
    // deutschen Hoopers-Ordnung (Geräte und Fertigkeiten des Führens auf
    // Distanz) gelten hier genauso; was sich unterscheidet, steht in den
    // Hinweisen der Ordnung. Ohne Größenklassen und ohne H0 - beides kennt
    // diese Fassung nicht.
    // -------------------------------------------------------------------
    private static async Task SeedOekvHoopersAsync(ApplicationDbContext db, Sport hoopers)
    {
        const string bewertung =
            "Bewertung: Fehlerpunkte in Fünfer-Schritten, keine Zeitfehler; Maximalzeit 4 Minuten, Überschreiten: ohne Bewertung (S. 20, 30).\n" +
            "Werturteil: 0 Fehlerpunkte Ausgezeichnet, 5 Vorzüglich, 10-15 Sehr gut, 20-25 Gut, mehr als 25 ohne Bewertung (S. 31). Die VDH-Ordnung vergibt „Vorzüglich“ schon bis 5.\n" +
            "Der Hundeführer bleibt nach dem Starthoop im Führbereich (Kreis 2 m Durchmesser oder Quadrat 2 x 2 m); Verlassen mit einem ganzen Fuß: ohne Bewertung. Dritter Fehler in Folge, absichtliches Training an mehr als 3 Geräten und ein Hund ohne Zielhoop: ohne Bewertung (S. 28-30).\n" +
            "Mindestens die Hälfte der Geräte sind Hoops; Start und Ziel sind immer ein Hoop. Tunnel nur ohne Lauffläche (S. 18, 20, 25).\n" +
            "Mindestalter 18 Monate. Voraussetzung: bestandene ÖPO-BH/VT oder IGP-BH/VT (S. 9 f.). Die Ordnung kennt keine Größenklassen und keine Klasse H0.";

        RegulationExerciseSeed[] Uebungen(string distanz) =>
        [
            new("Hoop", true, 0, "Vorbeilaufen oder Auslassen: 5 Fehlerpunkte (höchstens zwei in Folge). Lichte Weite 90 ± 5 cm, Höhe mindestens 100 cm (S. 22, 29)."),
            new("Tonne", true, 0, "Entgegen der Laufrichtung oder außerhalb der Reihenfolge absolviert: je 5 Fehlerpunkte; ebenso das Verlassen des Korridors, wenn Verleitungsgeräte in zweiter Reihe stehen. Durchmesser 45-70 cm, Höhe 65-100 cm (S. 23, 29)."),
            new("Gate", true, 0, "Entgegen der Laufrichtung oder außerhalb der Reihenfolge: je 5 Fehlerpunkte. Breite 100-130 cm, Höhe 90-110 cm (S. 24, 29)."),
            new("Tunnel", true, 0, "Wenden im Tunnel und Verlassen auf der falschen Seite: 5 Fehlerpunkte. Nur bodenlos, Durchmesser 80 ± 5 cm, Länge 100 ± 5 cm (S. 25, 29)."),
            new("Start vor dem Starthoop", true, 0, "Freiwillige Startkorrektur und Fehlstart: je 5 Fehlerpunkte, jeweils höchstens zweimal; das dritte Verlassen des Führbereichs oder der dritte Fehlstart, ein nicht korrigierter Fehlstart und ein Start vor der Richterfreigabe: ohne Bewertung (S. 28-30)."),
            new("Distanz zum Führbereich", true, 0, distanz),
            new("Richtungszeichen", true, 0, "Absichtliches Heranrufen oder Führen des Hundes durch den Führbereich und absichtliches Berühren des Hundes: je 5 Fehlerpunkte. Der Hundeführer hält nichts in der Hand (S. 13, 29)."),
            new("Flüssiger Lauf", true, 0, "Anhalten oder Kreiseln auf der Stelle (länger als 2 Sekunden) und wiederholtes Anhalten durch Signale zum Ausrichten als Führstil: je 5 Fehlerpunkte (S. 29)."),
            new("Reihenfolge und Verleitungen", true, 0, "Ein Gerät außerhalb der Reihenfolge absolvieren oder die Gerätelinie überschreiten, ohne das Gerät zu absolvieren: je 5 Fehlerpunkte, auch bei Hoop und Tunnel. Nimmt der Hund auf dem Weg zum Starthoop ein Gerät außerhalb der Reihenfolge: ohne Bewertung (S. 29 f.)."),
            new("Zielhoop und Abschluss", true, 0, "Läuft der Hund nicht durch den Zielhoop: ohne Bewertung. Die versteckte Belohnung (Spielzeug oder geschlossener Futterbeutel) gibt es erst danach und sie wird nicht geworfen; Futterstücke auf der Parcoursfläche: ohne Bewertung (S. 13, 30)."),
        ];

        var datum = new DateOnly(2024, 1, 1);

        await SeedRegulationAsync(db, hoopers, new RegulationSeed("ÖKV Hoopers 1 (H1)", "2024", datum,
            Uebungen("Weitestes Gerät höchstens 15 m vom Führbereich (S. 19)."),
            Description: "ÖKV-Hoopers, Klasse H1 - die Einstiegsklasse. Startberechtigt ist jeder Hund, der die allgemeinen Voraussetzungen erfüllt (S. 15).\n" +
                "Parcours: 10-17 Geräte, 5-8 m Abstand, weitestes Gerät höchstens 15 m vom Führbereich (S. 19).\n" +
                "Aufstieg nach H2: dreimal Ausgezeichnet aus Einzelläufen an mindestens drei verschiedenen Veranstaltungstagen, an jedem dieser Tage mindestens einmal, bei zwei verschiedenen Hoopers-Wertungsrichtern. Ein Verbleib in H1 ist möglich, erreichte Qualifikationen bleiben erhalten (S. 15).\n" +
                bewertung,
            Land: Oesterreich,
            SourceUrl: QuelleHoopers));

        await SeedRegulationAsync(db, hoopers, new RegulationSeed("ÖKV Hoopers 2 (H2)", "2024", datum,
            Uebungen("Weitestes Gerät höchstens 22 m vom Führbereich (S. 19)."),
            Description: "ÖKV-Hoopers, Klasse H2.\n" +
                "Parcours: 15-22 Geräte, 6-10 m Abstand, weitestes Gerät höchstens 22 m vom Führbereich (S. 19).\n" +
                "Aufstieg nach H3: dreimal Ausgezeichnet aus Einzelläufen an mindestens drei verschiedenen Veranstaltungstagen, an jedem Tag mindestens einmal, in geschützten Veranstaltungen bei zwei verschiedenen Wertungsrichtern. Ein Verbleib in H2 ist möglich; ein freiwilliger Abstieg nach H1 jederzeit, danach gelten die Aufstiegskriterien erneut (S. 15).\n" +
                bewertung,
            Land: Oesterreich,
            SourceUrl: QuelleHoopers));

        await SeedRegulationAsync(db, hoopers, new RegulationSeed("ÖKV Hoopers 3 (H3)", "2024", datum,
            Uebungen("Weitestes Gerät höchstens 35 m vom Führbereich (S. 19)."),
            Description: "ÖKV-Hoopers, Klasse H3 - die höchste Klasse.\n" +
                "Parcours: 18-25 Geräte, 6-12 m Abstand, weitestes Gerät höchstens 35 m vom Führbereich (S. 19).\n" +
                "Ein freiwilliger Abstieg nach H2 oder H1 ist jederzeit möglich und wird vom Hundeführer im Meldesystem dokumentiert; für den erneuten Aufstieg gelten die Kriterien wieder (S. 15).\n" +
                bewertung,
            Land: Oesterreich,
            SourceUrl: QuelleHoopers));
    }

    // -------------------------------------------------------------------
    // Breitensport (ÖKV Prüfungsordnung Breitensport, gültig ab 01.01.2025,
    // beschlossen am 29.02.2024)
    //
    // Vorbild ist die deutsche Sportart Turnierhundsport. Die Übungen sind
    // trotzdem eigene (Sportart BSP), damit der österreichische und der
    // deutsche Katalog getrennt bleiben. Als Prüfung gelten nur VK 1 und
    // VK 2; alles andere (einzelne Läufe, Dreikampf, CSC, Shorty) sind
    // Open-Bewerbe, die nicht als Prüfung zählen (S. 15 f.). Sie stehen
    // trotzdem im Katalog, weil man sie trainiert.
    //
    // Gewertet wird die Unterordnung mit Punkten (max. 60), die Läufe über
    // Ausgangspunkte abzüglich Laufzeit und Fehlerpunkten - sie stehen wie im
    // Turnierhundsport mit 0 Punkten, die Ausgangspunkte in der Beschreibung.
    // -------------------------------------------------------------------
    private static async Task SeedOekvBreitensportAsync(ApplicationDbContext db)
    {
        var breitensport = await SeedSportAsync(db, BreitensportCode, "Breitensport",
        [
            new("Leinenführigkeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "Nur im VK 1. Angeleint im festen H-Schema: 40 Schritte geradeaus, Kehrtwendung, Rechts- und Linkswendung, ein Anhalten, danach derselbe Ablauf im Laufschritt ohne Anhalten. Der Hund bleibt mit dem Schulterblatt auf Kniehöhe links, die Leine hängt locker durch."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Ohne Leine im festen Schema: 20 Schritte, Linkswendung, 10 Schritte, Anhalten, 10 Schritte, Rechtswendung, 20 Schritte, Kehrtwendung - danach dasselbe im Laufschritt ohne Anhalten. Im VK 2 gibt es nur ein Hörzeichen für Angehen und Gangartwechsel und keines für das Anhalten."),
            new("Sitz aus der Bewegung mit Abholen", ExerciseDifficulty.Beginner, "Unterordnung",
                "Nach 10 bis 15 Schritten Entwicklung setzt sich der Hund auf das Hörzeichen sofort in Laufrichtung, der Hundeführer entfernt sich 15 Schritte und holt ihn auf Anweisung des Richters ab. Die Hälfte der Punkte zählt für das Absitzen, die Hälfte für Entfernen, Abholen und Endgrundstellung."),
            new("Ablegen in Verbindung mit Herankommen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Nach 10 bis 15 Schritten Entwicklung legt sich der Hund sofort ab, der Hundeführer entfernt sich 30 Schritte und ruft ihn auf Anweisung des Richters heran; der Hund setzt sich vor oder geht direkt in die Grundstellung. Die Hälfte der Punkte zählt für das Ablegen, die Hälfte für Herankommen und Endgrundstellung."),
            new("Steh aus der Bewegung mit Abholen", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Nur im VK 2. Nach 10 bis 15 Schritten Entwicklung bleibt der Hund auf das Hörzeichen sofort und gerade stehen; der Hundeführer geht noch mindestens 15 Schritte, dreht sich um und holt ihn auf Anweisung des Richters ab."),
            new("Slalomlauf", ExerciseDifficulty.Intermediate, "Laufbewerbe",
                "Rund 75 m im Zickzack durch Start-, Ziel- und fünf Streckentore (lichte Weite 140 cm, Stangen mindestens 170 cm). Hund und Hundeführer laufen gemeinsam, der Hund frei; beide müssen jedes Tor in Laufrichtung passieren."),
            new("Hürdenlauf", ExerciseDifficulty.Intermediate, "Laufbewerbe",
                "60 m über vier 200 cm breite Hürden, hin und zurück um eine Wendestange (im Uhrzeigersinn). Höhe 30 cm in Gruppe 1, 40 cm in Gruppe 2 und 3. Hund und Hundeführer springen gemeinsam; in der Jugendklasse und ab der Altersklasse läuft der Hundeführer neben den Hürden mit."),
            new("Hindernislauf", ExerciseDifficulty.Advanced, "Laufbewerbe",
                "75 m mit acht Hindernissen für den Hund, der Hundeführer läuft neben der Bahn mit: zwei Hürden, Schrägwand (immer als zweites Gerät), Tunnel, Laufsteg, Halbtonne, Reifen und Hochweitsprung."),
            new("Karussell", ExerciseDifficulty.Advanced, "Laufbewerbe",
                "Zusatzgerät des CSC: Hürde, Wendestange, Hürde in einer Folge, die der Hund überwindet, während der Hundeführer rechts parallel mitläuft."),
            new("Weitsprung", ExerciseDifficulty.Intermediate, "Laufbewerbe",
                "Zusatzgerät im Shorty, alternativ zum Hochweitsprung nutzbar: 100 cm weit und 100 cm breit, beide Holme 30 cm hoch und zweifarbig."),
        ]);

        var datum = new DateOnly(2025, 1, 1);

        const string vierkampfAllgemein =
            "Aufbau: Unterordnung (max. 60 Punkte) plus drei Läufe mit Ausgangspunkten - Slalomlauf 110, Hürdenlauf 90, Hindernislauf 90 - abzüglich Laufzeit (eine Sekunde = ein Punkt) und Fehlerpunkten (S. 16, 28-30). Läufe immer mit freifolgendem Hund.\n" +
            "Als Sprintturnier (alles einmal) werden die Unterordnungspunkte mit 0,7 multipliziert; im vollen Vierkampf wird die Unterordnung einmal, jeder Lauf zweimal gezeigt, und die Unterordnung (mit 0,7) plus der erste Durchgang zählen als Prüfung (S. 14, 16).\n" +
            "Noten Vorzüglich, Sehr gut, Gut, ohne Bewertung nach der Gesamtpunktzahl, getrennt nach Altersklasse und Geschlecht, z. B. Aktivenklasse A männlich: Vorzüglich ab 286, Sehr gut 273-285,99, Gut 230-272,99 (S. 12). Altersklassen von Jugend bis Senioren (S. 14); Größengruppen nach Schulterhöhe: bis 30 cm, bis 50 cm, darüber (S. 14).\n" +
            "Voraussetzungen: Hund mindestens 15 Monate alt, bestandene BH/VT, Chip, Tollwutimpfung, Haftpflicht, Mitgliedschaft in einem ÖKV- oder ÖHU-Verein (S. 9).";

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV Breitensport VK 1", "2025", datum,
        [
            new("Leinenführigkeit", true, 15, "H-Schema angeleint (S. 20). Fehler: Zurückbleiben, Vor- oder Bedrängen, seitliches Abweichen, Zug an der Leine, zusätzliche Hörzeichen, Körperhilfen. Kehrtwendungen links drehend (S. 18)."),
            new("Freifolge", true, 20, "Festes Schema ohne Leine (S. 20 f.). Ein Hörzeichen für Angehen, Gangartwechsel und Anhalten ist erlaubt."),
            new("Sitz aus der Bewegung mit Abholen", true, 10, "S. 22. Zusätzlich zum Hörzeichen ist ein kurzes Sichtzeichen gestattet. Steht oder legt sich der Hund: zusätzlich 5 Punkte Abzug; nach dem dritten Hörzeichen ohne Ausführung: 0 Punkte."),
            new("Ablegen in Verbindung mit Herankommen", true, 15, "S. 22 f. Zusätzlich zum Hörzeichen ist ein kurzes Sichtzeichen gestattet. Sitzt oder steht der Hund: 50 % Abzug; kommt er auf das zweite Zusatzhörzeichen nicht: 0 Punkte."),
            new("Slalomlauf", true, 0, "Zeitgewertet, 110 Ausgangspunkte (S. 28). Jedes nicht durchlaufene Tor 5 Fehlerpunkte, ohne erkennbaren Versuch ausgelassen 20; vorteilhafte Berührung durch den Hundeführer 2."),
            new("Hürdenlauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 29). Die Ordnung nennt im Hürdenlauf selbst keine Fehlerpunkte für die Hürden."),
            new("Hindernislauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 30 f.). Ausgelassenes, nicht korrigiertes Hindernis 4 Fehlerpunkte, umgeworfene Hürde 4, vorteilhafte Berührung 2."),
        ],
        Description: "ÖKV-Breitensport-Vierkampf 1 (VK 1) - der leichtere Einstieg, ähnlich der alten Ordnung von 2011 (S. 5).\n" +
            "Unterordnung (max. 60 Punkte): Leinenführigkeit 15, Freifolge 20, Sitz aus der Bewegung 10, Ablegen in Verbindung mit Herankommen 15 (S. 19).\n" +
            "Gegenüber dem VK 2: mit Leinenführigkeit, zu den Positionsübungen ist ein kurzes Sichtzeichen erlaubt, Alternativgeräte (z. B. Agility-Wand) sind zulässig (S. 18, 27).\n" +
            "Bei hohem Starterfeld kann der Richter die Unterordnung ohne Leinenführigkeit durchführen; die Freifolge zählt dann höchstens 35 Punkte (S. 16).\n" +
            vierkampfAllgemein,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV Breitensport VK 2", "2025", datum,
        [
            new("Freifolge", true, 25, "Festes Schema ohne Leine, keine Leinenführigkeit (S. 21). Nur ein Kommando für das Angehen und die Gangartwechsel und eines für das Angehen nach dem Anhalten, für das Anhalten selbst keines."),
            new("Sitz aus der Bewegung mit Abholen", true, 10, "S. 22. Keine Sichtzeichen. Steht oder legt sich der Hund: zusätzlich 5 Punkte Abzug; nach dem dritten Hörzeichen ohne Ausführung: 0 Punkte."),
            new("Ablegen in Verbindung mit Herankommen", true, 15, "S. 22 f. Keine Sichtzeichen. Sitzt oder steht der Hund: 50 % Abzug; kommt er auf das zweite Zusatzhörzeichen nicht: 0 Punkte."),
            new("Steh aus der Bewegung mit Abholen", true, 10, "S. 23. Sitzt oder liegt der Hund: zusätzlich 5 Punkte Abzug; nach dem dritten Hörzeichen ohne Ausführung: 0 Punkte. Der Hundeführer nimmt den Hund von rechts mit Hörzeichen in die Grundstellung."),
            new("Slalomlauf", true, 0, "Zeitgewertet, 110 Ausgangspunkte (S. 28), wie im VK 1."),
            new("Hürdenlauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 29), wie im VK 1."),
            new("Hindernislauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 30 f.), wie im VK 1."),
        ],
        Description: "ÖKV-Breitensport-Vierkampf 2 (VK 2) - die anspruchsvollere Stufe, die Turnieren wie Österreichsieger und Staatsmeisterschaft mehr Gewicht geben soll (S. 5).\n" +
            "Unterordnung (max. 60 Punkte): Freifolge 25, Sitz aus der Bewegung 10, Ablegen in Verbindung mit Herankommen 15, Steh aus der Bewegung 10 (S. 19).\n" +
            "Gegenüber dem VK 1: keine Leinenführigkeit, dafür das Steh; keine Sichtzeichen; die Reihenfolge der drei Positionsübungen wird aus sechs Varianten ausgelost (S. 24); nur Geräte nach der Ordnung (S. 27).\n" +
            vierkampfAllgemein,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));

        const string offen =
            "Open-Bewerb: Kombinationen und einzelne Läufe zählen nicht als Prüfung, sondern werden als „Open“ ins Leistungsheft eingetragen; Gesamtnoten werden nicht vergeben (S. 14, 16).";

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV Breitensport Dreikampf", "2025", datum,
        [
            new("Slalomlauf", true, 0, "Zeitgewertet, 110 Ausgangspunkte (S. 28)."),
            new("Hürdenlauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 29)."),
            new("Hindernislauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 30 f.)."),
        ],
        Description: "ÖKV-Breitensport-Dreikampf - die drei Laufbewerbe des Vierkampfs ohne Unterordnung (S. 16).\n" +
            "Bewertung wie im Vierkampf: Ausgangspunkte (Slalomlauf 110, Hürdenlauf 90, Hindernislauf 90) abzüglich Laufzeit und Fehlerpunkten; Läufe mit freifolgendem Hund.\n" +
            offen,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV Slalomlauf", "2025", datum,
        [
            new("Slalomlauf", true, 0, "Zeitgewertet, 110 Ausgangspunkte (S. 28). Tor nicht durchlaufen 5 Fehlerpunkte, ohne erkennbaren Versuch ausgelassen 20, vorteilhafte Berührung 2."),
        ],
        Description: "ÖKV-Slalomlauf als einzelner Laufbewerb (S. 28).\n" +
            "Rund 75 m, Start- und Zieltor und fünf weitere Tore (lichte Weite 140 cm, Stangen mindestens 170 cm), Aufbaufläche 45 x 10 m. Die Strecke darf gespiegelt werden, wenn die Ausschreibung darauf hinweist.\n" +
            "Gemessen wird von der Startlinie (erster Partner) bis zur Ziellinie (letzter Partner); Endpunkte = 110 minus Laufzeitpunkte minus Fehlerpunkte. Fehler an Toren dürfen korrigiert werden.\n" +
            offen,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV Hürdenlauf", "2025", datum,
        [
            new("Hürdenlauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 29). Die Ordnung nennt im Hürdenlauf selbst keine Fehlerpunkte für die Hürden."),
        ],
        Description: "ÖKV-Hürdenlauf als einzelner Laufbewerb (S. 29).\n" +
            "60 m hin und zurück um eine Wendestange (im Uhrzeigersinn, 30 m je Richtung), vier 200 cm breite Hürden, Abstände je 10 m. Höhe 30 cm in Gruppe 1, 40 cm in Gruppe 2 und 3.\n" +
            "Hund und Hundeführer springen gemeinsam; in der Jugendklasse und ab der Altersklasse läuft der Hundeführer neben den Hürden. Dasselbe gilt für körperbehinderte Hundeführer mit Attest - dann werden 4 Sekunden zur Laufzeit gerechnet.\n" +
            "Endpunkte = 90 minus Laufzeitpunkte minus Fehlerpunkte.\n" +
            offen,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV Hindernislauf", "2025", datum,
        [
            new("Hindernislauf", true, 0, "Zeitgewertet, 90 Ausgangspunkte (S. 30 f.). Ausgelassenes, nicht korrigiertes Hindernis 4 Fehlerpunkte, umgeworfene Hürde 4, vorteilhafte Berührung durch den Hundeführer 2."),
        ],
        Description: "ÖKV-Hindernislauf als einzelner Laufbewerb (S. 30 f.).\n" +
            "75 m mit acht Hindernissen: zwei Hürden (30/40/50 cm je nach Gruppe), Schrägwand (immer an zweiter Stelle), Tunnel, Laufsteg, Halbtonne, Reifen und Hochweitsprung. Das erste Hindernis steht 3 m hinter der Startlinie, das letzte 3 m vor dem Ziel; der Hundeführer läuft neben der Bahn.\n" +
            "Der Laufsteg wird in vier Abschnitte geteilt, jeder nicht korrekt absolvierte Abschnitt zählt 1 Fehlerpunkt, bloßes seitliches Überspringen 4 (S. 31).\n" +
            "Endpunkte = 90 minus Laufzeitpunkte minus Fehlerpunkte.\n" +
            offen,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV CSC", "2025", datum,
        [
            new("Slalomlauf", true, 0, "Fehler analog Slalomlauf: 5 Fehlersekunden (S. 33)."),
            new("Hürdenlauf", true, 0, "Hürdenfehler analog Hürdenlauf: 4 Fehlersekunden. In Sektion 2 sind nach der Wendestange drei 30 cm hohe Hürden gemeinsam, aber nicht zeitgleich zu überspringen (S. 32 f.)."),
            new("Hindernislauf", true, 0, "Auslassen eines Geräts analog Hindernislauf: 4 Fehlersekunden (S. 33)."),
            new("Karussell", true, 0, "Zusatzgerät des CSC (Hürde, Wendestange, Hürde), S. 32."),
        ],
        Description: "ÖKV-CSC (Kombinations-Speed-Cup) - Staffellauf aus den drei Laufelementen des Vierkampfs (S. 32).\n" +
            "Eine Mannschaft besteht aus drei Teilnehmern mit drei verschiedenen Hunden, die den in drei Sektionen geteilten Parcours als Staffel laufen; zwei Durchgänge, Hunde frei folgend, der Hundeführer läuft rechts parallel. Die Zusammensetzung (z. B. Jugendlicher, Frau, Mann) kann die Ausschreibung festlegen.\n" +
            "Ein Helfer gibt die nächste Sektion frei, sobald der zuletzt ankommende Partner das Wechseltor erreicht hat. Start- und Zieltor müssen von Hund und Hundeführer in Laufrichtung durchlaufen werden.\n" +
            "Fehler werden zur Laufzeit gerechnet: Auslassen der Wendestange 10 Fehlersekunden, Auslassen des Wechseltors durch den Hund 4, Festhalten des Hundes nach dem Starttor 2 Fehlerpunkte, Frühstart eine Fehlersekunde (mehr als ein Frühstart: Ausscheiden). Platzierung nach der kürzesten Gesamtzeit beider Durchgänge (S. 33).\n" +
            "Ein Leistungsheft-Eintrag ist nur beim Standardaufbau mit zwei Durchgängen erlaubt (S. 32).\n" +
            offen,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));

        await SeedRegulationAsync(db, breitensport, new RegulationSeed("ÖKV Shorty", "2025", datum,
        [
            new("Hindernislauf", true, 0, "Hindernisse des Vierkampfs; Auslassen eines Geräts wie im CSC 4 Fehlersekunden (S. 34)."),
            new("Weitsprung", true, 0, "Zusätzlich zu den Hindernissen des Vierkampfs; Abwerfen oder Unterlaufen einer Stange 1 Fehlerpunkt, zweier Stangen 2 (S. 31, 34)."),
        ],
        Description: "ÖKV-Shorty - Kurzbahn-CSC mit zwei Sektionen für Zweier-Teams (S. 34).\n" +
            "Zwei Teams (je Hundeführer und Hund) laufen die Staffel über zwei Sektionen; die Hunde überwinden Hindernisse des Vierkampfs plus Weitsprung, der Hundeführer läuft rechts parallel. Zwei Durchgänge, jedes Team läuft zweimal dieselbe Sektion, Hunde frei folgend. Ein Hund startet nur mit einem Hundeführer (S. 9).\n" +
            "Gesamtzeit = Laufzeiten plus Fehlersekunden beider Durchgänge. Startfreigabe, Wechsel, Frühstart, Fehlerbewertung und Platzierung wie im CSC. Die Geräteanordnung steht nur in den Plänen im Anhang (S. 38) und ist bindend.\n" +
            offen,
        Land: Oesterreich,
        SourceUrl: QuelleBreitensport));
    }
}

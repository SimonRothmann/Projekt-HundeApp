using Dogity.Domain.Sports;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Jagd (Paket JK1, 2026-11-23): die Prüfungsordnungen des Jagdgebrauchshundverbands (JGHV) in einer
/// Sportart <c>JAGD</c> - Verbandsjugendprüfung (VJP) und Herbstzuchtprüfung (HZP) nach der VZPO,
/// Verbandsgebrauchsprüfung (VGP) nach der VGPO, Verbandsschweißprüfung (VSwP) und
/// Verbandsfährtenschuhprüfung (VFsP) nach der VSwPO/VFsPO, die beiden letzten je über 20 und 40 Stunden.
///
/// Die Übungen sind die Prüfungsfächer der Ordnungen; ein Fach, das in mehreren Ordnungen vorkommt
/// (Nasengebrauch, Führigkeit, Schussfestigkeit ...), ist dieselbe Übung. Kategorien: Feld, Wald, Wasser,
/// Gehorsam und Schweiß (Riemenarbeit auf der Wundfährte).
///
/// Bewertung: Der JGHV wertet Leistung mal Bedeutung. Der Richter vergibt je Fach Arbeitspunkte (VZPO:
/// 0 bis 12) bzw. Leistungsziffern (VGPO: 0 bis 4); sie werden mit der Fachwertziffer des Fachs
/// multipliziert. Als <c>MaxPoints</c> steht hier die höchste erreichbare Wertungspunktzahl des Fachs:
/// bei der VGP Fachwertziffer mal 4 (die Summen stimmen mit den Höchstpunktzahlen der Ordnung überein:
/// Wald 124, Wasser 44, Feld 104, Gehorsam 68, zusammen 340); bei VJP/HZP Fachwertziffer mal 12 in
/// Spurarbeit, Nasengebrauch und Stöbern mit Ente (nur dort darf "hervorragend" vergeben werden), mal 11
/// in den übrigen Anlagefächern und mal 10 in den Abrichtefächern (dort ist 10 das höchste Prädikat). Diese
/// Zahlen sind aus den Regeln der Ordnung abgeleitet, keine Angabe der Ordnung selbst; die Ordnung nennt
/// nur Ziffern und Punkteskala. Fächer ohne Fachwertziffer (Gehorsam und Schussfestigkeit der VJP, Prädikat
/// ohne Punkte) sowie Fächer, die ein anderes ersetzen oder nur zusätzlich zählen (Tagfährte statt
/// Übernachtfährte, Federwildschleppe, Totverbellen, Totverweisen), tragen 0 Punkte, damit die Summe stimmt;
/// ihre Ziffer steht in den Hinweisen. VSwP und VFsP kennen gar keine Punkte (Prädikate "zum Stück
/// gekommen"): alle Fächer 0, wie Teil B der BH.
///
/// Quellen und Fassungen (siehe docs/PO_VERIFIKATION.md, Abschnitt "Jagd (JGHV)"): Der JGHV untersagt
/// den Nachdruck auch auszugsweise und die Verarbeitung in elektronischen Systemen ohne Genehmigung.
/// Hinterlegt sind deshalb nur Fakten (Fächer, Fachwertziffern, Zulassung, Kennzahlen der Fährte,
/// Preisgrenzen) und eigene Beschreibungen - keine Textpassagen.
///
/// Land: JGHV-Ordnungen gelten für Deutschland ("DE", siehe <see cref="FciKatalog.GeltungsbereichVon"/>).
/// Englische Namen gibt es nicht: Die Kürzel der Ordnungen sind in beiden Sprachen gleich.
/// </summary>
public static partial class SportCatalogSeeder
{
    /// <summary>Sportart-Code der Jagd - Schweißfährten (Paket SF1) ordnen ihre Leitfäden den Ordnungen dieser Sportart zu.</summary>
    public const string JagdCode = "JAGD";

    private const string QuelleJghvVzpo = "https://jghv.de/images/managed/dateien/2024/service/pruefungsordnungen-des-jghv/VZPO_2018_Druck_3.pdf";

    private const string QuelleJghvVgpo = "https://www.jghv.de/images/managed/dateien/2024/service/pruefungsordnungen-des-jghv/VGPO_VPSO_2018_01.pdf";

    private const string QuelleJghvVswpo = "https://www.jghv.de/images/managed/dateien/2026/service/pruefungsordnungen-des-jghv/VSwPO_VFsPO_06.pdf";

    private static async Task SeedJagdAsync(ApplicationDbContext db)
    {
        var jagd = await SeedSportAsync(db, JagdCode, "Jagd (JGHV)",
        [
            // ---- Feld und Anlagen (VJP, HZP, VGP) ----
            new("Spurarbeit", ExerciseDifficulty.Intermediate, "Feld",
                "Arbeit auf der Spur von Hase oder Fuchs, die der Hund nur mit der Nase wahrnimmt: Spurwille und Spursicherheit, ruhig und selbständig, auch bei Schwierigkeiten."),
            new("Nasengebrauch", ExerciseDifficulty.Intermediate, "Feld",
                "Wie fein der Hund Wild und Witterung findet und anzeigt: weites Anziehen, kurzes Markieren von Witterungsstellen, bedächtiges Aufnehmen und Halten der Spur."),
            new("Suche", ExerciseDifficulty.Intermediate, "Feld",
                "Der Finderwille bei der Suche im Feld: planmäßig, dem Gelände und dem Wind angepasst, mit passendem Tempo und lenkbar."),
            new("Vorstehen", ExerciseDifficulty.Intermediate, "Feld",
                "Gefundenes Wild vorstehen oder vorliegen, bis der Führer ruhig herankommt - an Haar- und Federwild gleich bewertet. Blinken führt zum Ausschluss."),
            new("Manieren am Wild und Nachziehen", ExerciseDifficulty.Advanced, "Feld",
                "Haltung und Bewegung bei Wildwitterung sowie ruhiges Nachziehen und Festmachen von Wild, das vor dem Hund abläuft."),
            new("Arbeitsfreude", ExerciseDifficulty.Beginner, "Feld",
                "Arbeitslust und Arbeitswille, die der Hund über die ganze Prüfung in allen Fächern zeigt."),
            new("Führigkeit", ExerciseDifficulty.Beginner, "Gehorsam",
                "Das Bestreben des Hundes, Verbindung zum Führer zu halten: Blickkontakt bei Suche und Vorstehen, Anschluss nach dem Verfolgen von Wild."),
            new("Schussfestigkeit", ExerciseDifficulty.Beginner, "Gehorsam",
                "Reaktion auf Schrotschüsse während der Suche im Feld (und auf dem Wasser): schussfest, leicht beeindruckt oder schussscheu. Schussscheue Hunde bestehen nicht."),

            // ---- Gehorsam ----
            new("Allgemeines Verhalten - Gehorsam", ExerciseDifficulty.Beginner, "Gehorsam",
                "Lenkbarkeit und Folgsamkeit über die gesamte Prüfung - auch das Verhalten der Hunde, die gerade nicht arbeiten."),
            new("Verhalten auf dem Stand", ExerciseDifficulty.Beginner, "Gehorsam",
                "Ruhiges Verhalten am Stand, während geschossen wird und andere Hunde arbeiten."),
            new("Leinenführigkeit", ExerciseDifficulty.Beginner, "Gehorsam",
                "Der Hund geht an der Leine ruhig und ohne zu ziehen neben dem Führer."),
            new("Folgen frei bei Fuß", ExerciseDifficulty.Intermediate, "Gehorsam",
                "Der Hund folgt ohne Leine dicht bei Fuß, auch bei Wendungen und im Gelände."),
            new("Ablegen", ExerciseDifficulty.Intermediate, "Gehorsam",
                "Der Hund bleibt abgelegt, während sich der Führer entfernt und geschossen wird."),
            new("Benehmen vor eräugtem Federwild", ExerciseDifficulty.Intermediate, "Gehorsam",
                "Der Hund prellt abstreichendem Federwild ohne Einwirkung nicht nach."),
            new("Benehmen vor eräugtem Haarnutzwild", ExerciseDifficulty.Advanced, "Gehorsam",
                "Der Hund verfolgt sichtig wahrgenommenes Haarnutzwild nicht und bleibt unter der Einwirkung des Führers."),
            new("Schussruhe", ExerciseDifficulty.Intermediate, "Gehorsam",
                "Der Hund bleibt beim Schuss und beim Fallen des Wildes ruhig und wartet auf das Kommando."),

            // ---- Wald (VGP; Haarwildschleppe auch auf der HZP) ----
            new("Bringen von Fuchs über Hindernis", ExerciseDifficulty.Advanced, "Wald",
                "Der Hund bringt einen Fuchs, der hinter einem Hindernis liegt, über das Hindernis zum Führer."),
            new("Fuchsschleppe", ExerciseDifficulty.Advanced, "Wald",
                "Arbeit auf der Schleppe eines Fuchses bis zum Stück."),
            new("Bringen von Fuchs auf der Schleppe", ExerciseDifficulty.Advanced, "Wald",
                "Der Hund bringt den Fuchs, den er auf der Schleppe gefunden hat, zum Führer."),
            new("Hasen- oder Kaninchenschleppe", ExerciseDifficulty.Intermediate, "Wald",
                "Arbeit auf der Schleppe von Hase oder Kaninchen bis zum Stück: ca. 300 m mit zwei stumpfwinkligen Haken."),
            new("Bringen von Hase oder Kaninchen", ExerciseDifficulty.Intermediate, "Wald",
                "Der Hund bringt das gefundene Stück Haarwild sicher und korrekt zum Führer."),
            new("Stöbern", ExerciseDifficulty.Intermediate, "Wald",
                "Selbständiges Absuchen einer Dickung nach Wild, ohne weit weg vom Führer zu verschwinden, und Rückkehr auf Ruf."),
            new("Buschieren", ExerciseDifficulty.Intermediate, "Wald",
                "Gründliches Absuchen von Deckung vor dem Führer in Schussentfernung, wie in der Jagdpraxis üblich."),

            // ---- Wasser (HZP, VGP) ----
            new("Stöbern ohne Ente im deckungsreichen Gewässer", ExerciseDifficulty.Intermediate, "Wasser",
                "Auf einmaligen Befehl in Schilf und Deckung des Gewässers suchen, ohne dass Wild ausgelegt ist."),
            new("Verlorensuchen im deckungsreichen Gewässer", ExerciseDifficulty.Advanced, "Wasser",
                "Eine frisch erlegte Ente, die der Hund nicht gesehen hat, selbständig in der Deckung finden und bringen."),
            new("Stöbern mit Ente im deckungsreichen Gewässer", ExerciseDifficulty.Advanced, "Wasser",
                "Hinter einer ausgesetzten, nicht sichtigen Ente in der Deckung arbeiten, bis sie gegriffen oder herausgedrückt wird."),
            new("Bringen von Ente", ExerciseDifficulty.Intermediate, "Wasser",
                "Die gefundene Ente aufnehmen und sicher und korrekt zum Führer bringen."),

            // ---- Bringen und Federwild (HZP, VGP) ----
            new("Verlorenbringen von Federwild", ExerciseDifficulty.Intermediate, "Feld",
                "Fach der HZP: Federwild finden und bringen - gewählt wird eine der Arbeiten (Huhn oder Fasan, freies Verlorensuchen, Federwildschleppe)."),
            new("Art des Bringens", ExerciseDifficulty.Intermediate, "Feld",
                "Fach der HZP: Ausführung des Bringens, als Durchschnitt der Noten für Hase, Ente und Federwild."),
            new("Arbeit am geflügelten Federwild", ExerciseDifficulty.Advanced, "Feld",
                "Arbeit am angeschossenen, noch lebenden Huhn oder Fasan (VGP)."),
            new("Federwildschleppe", ExerciseDifficulty.Intermediate, "Feld",
                "Arbeit auf der Schleppe eines Stücks Federwild: ca. 200 m auf bewachsenem Boden mit zwei stumpfwinkligen Haken."),
            new("Freies Verlorensuchen von Federwild", ExerciseDifficulty.Advanced, "Feld",
                "Ein frisch geschossenes oder ausgelegtes Stück Federwild in Deckung selbständig suchen und bringen."),
            new("Bringen von Federwild", ExerciseDifficulty.Intermediate, "Feld",
                "Die Ausführung des Bringens aller Federwildarbeiten des Feldes als Gesamtnote."),

            // ---- Schweiß (VGP, VSwP, VFsP) ----
            new("Riemenarbeit auf der Übernachtfährte", ExerciseDifficulty.Advanced, "Schweiß",
                "Arbeit an der Wundfährte am mindestens 6 m langen Schweißriemen auf der über Nacht gestandenen Fährte (VGP)."),
            new("Riemenarbeit auf der Tagfährte", ExerciseDifficulty.Advanced, "Schweiß",
                "Wie die Übernachtfährte, aber auf einer Fährte, die nur wenige Stunden gestanden hat (VGP)."),
            new("Totverbellen", ExerciseDifficulty.Advanced, "Schweiß",
                "Zusatzfach der VGP nach der Riemenarbeit: Der Hund wird frei geschnallt, findet das Stück und gibt anhaltend Laut, bis der Führer kommt."),
            new("Totverweisen", ExerciseDifficulty.Advanced, "Schweiß",
                "Zusatzfach der VGP nach der Riemenarbeit: Der Hund findet das Stück frei und führt den Führer auf die vorher erklärte Weise dorthin."),
            new("Anschuss suchen und ansprechen", ExerciseDifficulty.Intermediate, "Schweiß",
                "Das Gespann sucht in einer markierten Fläche selbständig den Anschuss; der Führer erkennt ihn und spricht ihn an (VSwP/VFsP)."),
            new("Riemenarbeit: Fährtenwille und Fährtensicherheit", ExerciseDifficulty.Advanced, "Schweiß",
                "Die Arbeit auf der Wundfährte über mindestens 1.000 m: Fährtenwille, -ruhe, -sicherheit und Selbständigkeit (VSwP/VFsP)."),
            new("Selbstverbessern bei Verlust der Fährte", ExerciseDifficulty.Advanced, "Schweiß",
                "Wie der Hund die verlorene Fährte durch Bogenschlagen oder Vor- und Zurückgreifen wiederfindet und sich auf Verleitfährten selbst verbessert."),
            new("Verweisen von Wundbetten und Verweiserpunkten", ExerciseDifficulty.Advanced, "Schweiß",
                "Der Hund zeigt Wundbetten und Verweiserpunkte (bei der VFsP Tropfbetten) und Pirschzeichen an."),
            new("Zusammenarbeit von Führer und Hund", ExerciseDifficulty.Intermediate, "Schweiß",
                "Gerechte Führung am Riemen: Hilfen, Zurücknehmen und Ablegen, ohne dem Hund die Arbeit abzunehmen."),
            new("Erreichen des Stücks", ExerciseDifficulty.Intermediate, "Schweiß",
                "Das Gespann kommt zum Stück am Ende der Fährte; wer es erreicht, hat die Prüfung bestanden."),
        ]);

        // ---- VJP: Verbandsjugendprüfung (VZPO 2018) ----
        // Die VZPO nennt bei der VJP nur die Fachwertziffern, Prädikate und die Punkteskala; eine
        // Gesamtpunktzahl zum Bestehen steht nicht darin (das Zeugnis trägt "bestanden mit ... Punkten").
        const string vzpoBewertung =
            "Bewertung: Je Fach vergibt der Richter Arbeitspunkte von 0 bis 12 (hervorragend 12; sehr gut 11, 10, 9; gut 8, 7, 6; genügend 5, 4, 3; nicht genügend 2, 1, 0). " +
            "Sie werden mit der Fachwertziffer des Fachs multipliziert (Wertungspunkte). \"Hervorragend\" (12) gibt es nur in Spurarbeit, Nasengebrauch und Stöbern mit Ente; sonst ist 11 das Höchste, bei den Abrichtefächern der HZP 10. " +
            "Die Höchstpunkte im Katalog sind Fachwertziffer mal diese Obergrenze.\n" +
            "Bestehen: in jedem Fach mindestens genügend (3 Arbeitspunkte); Blinken oder schussscheu führen zum Nichtbestehen. Eine Gesamtpunktzahl zum Bestehen nennt die Ordnung nicht - die Richtersitzung stuft ein. " +
            "Festgestellt werden zudem Laut (spurlaut, sichtlaut, fraglich, stumm, waidlaut), Wesen und Verhalten, körperliche Mängel und die Identität.\n" +
            "Zulassung: Hunde, die im Vorjahr gewölft sind (bis drei Monate ältere ebenfalls); zur HZP auch Hunde des laufenden Jahres. Ein Führer darf höchstens zwei Hunde führen, der Jagdschein ist vorzulegen.\n" +
            "Fassung: VZPO, Ordnung für Verbandszuchtprüfungen, gültig vom 01.01.2018 bis 31.12.2027. Der JGHV hat 2026 eine Änderung vorgelegt; hier gilt die veröffentlichte Fassung 2018.";

        await SeedRegulationAsync(db, jagd, new RegulationSeed("JGHV VJP", "2018", new DateOnly(2018, 1, 1),
        [
            new("Spurarbeit", true, 24, "Fachwertziffer 2. Spurwille und Spursicherheit; der Führer darf den Hund bis 30 m an der Leine arbeiten."),
            new("Nasengebrauch", true, 24, "Fachwertziffer 2."),
            new("Suche", true, 11, "Fachwertziffer 1. Eine planmäßige Suche wird noch nicht erwartet; überwiegende Trabsuche höchstens gut (7 Punkte)."),
            new("Vorstehen", true, 11, "Fachwertziffer 1. Durchstehen wird nicht verlangt."),
            new("Führigkeit", true, 11, "Fachwertziffer 1."),
            new("Allgemeines Verhalten - Gehorsam", true, 0, "Ohne Fachwertziffer: nur ein Prädikat (sehr gut bis nicht genügend), keine Punkte. Ein dauerhaft nicht genügender Gehorsam schließt vom Weiterprüfen aus."),
            new("Schussfestigkeit", true, 0, "Ohne Fachwertziffer: nur festgestellt (mindestens zwei Schrotschüsse im Abstand von 20 Sekunden in der Nähe des suchenden Hundes). Schussscheue Hunde bestehen nicht."),
        ],
            Description: "JGHV-Verbandsjugendprüfung (VJP) - Zuchtprüfung für Junghunde: Es werden die natürlichen Anlagen beurteilt (Spurwille, Nasengebrauch, Suche, Vorstehen, Führigkeit), nicht die Ausbildung.\n" +
                "Fachwertziffern: Spurarbeit 2, Nasengebrauch 2, Suche 1, Vorstehen 1, Führigkeit 1; Gehorsam und Schussfestigkeit ohne Ziffer.\n" +
                vzpoBewertung,
            SourceUrl: QuelleJghvVzpo));

        // ---- HZP: Herbstzuchtprüfung (VZPO 2018) ----
        await SeedRegulationAsync(db, jagd, new RegulationSeed("JGHV HZP", "2018", new DateOnly(2018, 1, 1),
        [
            // Anlagefächer
            new("Spurarbeit", true, 36, "Fachwertziffer 3. Die Spurarbeit wird nur geprüft, wenn der Führer sie bei der Nennung verbindlich gewählt hat."),
            new("Nasengebrauch", true, 36, "Fachwertziffer 3. Für 12 Punkte ist auch die Nasenarbeit bei der Wasserarbeit mit mindestens 10 Punkten Voraussetzung."),
            new("Suche", true, 22, "Fachwertziffer 2. Eine planmäßige Suche wird erwartet; überwiegende Trabsuche höchstens gut (7 Punkte)."),
            new("Vorstehen", true, 22, "Fachwertziffer 2."),
            new("Führigkeit", true, 22, "Fachwertziffer 2."),
            new("Arbeitsfreude", true, 11, "Fachwertziffer 1. Wird über die ganze Prüfung beobachtet."),
            new("Stöbern mit Ente im deckungsreichen Gewässer", true, 36, "Fachwertziffer 3 (Wasserarbeit, Anlagefach)."),
            // Abrichtefächer
            new("Verlorensuchen im deckungsreichen Gewässer", true, 10, "Fachwertziffer 1 (Abrichtefach). Wer die Ente beim ersten Finden nicht bringt, besteht nicht; ohne mindestens genügend wird nicht weiter am Wasser geprüft."),
            new("Verlorenbringen von Federwild", true, 10, "Fachwertziffer 1 (Abrichtefach); eine der drei Arbeiten genügt."),
            new("Hasen- oder Kaninchenschleppe", true, 10, "Fachwertziffer 1 (Abrichtefach); Schleppe ca. 300 m mit zwei stumpfwinkligen Haken."),
            new("Art des Bringens", true, 10, "Fachwertziffer 1 (Abrichtefach): Durchschnitt aus Hase, Ente und Federwild; jedes Einzelbringen mindestens genügend."),
            new("Allgemeines Verhalten - Gehorsam", true, 10, "Fachwertziffer 1 (Abrichtefach)."),
            new("Schussfestigkeit", true, 0, "Ohne Fachwertziffer: wird bei der Feld- und der Wasserarbeit festgestellt."),
        ],
            Description: "JGHV-Herbstzuchtprüfung (HZP) - Zuchtprüfung im Herbst: Entwicklung der Anlagen des Junghundes und Stand der Ausbildung für den vielseitigen Jagdgebrauch, mit Feld- und Wasserarbeit.\n" +
                "Fachwertziffern: Anlagefächer - Spurarbeit 3, Nasengebrauch 3, Suche 2, Vorstehen 2, Führigkeit 2, Arbeitsfreude 1, Stöbern mit Ente 3; Abrichtefächer - Verlorensuchen im Wasser 1, Verlorenbringen von Federwild 1, Haarwildschleppe 1, Art des Bringens 1, Gehorsam 1. Schussfestigkeit wird zusätzlich geprüft.\n" +
                "Die HZP kann mit oder ohne Spurarbeit abgelegt werden; das Zeugnis vermerkt es.\n" +
                vzpoBewertung,
            SourceUrl: QuelleJghvVzpo));

        // ---- VGP: Verbandsgebrauchsprüfung (VGPO 2018, gültig bis 31.12.2027) ----
        await SeedRegulationAsync(db, jagd, new RegulationSeed("JGHV VGP", "2018", new DateOnly(2018, 1, 1),
        [
            // Fachgruppe Wald (124)
            new("Riemenarbeit auf der Übernachtfährte", true, 32, "Fachwertziffer 8. Ca. 400 m, jeder Hund arbeitet sie, auch Totverbeller und Totverweiser. Zweimaliges Zurücknehmen und Neuanlegen ist erlaubt, mindert aber das Prädikat; Riemen kürzer als 6 m: höchstens genügend."),
            new("Riemenarbeit auf der Tagfährte", false, 0, "Fachwertziffer 5 (bis 20 Punkte) statt der Übernachtfährte; Stehzeit ca. 2 bis 5 Stunden. Zählt nicht zusätzlich zur Summe."),
            new("Totverbellen", false, 0, "Zusatzfach, Fachwertziffer 4: bis zu 16 Zusatzpunkte, die bei der Einstufung zählen (nicht zu den Mindestpunktzahlen); nur bei Mindestprädikat genügend."),
            new("Totverweisen", false, 0, "Zusatzfach, Fachwertziffer 3: bis zu 12 Zusatzpunkte, sonst wie Totverbellen. Der Führer erklärt vorher verbindlich, woran er das Verweisen erkennt."),
            new("Bringen von Fuchs über Hindernis", true, 12, "Fachwertziffer 3 (nur VGP)."),
            new("Fuchsschleppe", true, 20, "Fachwertziffer 5."),
            new("Bringen von Fuchs auf der Schleppe", true, 8, "Fachwertziffer 2."),
            new("Hasen- oder Kaninchenschleppe", true, 16, "Fachwertziffer 4."),
            new("Bringen von Hase oder Kaninchen", true, 8, "Fachwertziffer 2."),
            new("Stöbern", true, 16, "Fachwertziffer 4."),
            new("Buschieren", true, 12, "Fachwertziffer 3."),
            // Fachgruppe Wasser (44)
            new("Schussfestigkeit", true, 0, "Wird auf dem Wasser festgestellt, aber nicht bewertet; für jeden Preis muss sie bestanden sein."),
            new("Stöbern ohne Ente im deckungsreichen Gewässer", true, 12, "Fachwertziffer 3."),
            new("Verlorensuchen im deckungsreichen Gewässer", true, 12, "Fachwertziffer 3."),
            new("Stöbern mit Ente im deckungsreichen Gewässer", true, 12, "Fachwertziffer 3; kann auch durch ein beigelegtes Zeugnis nachgewiesen werden."),
            new("Bringen von Ente", true, 8, "Fachwertziffer 2."),
            // Fachgruppe Feld (104)
            new("Nasengebrauch", true, 24, "Fachwertziffer 6 (nur VGP)."),
            new("Suche", true, 16, "Fachwertziffer 4 (nur VGP)."),
            new("Vorstehen", true, 16, "Fachwertziffer 4 (nur VGP)."),
            new("Manieren am Wild und Nachziehen", true, 12, "Fachwertziffer 3 (nur VGP)."),
            new("Arbeit am geflügelten Federwild", true, 16, "Fachwertziffer 4."),
            new("Federwildschleppe", false, 0, "Fachwertziffer 3 (bis 12 Punkte) als Ersatz, wenn die Arbeit am geflügelten Federwild nicht möglich ist; ca. 200 m. Zählt nicht zusätzlich zur Summe."),
            new("Freies Verlorensuchen von Federwild", true, 12, "Fachwertziffer 3; ein frisch geschossenes Stück (nur VGP) oder ersatzweise ein ausgelegtes."),
            new("Bringen von Federwild", true, 8, "Fachwertziffer 2; wird eine Einzelarbeit mit nicht genügend bewertet, kann auch die Gesamtnote nur nicht genügend sein."),
            // Fachgruppe Gehorsam (68)
            new("Allgemeines Verhalten - Gehorsam", true, 12, "Fachwertziffer 3."),
            new("Verhalten auf dem Stand", true, 8, "Fachwertziffer 2."),
            new("Leinenführigkeit", true, 4, "Fachwertziffer 1."),
            new("Folgen frei bei Fuß", true, 8, "Fachwertziffer 2."),
            new("Ablegen", true, 8, "Fachwertziffer 2."),
            new("Benehmen vor eräugtem Federwild", true, 8, "Fachwertziffer 2 (nur VGP)."),
            new("Benehmen vor eräugtem Haarnutzwild", true, 12, "Fachwertziffer 3 (nur VGP)."),
            new("Schussruhe", true, 8, "Fachwertziffer 2 (nur VGP). Für den Preis genügt mindestens genügend in einem der Fächer Federwild, Haarnutzwild oder Schussruhe."),
        ],
            Description: "JGHV-Verbandsgebrauchsprüfung (VGP) - Leistungsprüfung: Der Hund soll zeigen, dass er in der Praxis als vielseitiger Jagdgebrauchshund taugt. Vier Fachgruppen: Wald (mit Schweißarbeit), Wasser, Feld und Gehorsam; mindestens zwei Prüfungstage, ab 1. September.\n" +
                "Fachwertziffern (je Fach bei den Übungen): Riemenarbeit Übernachtfährte 8, Fuchsschleppe 5, Nasengebrauch 6; die übrigen Fächer 1 bis 4.\n" +
                "Schweißfährte: ca. 400 m im Wald, zwei stumpfwinklige Haken, ein Wundbett, höchstens ein Viertelliter Schweiß; Stehzeit auf der Übernachtfährte mindestens 14 Stunden, auf der Tagfährte ca. 2 bis 5 Stunden; Schweißriemen mindestens 6 m. Totverbeller und Totverweiser arbeiten danach ca. 200 m frei (zweites Wundbett).\n" +
                "Bewertung: Leistungsziffern 0 bis 4 je Fach (hervorragend 4h und sehr gut 4, gut 3, genügend 2, mangelhaft 1, nicht genügend 0), multipliziert mit der Fachwertziffer. Die Höchstpunkte im Katalog sind Ziffer mal 4; zusammen 340 (Wald 124, Wasser 44, Feld 104, Gehorsam 68), mit Totverbellen 356, mit Totverweisen 352. 4h darf nicht in Schleppen-, Bring- und Gehorsamsfächern vergeben werden.\n" +
                "Preise nach Gesamtpunktzahl: I. Preis ab 254, II. Preis ab 220, III. Preis ab 163 - dazu Mindestpunkte je Fachgruppe (I./II./III. Preis): Wald 90/80/48, Wasser 36/30/22, Feld 85/70/55, Gehorsam 43/40/38, und Mindestprädikate: I. Preis Riemenarbeit sehr gut, II. Preis Riemenarbeit gut, III. Preis Riemenarbeit mindestens genügend; Schussfestigkeit auf dem Wasser muss bestanden sein; Nasengebrauch, Suche und Vorstehen müssen für den I. Preis gut sein, Nasengebrauch und Vorstehen für den II. Preis. Zusatzpunkte von Totverbellen und Totverweisen zählen nur bei der Einstufung.\n" +
                "Zulassung: nicht im selben Jahr gewölft; Führer mit gültigem Jagdschein; Meldung als Riemenarbeiter, Totverbeller oder Totverweiser; höchstens 20 Hunde je Prüfung.\n" +
                "Fassung: VGPO/VPSO, gültig vom 01.01.2018 bis 31.12.2027. Die Verbandsprüfung nach dem Schuss (VPS) ist nicht abgebildet.",
            SourceUrl: QuelleJghvVgpo));

        // ---- VSwP / VFsP: Verbandsschweißprüfung und Verbandsfährtenschuhprüfung (VSwPO/VFsPO 2026) ----
        // Es gibt keine Punkte: Gewertet wird mit Prädikaten ("zum Stück gekommen"). Alle Fächer tragen deshalb
        // 0 Punkte; die Beschreibung sagt, was gewertet wird.
        RegulationExerciseSeed[] Schweiss(string verweisen) =>
        [
            new("Anschuss suchen und ansprechen", true, 0, "Das Gespann sucht in einer ca. 30 x 30 m großen, markierten Fläche selbständig den Anschuss oder den Fährtenabgang; ein gefundener Anschuss soll erkannt und angesprochen werden."),
            new("Riemenarbeit: Fährtenwille und Fährtensicherheit", true, 0, "Reine Riemenarbeit am mindestens 6 m langen, in ganzer Länge abgedockten Riemen. Besonderen Wert hat die Arbeitsweise: Fährtenwille, -ruhe, -sicherheit und Selbständigkeit. Hastige Arbeit kann kein sehr gut bekommen; übermäßiges Tempo, vieles Zurückgreifen, ständiges Ablegen und jeder Rückruf mindern das Prädikat."),
            new("Selbstverbessern bei Verlust der Fährte", true, 0, "Kommt der Hund von der Fährte ab und verbessert sich nicht innerhalb von 80 bis 100 m selbst, erhält das Gespann einen Rückruf; mehr als zwei Rückrufe bedeuten nicht bestanden."),
            new("Verweisen von Wundbetten und Verweiserpunkten", true, 0, verweisen),
            new("Zusammenarbeit von Führer und Hund", true, 0, "Gerechtes Führen am Riemen; der Führer darf zurückgreifen, vorsuchen und den Hund zeitweise ablegen. Pirschzeichen meldet er, die Richter bestätigen sie nicht."),
            new("Erreichen des Stücks", true, 0, "Wer mit dem Hund zum Stück kommt, hat bestanden. Bei nicht genügender Leistung brechen die Richter die Arbeit ab."),
        ];

        const string schweissBewertung =
            "Bewertung: keine Punkte. Prädikate: sehr gut, gut, genügend jeweils \"zum Stück gekommen\"; nicht genügend = Fehlsuche. Wer das Stück erreicht, hat bestanden; mehr als zwei Rückrufe insgesamt bedeuten nicht bestanden. Eine nicht erfüllte Sollbestimmung mindert das Prädikat, eine nicht erfüllte Mussbestimmung führt zum Nichtbestehen. Hunde mit zu kurzem Riemen können die Riemenarbeit höchstens mit genügend bekommen.\n" +
            "Zulassung: Der Hund ist am Prüfungstag mindestens 24 Monate alt und hat bis zum Nennschluss Schussfestigkeit und Lautnachweis (spurlaut, sichtlaut, laut, fährtenlaut) erbracht - Waidlaut und Laut im Schwarzwildgatter zählen nicht. Der Führer führt nur einen Hund und legt seinen eigenen gültigen Jagdschein vor; der Eigentümer ist Mitglied eines JGHV-Vereins. Ein Hund darf höchstens zweimal geführt werden, eine bestandene Variante nicht wiederholen; die 40-Stunden-Fährte setzt eine bestandene 20-Stunden-Fährte in einem früheren Kalenderjahr voraus.\n" +
            "Fährte: im Wald, mindestens 1.000 m lang, drei nahezu rechtwinklige Haken, zwei Wundbetten, Abstand zwischen den Fährten mindestens 300 m, Schweißriemen mindestens 6 m; Prüfung nur vom 1. Mai bis 30. November.\n" +
            "Fassung: VSwPO/VFsPO, gültig vom 01.12.2026 bis 31.12.2036.";

        await SeedRegulationAsync(db, jagd, new RegulationSeed("JGHV VSwP 20 h", "2026", new DateOnly(2026, 12, 1),
            Schweiss("Die Fährte hat neben dem Anschuss und zwei Wundbetten vier Verweiserpunkte aus geronnenem Schweiß (je höchstens 2 ml); der Hund soll sie und Pirschzeichen verweisen."),
            Description: "JGHV-Verbandsschweißprüfung (VSwP), Fährte mit 20 Stunden Stehzeit - Prüfung der Nachsuche auf Schalenwild nach der Praxis. Die Fährte wird im Tropf- oder Tupfverfahren mit Schweiß einer Schalenwildart gelegt, insgesamt höchstens ein Viertelliter. Zum Verweisen liegen außer den zwei Wundbetten vier Verweiserpunkte (geronnener Schweiß, je höchstens 2 ml) auf der Fährte.\n" +
                "Stehzeit mindestens 20 Stunden; 20- und 40-Stunden-Fährten werden am selben Tag gelegt.\n" +
                schweissBewertung,
            SourceUrl: QuelleJghvVswpo));

        await SeedRegulationAsync(db, jagd, new RegulationSeed("JGHV VSwP 40 h", "2026", new DateOnly(2026, 12, 1),
            Schweiss("Die Fährte hat neben dem Anschuss und zwei Wundbetten vier Verweiserpunkte aus geronnenem Schweiß (je höchstens 2 ml); der Hund soll sie und Pirschzeichen verweisen."),
            Description: "JGHV-Verbandsschweißprüfung (VSwP), Fährte mit 40 Stunden Stehzeit - die anspruchsvollere Stufe. Sie setzt eine bestandene 20-Stunden-VSwP in einem früheren Kalenderjahr voraus. Tropf- oder Tupfverfahren, insgesamt höchstens ein Viertelliter Schweiß; zwei Wundbetten und vier Verweiserpunkte.\n" +
                "Stehzeit mindestens 40 Stunden.\n" +
                schweissBewertung,
            SourceUrl: QuelleJghvVswpo));

        await SeedRegulationAsync(db, jagd, new RegulationSeed("JGHV VFsP 20 h", "2026", new DateOnly(2026, 12, 1),
            Schweiss("Die Fährte hat neben dem Anschuss und zwei Wundbetten vier Tropfbetten; in die ersten ca. 50 m nach dem Anschuss wird abnehmend Schweiß getropft, danach ist die Fährte nahezu schweißfrei."),
            Description: "JGHV-Verbandsfährtenschuhprüfung (VFsP), Fährte mit 20 Stunden Stehzeit - die Nachsuche auf einer Fährte, die ausschließlich mit Fährtenschuhen aus Schalen von Schalenwild gelegt wird (kein Rehwild). Höchstens ein Zehntelliter Schweiß je Fährte; zwei Wundbetten und vier Tropfbetten.\n" +
                "Stehzeit mindestens 20 Stunden.\n" +
                schweissBewertung,
            SourceUrl: QuelleJghvVswpo));

        await SeedRegulationAsync(db, jagd, new RegulationSeed("JGHV VFsP 40 h", "2026", new DateOnly(2026, 12, 1),
            Schweiss("Die Fährte hat neben dem Anschuss und zwei Wundbetten vier Tropfbetten; in die ersten ca. 50 m nach dem Anschuss wird abnehmend Schweiß getropft, danach ist die Fährte nahezu schweißfrei."),
            Description: "JGHV-Verbandsfährtenschuhprüfung (VFsP), Fährte mit 40 Stunden Stehzeit - die anspruchsvollere Stufe. Sie setzt eine bestandene 20-Stunden-VFsP in einem früheren Kalenderjahr voraus. Fährtenschuhe aus Schalen von Schalenwild, höchstens ein Zehntelliter Schweiß je Fährte; zwei Wundbetten und vier Tropfbetten.\n" +
                "Stehzeit mindestens 40 Stunden.\n" +
                schweissBewertung,
            SourceUrl: QuelleJghvVswpo));
    }
}

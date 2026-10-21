namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Was im Katalog "international" ist: die Sportarten der FCI-Prüfungsordnung
/// "International Utility Dog Regulations (FCI-IGP)", gültig ab 01.01.2025.
///
/// Zwei Dinge hängen daran, deshalb steht es an einer Stelle:
/// <list type="bullet">
/// <item>Geltungsbereich: Laut Abschnitt "Validity" der Ordnung gilt sie für
/// alle FCI-Mitglieder - ihre Prüfungsordnungen tragen deshalb keinen
/// Länder-Code (<c>Regulation.CountryCode</c> null = gilt überall). Die
/// nationalen VDH-Ordnungen (Turnierhundsport, Agility, Hoopers) bleiben "DE".</item>
/// <item>Englische Namen: Englisch ist in dieser Ordnung die Originalsprache
/// und hat bei Abweichungen Vorrang. Die Bezeichnungen unten stammen aus dem
/// englischen Text (FCI-IGP 2025, approved 03.09.2024, effective 01.01.2025);
/// die Zuordnung zu den deutschen Katalogübungen erfolgte über Abschnitt,
/// Reihenfolge der Übungstabellen und Punkte, nicht über freie Übersetzung.
/// Quelle und Ausnahmen: docs/PO_VERIFIKATION.md.</item>
/// </list>
/// Nationale Ordnungen bekommen keinen englischen Namen: Sie gibt es nur auf
/// Deutsch.
/// </summary>
public static class FciKatalog
{
    /// <summary>
    /// Sportart-Code von Obedience (Paket O1): die FCI-Klassen 1 bis 3 und die
    /// nationalen Einsteigerklassen von VDH, ÖKV und SKG teilen sich eine Sportart.
    /// </summary>
    public const string ObedienceCode = "OBEDIENCE";

    /// <summary>
    /// Sportarten-Codes, die FCI-Ordnungen führen - alles, was nicht darin steht,
    /// ist national. Die Sportart gilt als international, auch wenn sie daneben
    /// nationale Ordnungen führt (die ÖKV-BH unter "BH", die nationalen
    /// Einsteigerklassen unter Obedience): Deren Land steht dann an der Ordnung
    /// selbst, und englische Namen und Texte gibt es nur, wo
    /// <see cref="Ordnungen"/> und <see cref="Uebungen"/> einen Eintrag haben.
    /// </summary>
    public static readonly IReadOnlySet<string> InternationaleSportarten = new HashSet<string>
    {
        "BH", "IBGH1", "IBGH2", "IBGH3", "IGP1", "IGP2", "IGP3",
        "FAERTE", "FPR", "UPR", "SPR", "GPR", "STOEPR", "IAD",
        ObedienceCode,
    };

    /// <summary>Länder-Code der nationalen (VDH-)Ordnungen.</summary>
    public const string Deutschland = "DE";

    /// <summary>
    /// Länder-Code der österreichischen (ÖKV-)Ordnungen (Paket L2). Sie liegen
    /// teils unter FCI-Sportarten (BH) und teils unter VDH-Sportarten
    /// (Agility, Hoopers) und tragen den Code ausdrücklich - die Zuordnung
    /// nach Sportart (<see cref="GeltungsbereichVon"/>) reicht dafür nicht.
    /// </summary>
    public const string Oesterreich = "AT";

    /// <summary>
    /// Länder-Code der schweizerischen (SKG-)Ordnungen (Paket L3): Begleithund
    /// als eigene Sportart und Agility unter der Sportart AGILITY. Wie bei
    /// Österreich tragen sie den Code ausdrücklich.
    /// </summary>
    public const string Schweiz = "CH";

    /// <summary>
    /// Geltungsbereich der Ordnungen einer Sportart: null = gilt überall (FCI),
    /// sonst Deutschland (VDH).
    /// </summary>
    public static string? GeltungsbereichVon(string sportCode) =>
        InternationaleSportarten.Contains(sportCode) ? null : Deutschland;

    /// <summary>Englische Namen der Sportarten, nach Sportart-Code.</summary>
    public static readonly IReadOnlyDictionary<string, string> Sportarten = new Dictionary<string, string>
    {
        ["BH"] = "Companion Dog Trial (FCI-BH/VT)",
        ["IBGH1"] = "International Companion Dog Test 1 (FCI-IBGH 1)",
        ["IBGH2"] = "International Companion Dog Test 2 (FCI-IBGH 2)",
        ["IBGH3"] = "International Companion Dog Test 3 (FCI-IBGH 3)",
        ["FAERTE"] = "FCI-IGP Tracking Dog Examinations (IFH / IGP-FH)",
        ["IGP1"] = "International Utility Dog Examination 1 (FCI-IGP 1)",
        ["IGP2"] = "International Utility Dog Examination 2 (FCI-IGP 2)",
        ["IGP3"] = "International Utility Dog Examination 3 (FCI-IGP 3)",
        ["FPR"] = "Tracking (FCI-FPr)",
        ["UPR"] = "Obedience (FCI-UPr)",
        ["SPR"] = "Protection (FCI-SPr)",
        ["GPR"] = "Working Dog Test (FCI-GPr)",
        ["STOEPR"] = "Article Search (FCI-StöPr)",
        ["IAD"] = "Endurance Trial (FCI-IAD)",
        [ObedienceCode] = "Obedience",
    };

    /// <summary>
    /// Englische Namen der Prüfungsordnungen, nach (Sportart-Code, deutscher Name).
    /// Form: das Kürzel, wie es die Ordnung trägt, und der englische Titel -
    /// kurz genug für 375 px, und wer das Kürzel kennt, findet es wieder.
    /// </summary>
    public static readonly IReadOnlyDictionary<(string Sport, string Name), string> Ordnungen =
        new Dictionary<(string, string), string>
        {
            [("BH", "BH")] = "FCI-BH/VT – Companion Dog Trial",
            [("IBGH1", "IBGH1")] = "FCI-IBGH 1 – International Companion Dog Test",
            [("IBGH2", "IBGH2")] = "FCI-IBGH 2 – International Companion Dog Test",
            [("IBGH3", "IBGH3")] = "FCI-IBGH 3 – International Companion Dog Test",
            [("FAERTE", "FCI-IFH 1")] = "FCI-IFH 1 – Tracking Dog Examination",
            [("FAERTE", "FCI-IFH 2")] = "FCI-IFH 2 – Tracking Dog Examination",
            [("FAERTE", "FCI-IFH 3")] = "FCI-IFH 3 – Tracking Dog Examination",
            [("FAERTE", "FCI-IGP FH")] = "FCI-IGP FH – Tracking Dog Examination",
            [("IGP1", "FCI-IGP 1")] = "FCI-IGP 1 – International Utility Dog Examination",
            [("IGP2", "FCI-IGP 2")] = "FCI-IGP 2 – International Utility Dog Examination",
            [("IGP3", "FCI-IGP 3")] = "FCI-IGP 3 – International Utility Dog Examination",
            [("FPR", "FCI-FPr 1")] = "FCI-FPr 1 – Tracking",
            [("FPR", "FCI-FPr 2")] = "FCI-FPr 2 – Tracking",
            [("FPR", "FCI-FPr 3")] = "FCI-FPr 3 – Tracking",
            [("UPR", "FCI-UPr 1")] = "FCI-UPr 1 – Obedience",
            [("UPR", "FCI-UPr 2")] = "FCI-UPr 2 – Obedience",
            [("UPR", "FCI-UPr 3")] = "FCI-UPr 3 – Obedience",
            [("SPR", "FCI-SPr 1")] = "FCI-SPr 1 – Protection",
            [("SPR", "FCI-SPr 2")] = "FCI-SPr 2 – Protection",
            [("SPR", "FCI-SPr 3")] = "FCI-SPr 3 – Protection",
            [("GPR", "FCI-GPr 1")] = "FCI-GPr 1 – Working Dog Test",
            [("GPR", "FCI-GPr 2")] = "FCI-GPr 2 – Working Dog Test",
            [("GPR", "FCI-GPr 3")] = "FCI-GPr 3 – Working Dog Test",
            [("STOEPR", "FCI-StöPr 1")] = "FCI-StöPr 1 – Article Search",
            [("STOEPR", "FCI-StöPr 2")] = "FCI-StöPr 2 – Article Search",
            [("STOEPR", "FCI-StöPr 3")] = "FCI-StöPr 3 – Article Search",
            [("IAD", "FCI-IAD")] = "FCI-IAD – Endurance Trial",
            // Obedience (Paket O1): der englische Titel der Ordnung heißt "Obedience Trials for Classes 1, 2 and 3".
            [(ObedienceCode, "FCI-Obedience 1")] = "FCI Obedience Class 1",
            [(ObedienceCode, "FCI-Obedience 2")] = "FCI Obedience Class 2",
            [(ObedienceCode, "FCI-Obedience 3")] = "FCI Obedience Class 3",
        };

    /// <summary>
    /// Die Übungen der Abschnitte A, B und C der FCI-IGP. IGP 1-3 und die
    /// Einzelprüfungen FPr, UPr, SPr und GPr bestehen aus denselben Übungen
    /// ("Die Prüfungen bestehen nur aus Abteilung A/B/C der IGP"), tragen also
    /// auch dieselben englischen Namen.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Gebrauchshundeuebungen = new Dictionary<string, string>
    {
        // Phase A - Tracking
        ["Fährtenarbeit (Eigenfährte)"] = "Tracking (own track)",
        ["Fährtenarbeit (Fremdfährte)"] = "Tracking (strange track)",
        ["Gegenstände verweisen"] = "Indicating of the articles",
        // Phase B - Obedience
        ["Freifolge"] = "Free heeling",
        ["Sitz aus der Bewegung"] = "Sit in motion",
        ["Ablegen in Verbindung mit Herankommen"] = "Down out of motion with recall",
        ["Steh aus der Bewegung"] = "Stand out of motion",
        ["Bringen auf ebener Erde"] = "Retrieve on the flat",
        ["Freisprünge / Bringen über eine 1 Meter hohe Hürde"] = "Retrieve / jump over the 1-meter hurdle",
        ["Klettersprung / Bringen über die Schrägwand"] = "Retrieve / climb-jump over the scaling wall",
        ["Voraussenden mit Hinlegen"] = "Send out with down",
        ["Ablegen unter Ablenkung"] = "Down under distraction",
        // Phase C - Protection
        ["Revieren"] = "Search for the helper",
        ["Stellen und Verbellen"] = "Hold and Bark",
        ["Verhinderung eines Fluchtversuches"] = "Prevention of attempted escape",
        ["Abwehr eines Angriffs aus der Bewachungsphase (nach Fluchtversuch)"] = "Defense against an attack from the guarding phase (after attempted escape)",
        ["Rückentransport"] = "Back transport",
        ["Überfall auf den Hund aus dem Rückentransport"] = "Attack on the dog out of the back transport",
        ["Angriff auf den Hund aus der Bewegung"] = "Attack on the dog out of motion",
        ["Abwehr eines Angriffs aus der Bewachungsphase (Schlussphase)"] = "Defense against an attack from the guarding phase (with completion of phase C)",
    };

    /// <summary>
    /// Die Übungen der FCI-Klassen 1 bis 3 im Obedience (FCI-Obedience 2025, Tabelle S. 14 f.).
    /// Englisch ist die Originalsprache; die Namen stammen aus der Übungstabelle bzw. - wo eine
    /// Übung in mehreren Klassen vorkommt - aus den Überschriften der Kapitel ("Positions under
    /// march"). Dieselben Übungen führen die nationalen Einsteigerklassen; die drei Übungen,
    /// die keine FCI-Klasse kennt, haben keinen englischen Namen.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Obedienceuebungen = new Dictionary<string, string>
    {
        ["Sitzen in der Gruppe"] = "Sitting in a group",
        ["Liegen in der Gruppe"] = "Lying in a group",
        ["Liegen in der Gruppe mit Abrufen"] = "Lying in a group for 1 minute, and recall",
        ["Freifolge"] = "Heelwork",
        ["Positionen aus der Bewegung"] = "Positions under march",
        ["Abrufen"] = "Recall",
        ["Abrufen mit Steh"] = "Recall with stand",
        ["Abrufen mit Steh/Sitz/Platz"] = "Recall with stand / sit / down",
        ["In ein Quadrat schicken mit Platz"] = "Send away into a square and lying down",
        ["In ein Quadrat schicken mit Platz und Abrufen"] = "Send away to a square, down and recall",
        ["In ein Quadrat schicken mit Richtungsanweisung, Platz und Abrufen"] = "Send away with directing, down and recall",
        ["Distanzkontrolle"] = "Distance control",
        ["Apportieren und Sprung über eine Hürde"] = "Retrieve and jumping over a hurdle",
        ["Apportieren mit Richtungsanweisung"] = "Directed retrieve",
        ["Geruchsidentifizierung"] = "Scent discrimination and retrieve",
        ["Um eine Pylonengruppe/Tonne herumschicken"] = "Send round a group of cones/barrel and return",
        ["Um eine Pylonengruppe/Tonne herumschicken, Steh/Platz und springen"] = "Send round a group of cones/barrel, stop and jump",
        ["Um eine Pylonengruppe/Tonne herumschicken, Steh/Sitz/Platz, apportieren und springen"] = "Send round a group of cones, stop (stand/sit/down), retrieve and jump",
        ["Gesamteindruck"] = "General impression",
    };

    private static readonly string[] Gebrauchshundesportarten = ["IGP1", "IGP2", "IGP3", "FPR", "UPR", "SPR", "GPR"];

    /// <summary>
    /// Englische Namen der Übungen, nach (Sportart-Code, deutscher Name).
    /// Übungen ohne Eintrag (vereinseigene, Freitext, Altbestand außerhalb der
    /// Ordnung) behalten ihren deutschen Namen.
    /// </summary>
    public static readonly IReadOnlyDictionary<(string Sport, string Name), string> Uebungen = BaueUebungen();

    private static Dictionary<(string, string), string> BaueUebungen()
    {
        var alle = new Dictionary<(string, string), string>();

        foreach (var sport in Gebrauchshundesportarten)
            foreach (var (name, en) in Gebrauchshundeuebungen)
                alle[(sport, name)] = en;

        foreach (var (name, en) in Obedienceuebungen)
            alle[(ObedienceCode, name)] = en;

        // Begleithundeprüfung mit Verhaltens- und Verkehrsteil (FCI-BH/VT).
        // Die Verkehrsteile heißen im englischen Text "Examination in traffic".
        alle[("BH", "Fußarbeit")] = "Heeling on leash";
        alle[("BH", "Freifolge")] = "Free heeling";
        alle[("BH", "Sitzübung")] = "Sit exercise";
        alle[("BH", "Ablegen in Verbindung mit Herankommen")] = "Down with recall";
        alle[("BH", "Ablegen unter Ablenkung")] = "Down under distraction";
        alle[("BH", "Verhalten im Verkehr")] = "Encounter with cars and cyclists";
        alle[("BH", "Begegnung mit Personengruppe")] = "Encounter with group of persons";
        alle[("BH", "Verhalten gegenüber anderen Hunden")] = "Encounter with other dogs";
        alle[("BH", "Zurücklassen des Hundes")] = "Behaviour of the dog leashed and left alone in traffic";
        // Zwei Übungen aus einer älteren Fassung des Katalogs ("Sitzübung" bzw. "Ablegen in
        // Verbindung mit Herankommen" heißen sie heute): Die Ordnung führt sie nicht mehr, wo
        // Einträge daran hängen, bleiben sie aber auswählbar. Englisch nach der Terminologie der
        // FCI-IGP 2025. "Down with recall" ist in der BH schon vergeben - zwei Übungen mit
        // gleichem Namen wären in der Auswahl nicht zu unterscheiden -, deshalb hier die
        // ausführliche Fassung, wie sie die IGP für dieselbe Übung nennt. Die ÖKV-eigenen
        // Übungen bleiben bewusst ohne englischen Namen (national).
        alle[("BH", "Sitz aus der Bewegung")] = "Sit in motion";
        alle[("BH", "Ablegen mit Abrufen")] = "Down out of motion with recall";

        // Internationale Begleithundeprüfungen: die Übungen stehen in der
        // Verteilungstabelle "Distribution and values of the exercises".
        foreach (var sport in new[] { "IBGH1", "IBGH2", "IBGH3" })
        {
            alle[(sport, "Fußarbeit")] = "Heeling on leash";
            alle[(sport, "Freifolge")] = "Free heeling";
            alle[(sport, "Absitzen aus der Bewegung")] = "Sit in motion";
            alle[(sport, "Ablegen aus der Bewegung")] = "Down out of motion with recall";
            alle[(sport, "Bringen auf ebener Erde")] = "Retrieve on the flat";
            alle[(sport, "Voraussenden mit Hinlegen")] = "Send out with down";
            alle[(sport, "Ablegen unter Ablenkung")] = "Down under distraction";
        }
        alle[("IBGH3", "Steh aus dem Schritt")] = "Stand out of motion with recall";
        alle[("IBGH3", "Bringen über die Schrägwand")] = "Retrieve over the scaling wall (140 cm)";

        // Fährtenhund-Prüfungen (IFH, IGP-FH).
        alle[("FAERTE", "Fährtenarbeit")] = "Tracking";
        alle[("FAERTE", "Gegenstände verweisen")] = "Indicating of the articles";
        // Trainingsübungen ohne Punkte in der Ordnung; die Bezeichnungen sind
        // dem Wortlaut des Textes entlehnt ("Corners", "Own track",
        // "Strange track", "start of the track"), keine eigene Übung dort.
        alle[("FAERTE", "Fährtenaufnahme")] = "Starting the track";
        alle[("FAERTE", "Winkelarbeit")] = "Working the corners";
        alle[("FAERTE", "Eigenfährte vertiefen")] = "Working an own track";
        alle[("FAERTE", "Fremde Fährte folgen")] = "Working a strange track";

        // FCI-Stöberprüfung: Die fünf Bewertungsmerkmale mit ihren Punkten.
        alle[("STOEPR", "Führigkeit (Hör- und Sichtzeichen)")] = "Tenacity (following instructions and hand signals)";
        alle[("STOEPR", "Arbeitsintensität")] = "Intensity of work (determination and work ethic)";
        alle[("STOEPR", "Ausdauer der Sucharbeit")] = "Stamina (never stopping the search drive)";
        alle[("STOEPR", "Sucheinteilung und Lenken (Hundeführer)")] = "Behaviour of the handler (affecting the dog positively)";
        alle[("STOEPR", "Finden und Anzeigen der Gegenstände")] = "Finding the articles (convincing, confident, steady indication)";

        // Ausdauerprüfung: "Running exercise" mit den Pausen, in denen der
        // Richter den Hund auf Ermüdung und Pfoten prüft.
        alle[("IAD", "Laufübung am Fahrrad")] = "Running exercise (beside the bike)";
        alle[("IAD", "Verfassungsprüfung in den Pausen")] = "Condition check during the breaks";

        return alle;
    }
}

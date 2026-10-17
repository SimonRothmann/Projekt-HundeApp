using Dogity.Domain.Sports;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Die schweizerischen Prüfungsordnungen der SKG (Paket L3, 2026-10-17):
/// Begleithund 1 bis 3 (NPO-BH) und Agility mit den Arbeitsklassen 1 bis 3.
///
/// Eigene Datei wie bei Österreich, damit der Hauptseeder nicht weiter wächst.
/// Alles hier trägt <c>CountryCode</c> "CH" - auch unter der Sportart Agility,
/// die es zusätzlich mit VDH- und ÖKV-Ordnungen gibt.
///
/// Quellen (siehe docs/PO_VERIFIKATION.md, Abschnitt "Schweiz"). Jede Ordnung
/// und Übung nennt die Seite des Dokuments, in Klammern wie im PDF gedruckt
/// ("S. 14"). Die Texte sind eigene Beschreibungen, keine Kopie der Ordnungen.
///
/// Unklares ist nicht ergänzt, sondern in den Beschreibungen so benannt, wie es
/// im Dokument steht - etwa dass die Aufstiegskriterien im Agility-Reglement
/// gar nicht stehen, sondern jährlich per Weisung der TKAMO festgelegt werden.
/// </summary>
public static partial class SportCatalogSeeder
{
    /// <summary>Geltungsbereich der SKG-Ordnungen (ISO 3166-1 alpha-2).</summary>
    private const string Schweiz = FciKatalog.Schweiz;

    // Die offizielle TKGS-Seite (tkgs.ch/de/reglemente) nennt die 3. Ausgabe der
    // NPO-BH, verlinkt das deutsche PDF aber nicht. Der Text stammt aus dem
    // Dokumentenbereich einer SKG-Sektion - die Adresse führt zum Dokument selbst.
    private const string QuelleNpoBh = "https://scog-schaffhausen.ch/documents/bh_npo_15_d_ausgabe_3-1/";
    private const string QuelleAgilityCh = "https://www.tkamo.ch/media/files/Agility_Reglement_2025_DE.pdf";

    /// <summary>Sportart-Code des schweizerischen Begleithunds (NPO-BH der SKG).</summary>
    public const string BegleithundSkgCode = "BHCH";

    private static async Task SeedSchweizAsync(ApplicationDbContext db, Sport agility)
    {
        await SeedSkgBegleithundAsync(db);
        await SeedSkgAgilityAsync(db, agility);
    }

    // -------------------------------------------------------------------
    // Begleithund 1-3 (NPO-BH, 3. Ausgabe, gültig ab 1. März 2022)
    //
    // Eine NEUE Sportart statt Ordnungen unter der BH: Aufbau und Übungen
    // sind grundlegend anders als in der deutschen/FCI-BH (drei Abteilungen
    // Fährte bzw. Revier, Unterordnung, Führigkeit zu je 100 Punkten, in BH 2
    // und 3 mit Fremdfährte). Die Übungen sind deshalb eigene dieser Sportart
    // und mischen sich nicht mit denen der BH/IGP.
    //
    // Das Deckblatt trägt noch durchgestrichene Angaben der Vorausgabe
    // ("32. Ausgabe", "1. Januar 2019"); maßgeblich ist die 3. Ausgabe ab
    // 1.3.2022. Weitere Änderungsmarkierungen betreffen nur die Bezeichnung
    // der Mitgliedskarte, keine Zahlenwerte.
    //
    // Der Name folgt dem Inhaltsverzeichnis ("Prüfungsklassen mit
    // Prüfungsfächern", S. 4-6): Wo die Tabelle und die Überschrift im
    // Kapitel leicht abweichen ("Steh" und "Stehen"), gilt die Tabelle.
    // -------------------------------------------------------------------
    private const string BhBewertung =
        "Bewertung (S. 7): Vorzüglich 100-96 %, Sehr gut 95-90 %, Gut 89-80 %, Befriedigend 79-70 %, Mangelhaft 69-0 %. Je Abteilung (100 Punkte) V 100-96, SG 95-90, G 89-80, B 79-70, M 69-0; im Gesamt (300 Punkte) V 300-286, SG 285-270, G 269-240, B 239-210, M 209-0.\n" +
        "Bestanden ist eine Abteilung mit mindestens 70 Punkten; die Prüfung nur, wenn Nasenarbeit (A), Unterordnung (B) und Führigkeit (C) je mindestens 70 Punkte erreichen - im Minimum 210 von 300 Punkten (S. 7).\n" +
        "Ein Zusatzhörzeichen begrenzt die Übung auf „Gut“, zwei auf „Befriedigend“; Führerhilfen kosten 5 bis 20 % des Übungswertes (S. 9). Aggressive oder stark ängstliche Hunde werden nicht zugelassen bzw. disqualifiziert (S. 10).";

    private static async Task SeedSkgBegleithundAsync(ApplicationDbContext db)
    {
        var bh = await SeedSportAsync(db, BegleithundSkgCode, "Begleithund (SKG)",
        [
            // ---- Abteilung A: Nasenarbeit ----
            new("Halten der Fährte", ExerciseDifficulty.Intermediate, "Fährte",
                "Abteilung A 1 (Fährte). Der Hund folgt der Fährte intensiv, sicher und mit tiefer Nase, nimmt die Winkel sicher an oder arbeitet sie auf engem Raum aus, ohne die Fährte zu verlassen; Absichern auf der Fährte ist nicht fehlerhaft. Der Hundeführer lässt die 6 oder 10 m lange Suchleine bis zum Ende ablaufen und folgt dann dem Hund; Zurückhalten und Hilfen sind nicht statthaft. Ein Hörzeichen für den Ansatz: ein erstes Zusatzhörzeichen begrenzt die Wertung auf „Gut“, ein zweites auf „Befriedigend“. Hält der Hundeführer den Hund zurück, gibt es eine einmalige Aufforderung zum Nachgehen (-5 Punkte); verlässt der Hund die Fährte um mehr als eine Leinenlänge, wird abgebrochen und teilbewertet (S. 15)."),
            new("Gegenstände auf der Fährte", ExerciseDifficulty.Intermediate, "Fährte",
                "Der Hund zeigt die auf der Fährte liegenden weichen Gegenstände (8-10 cm lang, 2-3 cm dick) sicher an: Herbeibringen, Aufnehmen oder Verweisen. Die Art meldet der Hundeführer beim Anmelden, ein Wechsel innerhalb der Arbeit ist nicht zulässig. Gefundene Gegenstände zeigt der Hundeführer dem Leistungsrichter durch Hochheben an. Ein überlaufener Gegenstand, einer mit Führerhilfe oder einer, der nicht wie gemeldet angezeigt wird, zählt 0 Punkte."),
            new("Revierarbeit", ExerciseDifficulty.Advanced, "Revier",
                "Abteilung A 2 (Revier). Der Hund arbeitet ein abgestecktes Revier in systematischen Seitenschlägen ab, mit intensiver Quersuche und grosser Führigkeit, und folgt dabei keiner Fährte. In den Querschlägen arbeitet er nicht mehr als 6 m vor dem Hundeführer, Durchgehen hinter dem Hundeführer ist fehlerhaft. Der Hundeführer bewegt sich auf der Mittellinie, seitlich sind ca. 3 m erlaubt. Hörzeichen mit kurzem Sichtzeichen für Ansetzen, Richtungsänderung (auch Doppelpfiff) und Vorbeigehen. Ausarbeitungszeit 10 Minuten."),
            new("Gegenstände im Revier", ExerciseDifficulty.Intermediate, "Revier",
                "Drei weiche Gegenstände unterschiedlicher Form, die der Leistungsrichter unbeobachtet auswirft. Der Hund zeigt sie durch Herbeibringen, Aufnehmen oder Verweisen an, die Art wird beim Anmelden gemeldet. Ein überlaufener Gegenstand, einer mit Führerhilfe oder einer, der nicht wie gemeldet angezeigt wird, zählt 0 Punkte; ein Fehlverweis kostet 2 Punkte. Einmaliges Loben am Gegenstand ist erlaubt."),

            // ---- Abteilung B: Unterordnung ----
            new("Leinenführigkeit", ExerciseDifficulty.Beginner, "Unterordnung",
                "Nur in BH 1. Der angeleinte Hund geht gerade und dicht neben dem Hundeführer, die Schulter auf Höhe des Knies, aufmerksam und mit hoher Arbeitsbereitschaft; Wendungen eng, im Sitz stehen die Vorderbeine senkrecht. Ein Hörzeichen mit kurzem Sichtzeichen je Angehen, Wendung und Grundstellung. Zwischen den Wendungen mindestens 10-15 Schritte."),
            new("Freifolge", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Der freifolgende Hund geht gerade und dicht neben dem Hundeführer, Schulter auf Höhe des Knies, mit enger, aufmerksamer Wendung. Das Laufschema beginnt und endet in einer korrekten Grundstellung, die Linkskehrtwendung wird immer gleich gezeigt, zwischen den Wendungen sind mindestens 10-15 Schritte zu gehen. Von BH 1 bis BH 3 kommen Gangartwechsel (Laufschritt, Langsamschritt) und Stellungen im Laufschema hinzu."),
            new("Stellungen in der Fussposition", ExerciseDifficulty.Beginner, "Unterordnung",
                "Aus der Grundstellung zeigt der Hund neben dem Hundeführer die Stellungen in der festgelegten Reihenfolge je einmal und endet im Sitz in der Grundstellung: gerades Platz ohne seitliches Kippen, gerades Sitz mit senkrechten Vorderbeinen, in BH 3 zusätzlich Steh, bei dem der Hund ganz durchsteht."),
            new("Stellungen in der Frontposition", ExerciseDifficulty.Intermediate, "Unterordnung",
                "BH 2 und BH 3. Der Hund wechselt aus der Grundstellung in die Frontposition vor dem Hundeführer, zeigt dort Platz und Sitz je einmal und wechselt wieder in die Grundstellung (direkt oder um den Hundeführer herum). Dichtes, gerades, zentrales Vorsitzen; Grätschstellung nach rechts ist erlaubt, wenn sie sofort beim Hörzeichen eingenommen wird."),
            new("Apportieren", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Der Hundeführer wirft seinen eigenen Apportiergegenstand mindestens 6 Schritte geradeaus und wartet, bis er ruht. Der Hund holt ihn auf Hörzeichen, trägt ihn sicher, setzt sich gerade und dicht davor und gibt ihn auf Kommando ab; Abschluss in der Grundstellung ohne Grätschstellung des Hundeführers. Der Gegenstand darf kein Futter enthalten. Nimmt der Hund ihn nach dem zweiten Zusatzhörzeichen nicht auf, zählt die Übung 0 Punkte; einmaliges Nachfassen im Arbeitseifer bleibt ohne Abzug."),
            new("Hochsprung", ExerciseDifficulty.Intermediate, "Unterordnung",
                "Die Hürde (geschlossene Fläche, 120-150 cm breit) ist 50 cm hoch, auf Wunsch 20 cm; die Wahl meldet der Hundeführer vor der Abteilung. Der Hund springt sicher auf Hörzeichen, ohne die Hürde zu berühren, und schliesst schnell wieder an. BH 1: Sprung im Vorbeigehen des Hundeführers; BH 2: hin und zurück; BH 3: hin, Wartestellung im Steh, zurück. Springt der Hund nicht oder fällt die Hürde: Wiederholung möglich, beim zweiten Mal 0 Punkte."),

            // ---- Abteilung C: Führigkeit ----
            new("Durchgehen einer Personengruppe", ExerciseDifficulty.Beginner, "Führigkeit",
                "Nur in BH 1; in der Ordnung „Durchgehen einer Personengruppe / Leinenführigkeit“. Der angeleinte Hund durchquert zweimal eine ruhig stehende Gruppe aus vier Personen, sicher und unbefangen, ohne Ablenkung und ohne jemanden zu belästigen, und endet in der Grundstellung, in der er abgeleint wird. Aggressives Verhalten in der Gruppe führt zum Abbruch der Prüfung."),
            new("Abruf unter Ablenkung", ExerciseDifficulty.Intermediate, "Führigkeit",
                "Nur in BH 1. Der Hund wartet im Sitz, der Hundeführer durchgeht die Personengruppe und ruft ihn aus 10-15 Schritten Entfernung nach der Gruppe in die Grundstellung ab. Der Hund passiert die Gruppe zügig auf kürzestem Weg oder eng daran vorbei, ohne sich ablenken zu lassen. Weicht er der Gruppe aus, ist die Übung „Mangelhaft“; Aggression führt zum Abbruch der Prüfung."),
            new("Sitz aus der Bewegung", ExerciseDifficulty.Intermediate, "Führigkeit",
                "Der freifolgende Hund setzt sich auf ein Hörzeichen aus der Bewegung sofort gerade hin und bleibt aufmerksam sitzen, während der Hundeführer ohne Umsehen und ohne weiteres Hörzeichen weitergeht. Entfernung und Punkte wachsen mit der Klasse: BH 1 mindestens 10 Schritte (20 Punkte), BH 2 mindestens 20 Schritte (10 Punkte), BH 3 mindestens 30 Schritte (10 Punkte)."),
            new("Stellungen in Frontposition", ExerciseDifficulty.Intermediate, "Führigkeit",
                "Nur in BH 1. Der Hundeführer tritt vor den wartenden Hund in die Frontstellung; der Hund zeigt dort Platz und Sitz in dieser Reihenfolge je einmal und wechselt dann in die Grundstellung. Er bleibt beim Wechsel des Hundeführers ruhig. Grätschstellung nach rechts ist erlaubt; wird die Reihenfolge nicht eingehalten, kostet das Befriedigend -6 Punkte."),
            new("Abrufen über die Hürde", ExerciseDifficulty.Intermediate, "Führigkeit",
                "Der Hund wartet 10 Schritte vor der Hürde, der Hundeführer geht ohne ihn darüber oder eng vorbei, stellt sich 10 Schritte danach mit der Front zum Hund auf und ruft ihn nach 3 Sekunden über die Hürde in die Grundstellung. Sicherer Sprung, schneller Zulauf; wird die Hürde ausgelassen, ist das Mangelhaft -10 Punkte. In BH 2 wartet der Hund in Sitz, Platz oder Steh nach Wahl."),
            new("Abrufen in Frontposition", ExerciseDifficulty.Intermediate, "Führigkeit",
                "Nur in BH 2. Der Hundeführer geht ohne Hund 20 Schritte, bleibt mit dem Rücken zum Hund stehen und ruft ihn nach 3 Sekunden in die Frontposition. Der Hund läuft schnell und direkt, sitzt gerade und dicht vor und wechselt verzugslos in die Grundstellung."),
            new("Stellungen auf Distanz", ExerciseDifficulty.Advanced, "Führigkeit",
                "BH 2 und BH 3. Direkt nach Sitz aus der Bewegung führt der Hund aus dem Sitz auf Distanz Stellungen in festgelegter Reihenfolge aus - BH 2: Platz, Sitz (Hundeführer 20 Schritte entfernt); BH 3: Steh, Platz, Sitz (30 Schritte) - und wird danach direkt in die Grundstellung abgerufen. Wird die Reihenfolge nicht eingehalten, kostet das Befriedigend -6 Punkte."),
            new("Steh aus dem Normalschritt", ExerciseDifficulty.Advanced, "Führigkeit",
                "Nur in BH 2. Der freifolgende Hund bleibt auf Hörzeichen aus dem Normalschritt sofort und gerade stehen, während der Hundeführer ohne Änderung der Gangart mindestens 20 Schritte weitergeht. Dann wird er direkt in die Grundstellung abgerufen. Eine falsche Stellung kostet Mangelhaft -5 Punkte."),
            new("Voran mit Stellung", ExerciseDifficulty.Advanced, "Führigkeit",
                "Nur in BH 2. Der Hund läuft aus der Grundstellung mindestens 20 Schritte voran, seitlich an einer Hürde vorbei, und nimmt dort auf Hörzeichen sofort Sitz, Platz oder Steh nach Wahl des Hundeführers ein. Der Hundeführer geht dann zum Hund. Gerades, zielstrebiges Vorangehen; Abbruch bei schrägem Vorangehen über 45 Grad."),
            new("Apportieren aus der Bewegung", ExerciseDifficulty.Advanced, "Führigkeit",
                "Nur in BH 3. Der Hundeführer lässt seinen Apportiergegenstand aus dem Gehen fallen, geht ohne Änderung der Gangart weiter und gibt zwischen dem 10. und 15. Schritt das Hörzeichen zum Apportieren; der Hund holt den Gegenstand und bringt ihn in den Vorsitz zum stehengebliebenen Hundeführer. Löst er sich nach dem zweiten Zusatzhörzeichen nicht oder nimmt er ihn nicht auf: 0 Punkte."),
            new("Steh aus dem Laufschritt", ExerciseDifficulty.Advanced, "Führigkeit",
                "Nur in BH 3. Der freifolgende Hund bleibt auf Hörzeichen aus dem Laufschritt sofort und gerade stehen, während der Hundeführer ohne Änderung der Gangart mindestens 30 Schritte weiterläuft. Dann wird er direkt in die Grundstellung abgerufen. Eine falsche Stellung kostet Mangelhaft -5 Punkte."),
            new("Voran mit Steh und Weitersenden", ExerciseDifficulty.Advanced, "Führigkeit",
                "Nur in BH 3. Der Hund läuft mindestens 20 Schritte voran, nimmt auf Hörzeichen Steh ein und läuft auf ein weiteres Kommando nochmals mindestens 20 Schritte voran; dann wird er aus der Bewegung in die Grundstellung abgerufen. Abbruch bei Rückkehr vor dem ersten Steh oder schrägem Vorangehen über 45 Grad."),
            new("Voran mit Verschieben", ExerciseDifficulty.Advanced, "Führigkeit",
                "Nur in BH 3. Der Hund läuft mindestens 40 Schritte voran, seitlich an einer Hürde vorbei, nimmt Steh ein, wird seitlich bis auf Höhe der Hürde verschoben und steht dort erneut. Der Hundeführer darf sich gleichzeitig, vor oder nach dem Hund verschieben. Danach Abruf direkt aus dem Steh, auf dem Rückweg überspringt der Hund die Hürde."),
        ]);

        var gueltig = new DateOnly(2022, 3, 1);

        // ---------------------------------------------------------------
        // BH 1: Eigenfährte, Unterordnung mit Leine, Führigkeit mit Gruppe
        // ---------------------------------------------------------------
        await SeedRegulationAsync(db, bh, new RegulationSeed("SKG BH 1", "2022", gueltig,
        [
            new("Halten der Fährte", true, 80, "Abteilung A (Fährte), S. 21 f. Eigenfährte, vom Hundeführer in Abwesenheit des Hundes gelegt: etwa 150 Schritte mit einem 90-Grad-Winkel, Liegezeit mindestens 20 Minuten, Ausarbeitungszeit 15 Minuten, Suchleine 6 oder 10 m. Die Abgangsmarkierung stellt der Hundeführer selbst (S. 13). Hält der Hundeführer den Hund zurück: einmalige Aufforderung zum Nachgehen, -5 Punkte; mehr als eine Leinenlänge neben der Fährte: Abbruch mit Teilbewertung (S. 15); wird das Fährtenende nicht in der Zeit erreicht, gibt es eine Teilbewertung, höchstens Mangelhaft. Nimmt der Hund die Fährte nach dem zweiten Zusatzhörzeichen nicht auf: Abteilung 0 Punkte (S. 22). Allgemeine Fährtenbestimmungen S. 13-16."),
            new("Gegenstände auf der Fährte", true, 20, "S. 21 f. Ein einziger Gegenstand, der Eigengegenstand des Hundeführers, am Fährtenende und mindestens 20 Schritte nach dem Winkel. Der Hundeführer weist ihn dem Leistungsrichter vor (S. 21). Überlaufen, Anzeigen mit Führerhilfe oder nicht wie gemeldet: 0 Punkte auf den Gegenstand (S. 22)."),
            new("Leinenführigkeit", true, 20, "S. 23 f. Normalschritt mit je einer Rechts- und Linkswendung und einer Kehrtwendung rechts. Am Ende wird der Hund in der Grundstellung abgeleint, danach beginnt die Freifolge. Die Leine wird am Ende gehalten, Aufwickeln ist nicht zulässig (S. 11). Ein Hörzeichen mit kurzem Sichtzeichen je Angehen, Wendung und Grundstellung."),
            new("Freifolge", true, 20, "S. 23 f. Normalschritt mit je einer Rechts- und Linkswendung und einer Kehrtwendung links; das Schema kann nach links oder rechts gezeigt werden, zwischen den Wendungen mindestens 10-15 Schritte. Ein Hörzeichen mit kurzem Sichtzeichen je Angehen, Wendung und Grundstellung."),
            new("Stellungen in der Fussposition", true, 20, "S. 24. Aus der Grundstellung Platz und Sitz in dieser Reihenfolge je einmal; Schluss mit Sitz in der Grundstellung. Ein Hörzeichen mit kurzem Sichtzeichen je Stellung."),
            new("Apportieren", true, 20, "S. 24 f. Eigener Apportiergegenstand, mindestens 6 Schritte geworfen; der Hund holt ihn auf Hörzeichen, setzt sich vor und gibt auf Kommando ab; Schluss in der Grundstellung, direkt oder um den Hundeführer herum. Der Gegenstand gehört dem Hundeführer und enthält kein Futter (S. 19)."),
            new("Hochsprung", true, 20, "S. 25 f. Hürde 50 cm, auf Wunsch 20 cm (vor der Abteilung melden). Der Hundeführer geht mit dem freifolgenden Hund mindestens 5 Schritte auf die Hürde zu, gibt die Freigabe und läuft im Moment des Sprungs eng an der Hürde vorbei; danach nimmt er den Hund aus der Bewegung mit und geht noch mindestens 5 Schritte. Kein Sprung oder umgefallene Hürde: Wiederholung möglich, Mangelhaft -10 Punkte; beim zweiten Mal 0 Punkte."),
            new("Durchgehen einer Personengruppe", true, 20, "S. 27. Der angeleinte Hund durchquert eine Gruppe aus vier Personen (im Viereck, etwa 1 m Abstand, S. 20), die etwa 10 Schritte entfernt steht, geht 10-15 Schritte weiter, Kehrtwendung, durchquert die Gruppe ein zweites Mal und hält nahe dem Start in der Grundstellung an; dort wird abgeleint. Aggression in der Gruppe: Abbruch der Prüfung."),
            new("Abruf unter Ablenkung", true, 20, "S. 28. Hörzeichen zum Warten, der Hundeführer durchgeht die etwa 10 Schritte entfernte Personengruppe und stellt sich 10-15 Schritte danach mit der Front zum Hund auf; nach etwa 3 Sekunden ruft er den Hund in die Grundstellung ab (direkt oder um den Hundeführer herum). Weicht der Hund der Gruppe aus: Mangelhaft, Abzug im Ermessen des Leistungsrichters."),
            new("Sitz aus der Bewegung", true, 20, "S. 28 f. Der Hundeführer geht mit dem freifolgenden Hund 5-10 Schritte, hält zwischen dem 5. und 10. Schritt 1 bis höchstens 2 Sekunden an und gibt das Hörzeichen zum Setzen; sobald der Hund sitzt, geht er ohne Umsehen mindestens 10 Schritte weiter, dreht sich um, wartet etwa 3 Sekunden und kehrt zurück. Falsche Stellung: Mangelhaft."),
            new("Stellungen in Frontposition", true, 20, "S. 29. Der Hundeführer tritt vor den wartenden Hund; Platz und Sitz in dieser Reihenfolge je einmal, dann Wechsel in die Grundstellung. Reihenfolge nicht eingehalten: Befriedigend -6 Punkte."),
            new("Abrufen über die Hürde", true, 20, "S. 29 f. Grundstellung 10 Schritte vor der Hürde (50 cm, auf Wunsch 20 cm), Hörzeichen zum Warten; der Hundeführer geht ohne Hund über die Hürde oder eng vorbei, stellt sich 10 Schritte danach auf und ruft nach 3 Sekunden. Auslassen der Hürde: Mangelhaft -10 Punkte."),
        ],
        Description: "Begleithund 1 der SKG nach der Nationalen Prüfungsordnung der Arbeitsgemeinschaft für das Gebrauchs- und Sporthundewesen (NPO-BH, 3. Ausgabe, gültig ab 1. März 2022); sie gilt für Prüfungen der SKG in der Schweiz (S. 3). BH 1 ist der Einstieg: Auch ein bereits in anderen Sparten ausgebildeter Hund beginnt hier (S. 8).\n" +
            "Aufbau, 300 Punkte (S. 4): A Fährte 100 (Halten der Fährte 80, Gegenstand 20; Eigenfährte), B Unterordnung 100 (Leinenführigkeit, Freifolge, Stellungen in der Fussposition, Apportieren, Hochsprung je 20), C Führigkeit 100 (Durchgehen einer Personengruppe, Abruf unter Ablenkung, Sitz aus der Bewegung, Stellungen in Frontposition, Abrufen über die Hürde je 20).\n" +
            "Mindestalter des Hundes am Prüfungstag: 12 Monate, ohne Ausnahme (S. 8). Voraussetzung: Leistungsheft und Kennzeichnung nach den gesetzlichen Bestimmungen (S. 8). Aufstieg in BH 2: Bestehen mit Ausbildungskennzeichen (AKZ) (S. 8).\n" +
            BhBewertung,
        Land: Schweiz,
        SourceUrl: QuelleNpoBh));

        // ---------------------------------------------------------------
        // BH 2: Fremdfährte und Revier, Freifolge mit Gangarten, Distanz
        // ---------------------------------------------------------------
        await SeedRegulationAsync(db, bh, new RegulationSeed("SKG BH 2", "2022", gueltig,
        [
            new("Halten der Fährte", true, 35, "Abteilung A 1 (Fährte), S. 31 f. Fremdfährte: etwa 300 Schritte mit drei 90-Grad-Winkeln, Liegezeit mindestens 30 Minuten, Ausarbeitungszeit 15 Minuten, Suchleine 6 oder 10 m. Der Hundeführer ist beim Anlegen nicht in Sichtweite. Der Wiederansatz nach einem Gegenstand erfolgt beim Hund; dafür steht ein weiteres Hörzeichen zu. Nimmt der Hund die Fährte beim Gegenstand nach dem zweiten Zusatzhörzeichen nicht auf: Teilbewertung der bisherigen Arbeit (S. 32). Sonst wie BH 1: Aufforderung zum Nachgehen -5 Punkte, mehr als eine Leinenlänge neben der Fährte Abbruch, Zeitüberschreitung höchstens Mangelhaft (S. 15)."),
            new("Gegenstände auf der Fährte", true, 15, "S. 31 f. Zwei Gegenstände mit 8 und 7 Punkten, mit der Fährtennummer gekennzeichnet; mindestens 20 Schritte nach dem Abgang bzw. vor oder nach den Winkeln, je Schenkel höchstens einer, der zweite markiert das Fährtenende. Überlaufen, Führerhilfe oder nicht wie gemeldet angezeigt: 0 Punkte auf den Gegenstand (S. 32)."),
            new("Revierarbeit", true, 35, "Abteilung A 2 (Revier), S. 33 f. Revier 35 x 70 Schritte (Schrittlänge ca. 70 cm), Ecken markiert, Ausarbeitungszeit 10 Minuten. Der Leistungsrichter teilt das Revier hälftig und wirft die Gegenstände unbeobachtet aus, ohne das Revier zu betreten (ausser bei lockerem Schnee). Der Hundeführer steht in der Mitte der Grundlinie mit dem Rücken zum Revier und meldet, auf welcher Seite er beginnt. Querschläge höchstens 6 m vor dem Hundeführer; Hundeführer auf der Mittellinie, seitlich ca. 3 m. Sind alle Gegenstände gefunden, darf der Leistungsrichter früher abbrechen."),
            new("Gegenstände im Revier", true, 15, "S. 33 f. Drei Gegenstände mit 5 / 5 / 5 Punkten. Überlaufen, Führerhilfe oder nicht wie gemeldet angezeigt: 0 Punkte auf den Gegenstand (S. 34); Fehlverweis 2 Punkte Abzug (S. 18). Nach dem Herbeibringen arbeitet der Hund von der Mittellinie weiter; beim Aufnehmen oder Verweisen darf er vor Ort bleiben oder mit zur Mittellinie genommen werden."),
            new("Freifolge", true, 20, "S. 35 f. Laufschema in vorgegebener Reihenfolge Normal-, Lauf-, Langsamschritt. Normalschritt: je eine Rechts- und Linkswendung, je zwei Kehrtwendungen rechts und links und im Verlauf ein Sitz; Laufschritt und Langsamschritt je eine Gerade ohne Wendung. Beginn und Ende in der Grundstellung, zwischen den Wendungen mindestens 10-15 Schritte, die Linkskehrtwendung immer gleich. Ein Hörzeichen für das Angehen und für jeden Schrittwechsel. Der Hund wird freifolgend angemeldet (S. 12)."),
            new("Stellungen in der Fussposition", true, 20, "S. 36. Platz und Sitz in dieser Reihenfolge je einmal; Schluss mit Sitz in der Grundstellung. Ein Hörzeichen je Stellung."),
            new("Stellungen in der Frontposition", true, 20, "S. 36 f. Der Hund wechselt aus der Grundstellung selbst in die Frontposition, zeigt Platz und Sitz je einmal und wechselt in die Grundstellung zurück, direkt oder um den Hundeführer herum. Ein Hörzeichen für die Front, jede Stellung und die Grundstellung. Grätschstellung nach rechts erlaubt, sofort beim Hörzeichen einzunehmen."),
            new("Apportieren", true, 20, "S. 37. Eigener Apportiergegenstand, mindestens 6 Schritte geworfen; Bringen, Vorsitz, Abgabe und Grundstellung auf je ein Hörzeichen. Nimmt der Hund ihn nach dem zweiten Zusatzhörzeichen nicht auf: 0 Punkte; einmaliges Nachfassen im Arbeitseifer ohne Abzug."),
            new("Hochsprung", true, 20, "S. 38. Grundstellung mindestens 5 Schritte vor der Hürde (50 cm, auf Wunsch 20 cm); auf Hörzeichen springt der Hund hin und zurück, das Ende ist die Grundstellung beim am Ort stehenden Hundeführer. Kein Sprung oder umgefallene Hürde: Wiederholung möglich, Mangelhaft -10 Punkte; im Wiederholungsfall 0 Punkte."),
            new("Abrufen in Frontposition", true, 20, "S. 39. Der Hundeführer geht ohne Hund 20 Schritte, bleibt mit dem Rücken zum Hund stehen und ruft ihn nach 3 Sekunden in die Frontposition (Sitz); danach Wechsel in die Grundstellung, direkt oder um den Hundeführer herum, ohne Grätschstellung."),
            new("Sitz aus der Bewegung", true, 10, "S. 40. 5-10 Schritte freifolgend, Hörzeichen zum Setzen aus der Bewegung; ohne Änderung der Gangart und ohne Umsehen mindestens weitere 20 Schritte, dann Umdrehen. Falsche Stellung: bis zu zwei weitere Zusatzhörzeichen auf Distanz, sonst geht der Hundeführer zurück; Mangelhaft -5 Punkte."),
            new("Stellungen auf Distanz", true, 20, "S. 40 f. Schliesst an Sitz aus der Bewegung an: Der Hundeführer steht 20 Schritte entfernt mit der Front zum Hund; aus dem Sitz Platz und Sitz in dieser Reihenfolge, dann Abruf direkt in die Grundstellung. Reihenfolge nicht eingehalten: Befriedigend -6 Punkte."),
            new("Steh aus dem Normalschritt", true, 10, "S. 41. 5-10 Schritte im Normalschritt, Hörzeichen zum Stehen; mindestens weitere 20 Schritte ohne Änderung der Gangart, Umdrehen, Abruf direkt in die Grundstellung. Falsche Stellung: Mangelhaft -5 Punkte."),
            new("Voran mit Stellung", true, 20, "S. 41 f. Aus der Grundstellung direkt Voran über mindestens 20 Schritte, seitlich an der Hürde vorbei (5 Schritte neben der Voranlinie, 10 Schritte von der Grundlinie); der Hundeführer wählt die Seite. Dort sofort Sitz, Platz oder Steh nach Wahl, danach geht der Hundeführer zum Hund. Abbruch (0 Punkte) bei schrägem Vorangehen über 45 Grad, bei zu kurzer Distanz, Überspringen der Hürde oder falscher Seite."),
            new("Abrufen über die Hürde", true, 20, "S. 42. Der Hund wartet in Sitz, Platz oder Steh 10 Schritte vor der Hürde; der Hundeführer geht darüber oder eng vorbei, stellt sich 10 Schritte danach auf und ruft nach 3 Sekunden über die Hürde in die Grundstellung. Auslassen der Hürde: Mangelhaft -10 Punkte."),
        ],
        Description: "Begleithund 2 der SKG nach der Nationalen Prüfungsordnung (NPO-BH, 3. Ausgabe, gültig ab 1. März 2022). Die Klassen sind der Reihe nach abzulegen; Voraussetzung für BH 2 ist das Bestehen von BH 1 mit AKZ (S. 8).\n" +
            "Aufbau, 300 Punkte (S. 5): A Nasenarbeit 100 (A 1 Fremdfährte 50: Halten der Fährte 35, Gegenstände 15 als 8/7; A 2 Revier 50: Ausführung 35, Gegenstände 15 als 5/5/5), B Unterordnung 100 (Freifolge, Stellungen in der Fussposition, Stellungen in der Frontposition, Apportieren, Hochsprung je 20), C Führigkeit 100 (Abrufen in Frontposition 20, Sitz aus der Bewegung 10, Stellungen auf Distanz 20, Steh aus dem Normalschritt 10, Voran mit Stellung 20, Abrufen über die Hürde 20).\n" +
            "Mindestalter des Hundes am Prüfungstag: 19 Monate, ohne Ausnahme (S. 8). Der Hund wird in B und C freifolgend angemeldet (S. 12).\n" +
            BhBewertung,
        Land: Schweiz,
        SourceUrl: QuelleNpoBh));

        // ---------------------------------------------------------------
        // BH 3: längere Fremdfährte mit spitzem und stumpfem Winkel, grosses Revier
        // ---------------------------------------------------------------
        await SeedRegulationAsync(db, bh, new RegulationSeed("SKG BH 3", "2022", gueltig,
        [
            new("Halten der Fährte", true, 35, "Abteilung A 1 (Fährte), S. 43 f. Fremdfährte: etwa 400 Schritte mit drei 90-Grad-Winkeln, einem 45-Grad- und einem 135-Grad-Winkel, Liegezeit mindestens 60 Minuten, Ausarbeitungszeit 20 Minuten, Suchleine 6 oder 10 m. Der Wiederansatz nach einem Gegenstand erfolgt beim Hund. Nimmt der Hund die Fährte beim Gegenstand nach dem zweiten Zusatzhörzeichen nicht auf: Teilbewertung der bisherigen Arbeit (S. 44). Sonst wie BH 1: Aufforderung zum Nachgehen -5 Punkte, mehr als eine Leinenlänge neben der Fährte Abbruch, Zeitüberschreitung höchstens Mangelhaft (S. 15)."),
            new("Gegenstände auf der Fährte", true, 15, "S. 43 f. Drei Gegenstände mit 5 / 5 / 5 Punkten, mit der Fährtennummer gekennzeichnet; mindestens 20 Schritte nach dem Abgang bzw. vor oder nach den Winkeln, je Schenkel höchstens einer, der dritte markiert das Fährtenende. Überlaufen, Führerhilfe oder nicht wie gemeldet angezeigt: 0 Punkte auf den Gegenstand (S. 44)."),
            new("Revierarbeit", true, 35, "Abteilung A 2 (Revier), S. 45 f. Revier 70 x 70 Schritte (Schrittlänge ca. 70 cm), Ecken markiert, Ausarbeitungszeit 10 Minuten. Der Leistungsrichter umgeht das ganze Revier und wirft die Gegenstände unbeobachtet aus, ohne das Revier zu betreten (ausser bei lockerem Schnee). Der Hundeführer steht in der Mitte der Grundlinie mit dem Rücken zum Revier. Querschläge höchstens 6 m vor dem Hundeführer; Hundeführer auf der Mittellinie, seitlich ca. 3 m. Sind alle Gegenstände gefunden, darf der Leistungsrichter früher abbrechen."),
            new("Gegenstände im Revier", true, 15, "S. 45 f. Drei Gegenstände mit 5 / 5 / 5 Punkten. Überlaufen, Führerhilfe oder nicht wie gemeldet angezeigt: 0 Punkte auf den Gegenstand (S. 46); Fehlverweis 2 Punkte Abzug (S. 18)."),
            new("Freifolge", true, 20, "S. 47 f. Drei Laufschemata in der vorgegebenen Reihenfolge Normal-, Lauf-, Langsamschritt, jedes mit je einer Rechts- und Linkswendung und je einer Kehrtwendung rechts und links. Im Normalschritt ist eine Platzstellung zu zeigen, im Langsamschritt eine Platz- und eine Stehstellung; der Ort ist frei wählbar. Jedes Schema beginnt und endet in der Grundstellung, zwischen den Wendungen mindestens 10-15 Schritte, die Linkskehrtwendung immer gleich. Ein Hörzeichen je Angehen und je Stellung."),
            new("Stellungen in der Fussposition", true, 20, "S. 48 f. Platz, Steh und Sitz in dieser Reihenfolge je einmal; Schluss mit Sitz in der Grundstellung. Im Steh muss der Hund ganz durchstehen. Ein Hörzeichen je Stellung."),
            new("Stellungen in der Frontposition", true, 20, "S. 49. Wie in BH 2: Der Hund wechselt in die Frontposition, zeigt Platz und Sitz je einmal und wechselt in die Grundstellung zurück. Ein Hörzeichen für die Front, jede Stellung und die Grundstellung."),
            new("Apportieren", true, 20, "S. 49 f. Wie in BH 2: eigener Apportiergegenstand, mindestens 6 Schritte geworfen; Bringen, Abgabe und Grundstellung auf je ein Hörzeichen. Nimmt der Hund ihn nach dem zweiten Zusatzhörzeichen nicht auf: 0 Punkte."),
            new("Hochsprung", true, 20, "S. 50 f. Grundstellung mindestens 5 Schritte vor der Hürde (50 cm, auf Wunsch 20 cm); der Hund springt auf Hörzeichen hin, nimmt auf Kommando für mindestens 3 Sekunden eine Wartestellung im Steh ein und springt auf Hörzeichen zurück; Ende in der Grundstellung beim am Ort stehenden Hundeführer. Kein Sprung oder umgefallene Hürde: Wiederholung möglich, Mangelhaft -10 Punkte; im Wiederholungsfall 0 Punkte."),
            new("Apportieren aus der Bewegung", true, 20, "S. 52 f. Der Hundeführer lässt seinen Apportiergegenstand zwischen dem 5. und 10. Schritt vor oder neben sich fallen, geht ohne Änderung der Gangart 10-15 Schritte weiter und gibt zwischen dem 10. und 15. Schritt das Hörzeichen; sobald sich der Hund löst, hält er an. Der Hund bringt den Gegenstand in den Vorsitz. Löst er sich nach dem zweiten Zusatzhörzeichen nicht oder nimmt er ihn nicht auf: 0 Punkte."),
            new("Sitz aus der Bewegung", true, 10, "S. 53. 5-10 Schritte freifolgend, Hörzeichen zum Setzen; ohne Änderung der Gangart und ohne Umsehen mindestens weitere 30 Schritte, dann Umdrehen. Falsche Stellung: bis zu zwei weitere Zusatzhörzeichen auf Distanz, sonst geht der Hundeführer zurück; Mangelhaft."),
            new("Stellungen auf Distanz", true, 20, "S. 54. Schliesst an Sitz aus der Bewegung an: Der Hundeführer steht 30 Schritte entfernt mit der Front zum Hund; aus dem Sitz auf Hörzeichen Steh, Platz und Sitz in dieser Reihenfolge, dann Abruf direkt in die Grundstellung. Reihenfolge nicht eingehalten: Befriedigend -6 Punkte."),
            new("Steh aus dem Laufschritt", true, 10, "S. 54 f. 5-10 Schritte im Laufschritt, Hörzeichen zum Stehen; der Hundeführer läuft mindestens weitere 30 Schritte ohne Änderung der Gangart, dreht sich um und ruft den Hund direkt in die Grundstellung. Falsche Stellung: Mangelhaft -5 Punkte."),
            new("Voran mit Steh und Weitersenden", true, 20, "S. 55. Direkt Voran über mindestens 20 Schritte, auf Hörzeichen Steh, dann auf ein weiteres Kommando nochmals mindestens 20 Schritte Voran; aus der Voranbewegung direkt in die Grundstellung abgerufen. Abbruch (0 Punkte): Rückkehr vor dem ersten Steh oder schräges Vorangehen über 45 Grad."),
            new("Voran mit Verschieben", true, 20, "S. 56. Direkt Voran über mindestens 40 Schritte, seitlich an der Hürde vorbei (5 Schritte neben der Voranlinie, 20 Schritte von der Grundlinie); Steh, seitliches Verschieben auf Höhe der Hürde, erneut Steh. Der Hundeführer verschiebt sich gleichzeitig, vor oder nach dem Hund. Dann Abruf direkt aus dem Steh, auf dem Rückweg überspringt der Hund die Hürde. Abbruch (0 Punkte) bei zu kurzer Distanz, Überspringen der Hürde beim Voraussenden oder falscher Seite."),
        ],
        Description: "Begleithund 3 der SKG nach der Nationalen Prüfungsordnung (NPO-BH, 3. Ausgabe, gültig ab 1. März 2022) - die höchste Klasse. Voraussetzung ist das Bestehen von BH 2 mit AKZ (S. 8).\n" +
            "Aufbau, 300 Punkte (S. 6): A Nasenarbeit 100 (A 1 Fremdfährte 50: Halten der Fährte 35, Gegenstände 15 als 5/5/5; A 2 Revier 50: Ausführung 35, Gegenstände 15 als 5/5/5), B Unterordnung 100 (Freifolge, Stellungen in der Fussposition, Stellungen in der Frontposition, Apportieren, Hochsprung je 20), C Führigkeit 100 (Apportieren aus der Bewegung 20, Sitz aus der Bewegung 10, Stellungen auf Distanz 20, Steh aus dem Laufschritt 10, Voran mit Steh und Weitersenden 20, Voran mit Verschieben 20).\n" +
            "Mindestalter des Hundes am Prüfungstag: 20 Monate, ohne Ausnahme (S. 8). Der Hund wird in B und C freifolgend angemeldet (S. 12).\n" +
            BhBewertung,
        Land: Schweiz,
        SourceUrl: QuelleNpoBh));
    }

    // -------------------------------------------------------------------
    // Agility (SKG Wettkampfordnung, Reglement Agility, gültig ab 01.01.2025,
    // beschlossen an der DKAMO vom 28.03.2023, genehmigt vom Zentralvorstand
    // der SKG am 09.10.2024)
    //
    // Wie die deutschen A0-A3 und die österreichischen LK 1-3: je Arbeitsklasse
    // eine Ordnung für den Agility-Lauf, dazu ein Jumping für alle drei.
    // Gewertet wird über Fehlerpunkte und Zeit, die Geräte tragen 0 Punkte. Das
    // Reglement kennt Arbeitsklassen 1, 2 und 3 (S. 17), die Geräte sind
    // dieselben wie bei den anderen Agility-Ordnungen und werden wiederverwendet.
    //
    // Grössenkategorien (Large bis Small) und die Oldies sind Teilnehmer-
    // kategorien, keine Leistungsstufen: Jede Kategorie läuft dieselben
    // Arbeitsklassen (S. 16 f.), die Oldies haben "keine Leistungsklassen" und
    // einen Parcours höchstens im Schwierigkeitsgrad der Klasse 2 (S. 15). Sie
    // stehen als Hinweis in der Beschreibung. Junioren und Senioren kommen nur
    // als Qualifikationsläufe zu den FCI-Open vor (S. 14), ein eigenes
    // Regelwerk dafür steht nicht im Reglement.
    //
    // Die Aufstiegskriterien stehen NICHT im Reglement: Die TKAMO legt sie
    // jährlich per Weisung fest (S. 17). Erfunden wird nichts.
    // -------------------------------------------------------------------
    private const string AgilityBewertungCh =
        "Bewertung: Abwurf, Fehler allgemeiner Art, Kontaktzonenfehler und Verweigerungen je 5 Fehlerpunkte; Zeitfehler 0,01 Punkte je Hundertstelsekunde über der Standardzeit, also 1 je Sekunde (S. 11). Rang nach Gesamtfehlerpunkten, dann weniger Parcoursfehlern, dann besserer Laufzeit (S. 14). Qualifikationsstufen wie „Vorzüglich“ nennt das Reglement nicht.\n" +
        "Disqualifikation u. a. bei der dritten Verweigerung, Überschreiten der Maximalzeit, falscher Reihenfolge und Zerstörung eines Hindernisses (S. 11-13).\n" +
        "Grössenkategorien nach Widerristhöhe: Large ab 48 cm, Intermediate 43 bis unter 48, Medium 35 bis unter 43, Small unter 35 cm (S. 16). Mindestalter 18 Monate, mit gültiger Lizenz (S. 14).";

    private const string AgilityParcoursCh =
        "Parcours: 100-220 m, 15-22 Hindernisse je nach Kategorie, davon mind. 7 Sprünge; Abstand der Geräte 5 m bis höchstens 7 m gerade bzw. 9 m auf der Lauflinie (S. 6). Slalom, Mauer und Reifen nur einmal (S. 18).";

    private const string AgilityOldiesCh =
        "Oldies (Hund ab 8 Jahren, freiwillig und endgültig) sind eine Teilnehmerklasse, keine Leistungsstufe: ohne Leistungsklassen, Parcours höchstens im Schwierigkeitsgrad der Klasse 2, niedrigere Sprunghöhen, ohne Doppelhürde, Mauer und Reifen, Schrägwand 140 cm (S. 8-10, 15).";

    private static async Task SeedSkgAgilityAsync(ApplicationDbContext db, Sport agility)
    {
        var datum = new DateOnly(2025, 1, 1);

        // Die Geräte der Arbeitsklassen. Der Doppelsprung (im Katalog
        // "Doppelsprung (Spread)", im Reglement "Doppelte Hürde") darf in der
        // Klasse 1 nicht eingesetzt werden (S. 18).
        RegulationExerciseSeed[] Geraete(bool mitDoppelsprung, bool mitKontaktzonen)
        {
            var liste = new List<RegulationExerciseSeed>
            {
                new("Hürde", true, 0, "Abwurf 5 Fehlerpunkte. Läuft der Hund unter der Stange durch, ohne dass sie fällt: Verweigerung (5); fällt sie dabei: Zerstörung des Hindernisses, Disqualifikation (S. 11). Sprunghöhe Large 55-60, Intermediate 45-50, Medium 35-40, Small 25-30 cm (S. 8)."),
            };
            if (mitDoppelsprung)
                liste.Add(new("Doppelsprung (Spread)", true, 0, "Im Reglement „Doppelte Hürde“: Abwurf 5 Fehlerpunkte. Die vordere Stange liegt 15-25 cm tiefer; Gesamttiefe höchstens Large 55, Intermediate 45, Medium 35, Small 25 cm (S. 9). Nicht in Klasse 1 (S. 18)."));
            liste.Add(new("Mauer", true, 0, "Fällt ein Mauerelement oder Seitenturm: Fehler (5). Höhe inklusive Abwurfelemente wie bei der Hürde, 3-5 Abwurfelemente; im Parcours nur einmal (S. 9, 12, 18)."));
            liste.Add(new("Reifen", true, 0, "Springt der Reifen beim Durchspringen auf: Fehler (5); läuft der Hund unter oder seitlich durch und er springt auf: Zerstörung, Disqualifikation (S. 12). Öffnung 50-60 cm, Achshöhe Large 80, Intermediate 70, Medium und Small 55 cm; nur einmal im Parcours (S. 9, 18)."));
            liste.Add(new("Weitsprung", true, 0, "Vorbeilaufen oder seitliches Ein- oder Ausspringen: Verweigerung (5); Umwerfen einer Einheit: Fehler (5); blosses Berühren und umfallende Begrenzungen werden nicht bestraft (S. 12). Zwei bis vier Elemente, Länge Large 120-150, Intermediate 90-110, Medium 70-90, Small 40-50 cm (S. 9)."));
            if (mitKontaktzonen)
            {
                liste.Add(new("Laufsteg", true, 0, "Absteigende Kontaktzone nicht mit mindestens einem Pfotenteil berührt: 5 Fehlerpunkte (S. 11). Aufsteigende Rampe nicht mit allen vier Pfoten: Disqualifikation; Abspringen ohne vorheriges Berühren des absteigenden Teils mit allen vier Pfoten: Verweigerung (S. 11). Höhe der Lauffläche 120-130 cm (S. 10)."));
                liste.Add(new("Wippe", true, 0, "Auf- und absteigende Kontaktzone müssen je mit mindestens einem Pfotenteil berührt werden, sonst 5 Fehlerpunkte (S. 11). Abspringen, bevor die Achse mit vier Pfoten überwunden ist: Verweigerung; Verlassen vor Bodenkontakt: Fehler (S. 12). Höhe am Drehpunkt 60 cm (S. 10)."));
                liste.Add(new("Schrägwand (A-Wand)", true, 0, "Absteigende Kontaktzone nicht berührt: 5 Fehlerpunkte (S. 11). Aufstieg nicht mit allen vier Pfoten oder Boden berühren, ohne den absteigenden Teil berührt zu haben: Disqualifikation; Abspringen ohne vorheriges Berühren des absteigenden Teils mit allen vier Pfoten: Verweigerung (S. 12). Höchster Punkt 170 cm für alle Hunde (S. 10)."));
            }
            liste.Add(new("Slalom", true, 0, "12 Stangen, 60 cm Abstand. Die erste Stange liegt links vom Hund; fehlerhaftes Einfädeln: Verweigerung (5). Ein verfehltes Tor ist ein Fehler, der sofort zu korrigieren ist (oder der Hund wird an den Anfang zurückgeführt), insgesamt höchstens 5 Fehlerpunkte; mehr als 2 Tore in die falsche Richtung: Disqualifikation (S. 10, 12). Nur einmal im Parcours (S. 18)."));
            liste.Add(new("Tunnel", true, 0, "Steckt der Hund Pfote oder Kopf hinein und zieht sich zurück oder wendet er im Tunnel und verlässt ihn auf der falschen Seite: Verweigerung (5) (S. 12). Durchmesser 60 cm, Länge 3-6 m (S. 10); der Veranstalter stellt vier Tunnel, einer davon 3-4 m lang (S. 8)."));
            return [.. liste];
        }

        await SeedRegulationAsync(db, agility, new RegulationSeed("SKG Agility Klasse 1", "2025", datum,
            Geraete(mitDoppelsprung: false, mitKontaktzonen: true),
            Description: "SKG-Agility, Arbeitsklasse 1: Agility-Wettbewerb (Parcours mit Kontaktzonen) und Jumping-Wettbewerb (ohne). Jeder Hund startet am ersten Wettbewerb in der tiefsten Klasse seiner Kategorie (S. 17). Reglement Agility, gültig ab 01.01.2025 (S. 5).\n" +
                AgilityParcoursCh + "\n" +
                "Klasse 1: Kein Doppelsprung (S. 18). Die Standardzeit legt der Richter fest, die Maximalzeit ist das 1,5-fache der Standardzeit; vor dem ersten Wettbewerb gibt es ein Briefing (S. 7). Ein Agility-Wettbewerb braucht drei unterschiedliche Kontaktzonengeräte (S. 18).\n" +
                "Aufstieg in die Klasse 2: Die Kriterien legt die TKAMO jährlich per Weisung fest und sie stehen nicht im Reglement; zwischen erstem und letztem geforderten Resultat dürfen höchstens 24 Monate liegen (S. 17).\n" +
                AgilityOldiesCh + "\n" +
                AgilityBewertungCh,
            Land: Schweiz,
            SourceUrl: QuelleAgilityCh));

        await SeedRegulationAsync(db, agility, new RegulationSeed("SKG Agility Klasse 2", "2025", datum,
            Geraete(mitDoppelsprung: true, mitKontaktzonen: true),
            Description: "SKG-Agility, Arbeitsklasse 2: Agility-Wettbewerb (Parcours mit Kontaktzonen) und Jumping-Wettbewerb (ohne). Der Doppelsprung ist ab dieser Klasse zugelassen (S. 18).\n" +
                AgilityParcoursCh + "\n" +
                "Klassen 2 und 3: Die Standardzeit wird aus der Laufzeit des schnellsten Teams mit den wenigsten Parcoursfehlern berechnet, mit dem Aufrechnungsfaktor der höchsten beteiligten Klasse (S. 7); die Maximalzeit ergibt sich aus Parcourslänge und der FCI-Geschwindigkeit (S. 8). Elektronische Zeitmessung ist Pflicht (S. 8). Ein Agility-Wettbewerb braucht drei, höchstens vier Kontaktzonengeräte (S. 18).\n" +
                "Aufstieg in die Klasse 3: Die Kriterien legt die TKAMO jährlich per Weisung fest und sie stehen nicht im Reglement (S. 17). Ein freiwilliger Abstieg in die Klasse 1 ist jederzeit möglich; für den Wiederaufstieg zählen nur Resultate nach dem Abstieg (S. 18).\n" +
                AgilityOldiesCh + "\n" +
                AgilityBewertungCh,
            Land: Schweiz,
            SourceUrl: QuelleAgilityCh));

        await SeedRegulationAsync(db, agility, new RegulationSeed("SKG Agility Klasse 3", "2025", datum,
            Geraete(mitDoppelsprung: true, mitKontaktzonen: true),
            Description: "SKG-Agility, Arbeitsklasse 3 - die höchste Klasse: Agility-Wettbewerb (Parcours mit Kontaktzonen) und Jumping-Wettbewerb (ohne).\n" +
                AgilityParcoursCh + "\n" +
                "Standardzeit, Maximalzeit, elektronische Zeitmessung und Kontaktzonengeräte wie in Klasse 2 (S. 7 f., 18).\n" +
                "Um in der Klasse 3 zu bleiben, muss der Hund die Bestätigungskriterien erfüllen, die die TKAMO jährlich per Weisung festlegt und die nicht im Reglement stehen; sonst steigt er in die Klasse 2 ab (S. 17 f.). Ein freiwilliger Abstieg ist jederzeit möglich (S. 18).\n" +
                AgilityOldiesCh + "\n" +
                AgilityBewertungCh,
            Land: Schweiz,
            SourceUrl: QuelleAgilityCh));

        await SeedRegulationAsync(db, agility, new RegulationSeed("SKG Jumping (Klasse 1-3)", "2025", datum,
            Geraete(mitDoppelsprung: true, mitKontaktzonen: false),
            Description: "SKG-Jumping - dasselbe Reglement wie beim Agility, aber ohne Kontaktzonengeräte, also ohne Laufsteg, Wippe und Schrägwand (S. 5). Gelaufen in den Arbeitsklassen 1, 2 und 3; an einem Agility-Wettkampf sind ein Agility- und ein Jumping-Wettbewerb mit eigener Rangliste je Kategorie und Klasse vorgeschrieben (S. 18). Die Unterschiede der Klassen liegen im Schwierigkeitsgrad und in der Parcourslänge (S. 18).\n" +
                AgilityParcoursCh + "\n" +
                "Der Doppelsprung ist erst ab Klasse 2 zugelassen (S. 18). Für den Aufstieg zählen die Resultate in Agility- und Jumping-Wettbewerben; die Kriterien legt die TKAMO jährlich per Weisung fest und sie stehen nicht im Reglement (S. 17).\n" +
                AgilityOldiesCh + "\n" +
                AgilityBewertungCh,
            Land: Schweiz,
            SourceUrl: QuelleAgilityCh));
    }
}

import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Versionshinweise - was sich in Dogity geändert hat, in Worten für Nutzer.
 *
 * Bewusst von Hand gepflegt und NICHT aus der Git-Historie erzeugt.
 * Commit-Texte sind für Entwickler geschrieben ("MapLibre GL gegengeprüft:
 * Nachteile treffen zu"); wer wissen will, ob sich sein Training jetzt anders
 * erfassen lässt, hat davon nichts. Eine automatisch erzeugte Liste wäre
 * vollständig und trotzdem unbrauchbar.
 *
 * Die Liste wird in das Frontend-Bundle einkompiliert. Das ist kein
 * Nebeneffekt, sondern der Grund, warum die Anzeige nicht lügen kann: Jede
 * Umgebung zeigt genau die Einträge, die in ihrem eigenen Stand enthalten
 * sind - Test kann keine Fassung melden, die auf Prod noch gar nicht läuft,
 * und umgekehrt. Voraussetzung dafür ist die eine Regel:
 *
 *     Ein neuer Eintrag gehört in denselben Commit wie die Änderung,
 *     die er beschreibt.
 *
 * Nummerierung: MAJOR.MINOR, je Veröffentlichung eine Minor-Stufe höher,
 * eine dritte Stelle nur für Fehlerbehebungen zwischendurch (0.9.1). Kein
 * SemVer im engeren Sinn - es gibt keine öffentliche Schnittstelle, deren
 * Bruch eine Major-Stufe rechtfertigen würde. 1.0 bleibt bewusst frei: als
 * Ansage, nicht als Nebenwirkung des Hochzählens.
 *
 * Neueste Fassung steht oben. Darauf verlässt sich die Anzeige, und
 * versionshinweise.test.ts wacht darüber.
 */

export type Aenderungsart = "neu" | "verbessert" | "behoben";

export const AENDERUNGSART_LABEL: Record<Aenderungsart, string> = {
  neu: uebersetzbar("Neu"),
  verbessert: uebersetzbar("Verbessert"),
  behoben: uebersetzbar("Behoben"),
};

export type Aenderung = {
  art: Aenderungsart;
  text: string;
};

export type Versionshinweis = {
  /** "0.9" oder "0.9.1" - siehe Nummerierungsregel oben. */
  version: string;
  /** Tag der Veröffentlichung als ISO-Datum (YYYY-MM-DD). */
  datum: string;
  /** Eine Zeile, die den Kern der Fassung benennt. Keine Aufzählung. */
  titel: string;
  aenderungen: Aenderung[];
};

/**
 * Alle Einträge bis einschließlich 0.9 sind nachträglich aus der
 * Entwicklungsgeschichte zusammengetragen worden - die Daten stimmen, die
 * Einteilung in Fassungen ist im Nachhinein gezogen. Ab der nächsten Fassung
 * entsteht jeder Eintrag zusammen mit der Änderung selbst.
 */
export const NACHTRAEGLICH_BIS = "0.9";

export const VERSIONSHINWEISE: Versionshinweis[] = [
  {
    version: "0.41",
    datum: "2026-10-27",
    titel: uebersetzbar("Gesundheit: Impfungen, Entwurmung, Tierarzt"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Jeder Hund hat jetzt den Bereich „Gesundheit“ (neben Plan und Tagebuch): Impfungen wie Tollwut, Entwurmung, Parasitenschutz und Tierarzttermine mit Datum, Uhrzeit und Notiz. Du trägst selbst ein, bis wann eine Impfung gilt oder wann die nächste Entwurmung fällig ist – so, wie es der Tierarzt im Impfpass vermerkt hat. Dogity rechnet nichts aus; die Schnellwahl (+1 Jahr, +3 Jahre, +3 Monate) füllt das Datum nur vor."),
      },
      {
        art: "neu",
        text: uebersetzbar("Oben steht „Als Nächstes“: Abgelaufenes in Rot, bald Fälliges in Gelb und der nächste Tierarzttermin. Wenn du eine Impfung erneuerst, trägst du sie einfach neu ein – die alte bleibt im Verlauf. Auf der Startseite erscheint ein kleiner Hinweis, wenn etwas abgelaufen oder in zwei Wochen fällig ist oder ein Tierarzttermin in den nächsten zwei Tagen ansteht."),
      },
      {
        art: "neu",
        text: uebersetzbar("Erinnerungen: Dogity meldet sich 14 Tage vor Ablauf und am Tag selbst, bei Tierarztterminen am Vortag ab 18 Uhr – als Meldung in der Glocke und als Push, nie per E-Mail. Unter Profil → Funktionen lässt sich die Erinnerung abschalten, und dort kannst du den Bereich „Gesundheit“ auch ganz ausblenden; deine Einträge bleiben dabei erhalten."),
      },
      {
        art: "neu",
        text: uebersetzbar("Die Gesundheitsangaben sehen nur du und die Mitbesitzer:innen des Hundes – deine Trainer:in sieht sie nicht. Ein Foto vom Impfpass gibt es bewusst nicht: Dogity speichert keine Dateien."),
      },
    ],
  },
  {
    version: "0.40",
    datum: "2026-10-26",
    titel: uebersetzbar("Prüfungsreife: Wie weit seid ihr?"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Zu jedem Prüfungsziel zeigt Dogity jetzt die Prüfungsreife: eine Ampel (grün, gelb, rot) und für jede Übung der Prüfungsordnung, ob sie sicher sitzt, noch wackelt oder noch offen ist. Sie steht als kleine Zeile auf der Startseite in der Zielkarte und im Trainingsplan des Hundes; ein Tipp darauf zeigt dir, was bis zur Prüfung noch fehlt, mit dem Grund je Übung, etwa „zuletzt vor 35 Tagen“ oder „noch nie trainiert“."),
      },
      {
        art: "neu",
        text: uebersetzbar("Gerechnet wird aus eurem Trainingstagebuch: Eine Übung gilt als sicher, wenn sie wiederholt gut gelaufen ist, ohne zwischendurch abzurutschen, im Schnitt gut bewertet wurde und noch nicht länger als vier Wochen her ist. Tage, an denen dein Hund pausiert hat, zählen dabei nicht mit. Das ist eine Faustregel und kein Urteil der Richter:in – unter „Wie wird das berechnet?“ steht, wie genau."),
      },
      {
        art: "neu",
        text: uebersetzbar("Bei der Begleithundeprüfung (BH/VT) gehört die Theorie zur Ampel: Sie ist sicher, wenn du die letzten beiden Übungsprüfungen der Sachkunde bestanden hast. Diese Zeile siehst nur du – deine Trainer:in sieht sie nicht."),
      },
      {
        art: "neu",
        text: uebersetzbar("Wer Hunde betreut, sieht die Ampel und die Übungsliste auch bei den betreuten Hunden. Steht die Ampel auf rot und bleiben nur noch wenige Wochen, fragt Dogity, ob das Zieldatum verschoben werden soll – verschoben wird nie von selbst."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Wird eine Übungsprüfung der Sachkunde zweimal abgeschickt, etwa durch einen Doppeltipp oder eine verlorene Antwort bei schlechtem Empfang, wird sie nur noch einmal gespeichert."),
      },
    ],
  },
  {
    version: "0.39",
    datum: "2026-10-23",
    titel: uebersetzbar("Sachkunde: Übungsprüfung"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Bei der Sachkunde gibt es jetzt eine Übungsprüfung: eine Frage nach der anderen zum Ankreuzen, ohne Auflösung zwischendurch – erst nach dem Abgeben siehst du Punkte, Ergebnis je Themenkomplex und deine falschen Fragen mit der richtigen Antwort. Für Erwachsene sind es 25 Fragen, im Jugendkatalog 15 aus 40 Fragen. Du findest sie auf der Seite des Fragenkatalogs, auch ohne Anmeldung."),
      },
      {
        art: "neu",
        text: uebersetzbar("Gewertet wird nach der verbreiteten Regel: 2 Punkte für jede richtige Antwort, 2 Punkte Abzug für jede falsch angekreuzte, bestanden ab 70 %. Das ist eine Übung und keine offizielle Prüfung – dein Verband oder Verein kann anders werten. Deshalb heißt das Ergebnis „Übung bestanden“ oder „Übung nicht bestanden“."),
      },
      {
        art: "neu",
        text: uebersetzbar("Wenn du angemeldet bist, speichert Dogity das Ergebnis und zeigt dir deine letzten Übungsprüfungen. Die Fragen, die du falsch hattest, kommen in den Fehlerspeicher und beim Üben wieder; „Falsche Fragen üben“ führt dich direkt dorthin. Richtig beantwortete Fragen ändern deinen Lernstand nicht."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Eine laufende Übungsprüfung geht nicht verloren, wenn du die Seite neu lädst."),
      },
    ],
  },
  {
    version: "0.38",
    datum: "2026-10-22",
    titel: uebersetzbar("Rally Obedience"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Rally Obedience ist als neue Sportart dazugekommen: ein Parcours aus Schildern, an denen Hund und Mensch gemeinsam Aufgaben zeigen – gewertet wird über Abzüge von 100 Punkten. Die internationale FCI-Klasse gilt in jedem Land und steht mit allen Übungen und Bewertungshinweisen im Katalog, beim Anlegen eines Ziels und beim Eintragen eines Trainings; auf Englisch auch mit englischen Texten und den Schildern in ihren Originalbezeichnungen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Dazu kommen je nach Land die Klassen Beginner, 1, 2, 3 und Senioren: für Deutschland die des VDH (Regelwerk ab 01.01.2027), für Österreich die des ÖKV. Sie erscheinen nur, wenn das jeweilige Land gewählt ist. Statt jedes einzelnen Schildes gibt es trainierbare Gruppen wie Wendungen, Slalom oder Abruf über die Hürde; welche Schilder zu einer Klasse gehören, steht in den Hinweisen der Ordnung."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Wenn du in den Einstellungen bestimmte Sportarten gewählt hast, bleibt diese Auswahl unverändert. Rally Obedience ergänzt du dort selbst."),
      },
    ],
  },
  {
    version: "0.37",
    datum: "2026-10-22",
    titel: uebersetzbar("Erinnerungen als Meldung und Push"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Dogity erinnert dich jetzt von selbst, in der Glocke und als Push, wenn du Push eingeschaltet hast: am Abend vorher an Gruppentermine („Morgen 18:00: Welpengruppe – du hast zugesagt“ oder „kommst du?“; wer abgesagt hat, hört nichts) und gut drei Stunden nach dem Legen daran, dass eine Fährte noch nicht abgelaufen ist, solange das noch geht. Ist dein Hund gerade in einer Trainingspause, kommt keine Fährten-Erinnerung."),
      },
      {
        art: "neu",
        text: uebersetzbar("Trainer:innen bekommen auf Wunsch eine Zusammenfassung, wie viele Trainings auf ihre Rückmeldung warten – nur, wenn es welche gibt. Wochentage und Uhrzeit wählst du selbst; Vorgabe ist Sonntag um 18 Uhr. Rückmeldungen bleiben freiwillig."),
      },
      {
        art: "neu",
        text: uebersetzbar("Jede Erinnerung lässt sich unter „Profil“ → „Funktionen“ → „Erinnerungen“ einzeln ausschalten. Zwischen 22 und 8 Uhr schickt Dogity keine Erinnerungen, und es kommt nie eine Mail dazu."),
      },
    ],
  },
  {
    version: "0.36",
    datum: "2026-10-22",
    titel: uebersetzbar("Trainingspausen: verletzt, krank, läufig"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Ist dein Hund verletzt, krank oder – bei Hündinnen – läufig, kannst du jetzt eine Trainingspause eintragen: beim Hund unter „Verwalten“ → „Trainingspause“. Du wählst den Grund, den Beginn und, wenn du es schon weißt, das Ende; bei einer Läufigkeit schlägt Dogity drei Wochen vor. Auf der Hundeseite und der Startseite steht dann zum Beispiel „Pause: verletzt · bis 03.11.“, und mit „Pause beenden“ ist dein Hund mit einem Tipp wieder dabei. Vergangene Pausen bleiben im Verlauf, du kannst sie ändern oder löschen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Der Trainingsplan pausiert mit: Wochen in der Pause gelten nicht als versäumt, und danach geht es dort weiter, wo der Plan stand. Reicht die Zeit bis zur Prüfung dadurch nicht mehr, fragt Dogity, ob du das Zieldatum verschieben möchtest. Von selbst ändert sich das Datum nie."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Training eintragen geht auch während einer Pause, zum Beispiel für leichtes Training oder Physio – Dogity weist nur kurz darauf hin. Trainer:innen, die deinen Hund betreuen, sehen nur, dass er pausiert und bis wann, nicht den Grund und nicht deine Notiz."),
      },
    ],
  },
  {
    version: "0.35",
    datum: "2026-10-21",
    titel: uebersetzbar("Obedience"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Obedience ist als neue Sportart dazugekommen, zuerst mit den FCI-Klassen 1 bis 3. Sie gelten in jedem Land und stehen mit allen Übungen und Bewertungshinweisen im Katalog, beim Anlegen eines Ziels und beim Eintragen eines Trainings. Auf Englisch erscheinen sie wie die übrigen FCI-Ordnungen in englischer Sprache."),
      },
      {
        art: "neu",
        text: uebersetzbar("Dazu kommen je nach Land die Einsteigerklassen: für Deutschland Beginner und Senioren (VDH), für Österreich Beginner und Senior (ÖKV) und für die Schweiz Beginners (SKG). Sie erscheinen nur, wenn das jeweilige Land gewählt ist. Übungen, die in mehreren Klassen vorkommen, zum Beispiel die Freifolge, gibt es nur einmal; was je Klasse anders ist, steht bei der jeweiligen Ordnung."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Wenn du in den Einstellungen bestimmte Sportarten gewählt hast, bleibt diese Auswahl unverändert. Obedience ergänzt du dort selbst."),
      },
    ],
  },
  {
    version: "0.34",
    datum: "2026-10-18",
    titel: uebersetzbar("Übungsbeschreibungen auf Englisch"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Wenn du Dogity auf Englisch nutzt, zeigen die FCI-Prüfungsordnungen jetzt auch ihre Texte auf Englisch: die Rahmenbedingungen einer Prüfung, die Bewertungskriterien der Übungen und die Hinweise zu den einzelnen Übungen. Du findest sie im Katalog, beim Anlegen eines Ziels und beim Eintragen eines Trainings. Die Texte sind unsere eigenen Formulierungen in der Fachsprache der englischen FCI-Ordnung, keine Kopie der offiziellen Ordnung."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Bereiche der FCI-Übungen, zum Beispiel „Abteilung B“ oder „Unterordnung“, erscheinen auf Englisch als „Phase B“ und „Obedience“. Die nationalen Ordnungen (VDH, ÖKV, SKG) bleiben deutsch."),
      },
    ],
  },
  {
    version: "0.33",
    datum: "2026-10-18",
    titel: uebersetzbar("Schneller speichern, richtiges Datum nach Mitternacht"),
    aenderungen: [
      {
        art: "verbessert",
        text: uebersetzbar("Eine Fährte, einen Ablauf oder ein Training mit Ort und Uhrzeit zu speichern geht jetzt ohne Warten. Das Wetter holt Dogity erst danach im Hintergrund und trägt es nach – es erscheint nach wenigen Sekunden von selbst, du musst die Seite nicht neu laden."),
      },
      {
        art: "behoben",
        text: uebersetzbar("In den Stunden nach Mitternacht (bis etwa 1 bzw. 2 Uhr) rechnete Dogity noch mit dem Vortag. Das betraf die Startseite mit den heutigen Fährten, die Fristen bei Zielen, die Statistik und den Beginn einer Betreuung. Jetzt zählt der Tag, an dem du dich gerade befindest."),
      },
    ],
  },
  {
    version: "0.32",
    datum: "2026-10-17",
    titel: uebersetzbar("Prüfungsordnungen für Österreich und die Schweiz"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Wer Österreich als Land gewählt hat, findet jetzt die Prüfungsordnungen des ÖKV: die Mensch-Hund-Team-Prüfung (BH/VT), Agility mit den Leistungsklassen 1 bis 3 und Jumping, Hoopers H1 bis H3 sowie den Breitensport mit Vierkampf, Läufen, CSC und Shorty."),
      },
      {
        art: "neu",
        text: uebersetzbar("Der Breitensport ist als eigene Sportart dazugekommen. Er erscheint nur, wenn Österreich gewählt ist."),
      },
      {
        art: "neu",
        text: uebersetzbar("Wer die Schweiz als Land gewählt hat, findet jetzt die Prüfungsordnungen der SKG: den Begleithund 1 bis 3 mit Fährte, Revier, Unterordnung und Führigkeit sowie Agility mit den Arbeitsklassen 1 bis 3 und Jumping."),
      },
      {
        art: "neu",
        text: uebersetzbar("Der Schweizer Begleithund ist als eigene Sportart dazugekommen („Begleithund (SKG)“). Er erscheint nur, wenn die Schweiz gewählt ist."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Sportarten, die es nur in einem anderen Land gibt, werden dir nicht mehr angeboten – in der Sportartenliste, beim Eintragen und beim Anlegen eines Ziels. Bestehende Ziele und Trainings bleiben unverändert sichtbar."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Beim Anlegen eines Ziels werden nur noch die Prüfungsordnungen deines Landes und die international gültigen angeboten."),
      },
    ],
  },
  {
    version: "0.31",
    datum: "2026-10-16",
    titel: uebersetzbar("Internationale Prüfungsordnungen in allen Ländern"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Die internationalen FCI-Prüfungsordnungen – Begleithundeprüfung (BH/VT), IBGH, IGP, Fährte, Unterordnung, Schutzdienst, Stöbern und Ausdauer – gelten jetzt in jedem Land und erscheinen bei jedem Geltungsbereich, nicht nur bei Deutschland. Turnierhundsport und Agility bleiben bei Deutschland, weil es Ordnungen des VDH sind."),
      },
      {
        art: "neu",
        text: uebersetzbar("Wenn du Dogity auf Englisch nutzt, zeigen die FCI-Prüfungsordnungen jetzt ihre offiziellen englischen Namen – bei Sportarten, Prüfungsstufen und Übungen, zum Beispiel „Heeling on leash“ statt „Fußarbeit“. Das gilt im Katalog, im Trainingsplan, im Tagebuch, in der Statistik und beim Eintragen. Die Beschreibungstexte bleiben deutsch."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Länderauswahl in den Einstellungen ist jetzt für alle sichtbar, weil die internationalen Prüfungsordnungen in jedem Land angeboten werden. Wer ein anderes Land als Deutschland wählt, sieht dort keine Ordnungen des VDH wie Hoopers, Agility und Turnierhundsport."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Hoopers war bei jedem Land zu sehen, obwohl es die Prüfungsordnung des VDH ist. Sie gilt jetzt nur bei Deutschland."),
      },
    ],
  },
  {
    version: "0.30",
    datum: "2026-10-16",
    titel: uebersetzbar("Dogity auf Englisch"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Auf der Startseite, bei der Anmeldung und bei der Registrierung gibt es oben jetzt einen kleinen Knopf (DE / EN), mit dem du zwischen Deutsch und English wechselst – auch ohne Konto. Deine Wahl bleibt in diesem Browser gespeichert. Wer angemeldet ist, ändert die Sprache wie bisher im Profil."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Startseite, Anmeldung, Registrierung und die Seiten zum Zurücksetzen des Passworts gibt es jetzt auch auf Englisch. Die Prüfungsordnungen, die Sachkundefragen und das Impressum bleiben deutsch."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Meldungen vom Server – etwa „Hund nicht gefunden.“ oder Hinweise zum Passwort – erscheinen jetzt in der Sprache, die du eingestellt hast."),
      },
      {
        art: "behoben",
        text: uebersetzbar("An einigen Stellen blieb auch bei eingestelltem Englisch deutscher Text stehen, zum Beispiel im Sachkunde-Trainer, beim Installationshinweis, im Katalog-Editor und bei einigen Beschriftungen für Screenreader. Das ist jetzt durchgehend übersetzt."),
      },
      {
        art: "neu",
        text: uebersetzbar("Benachrichtigungen, Mitteilungen auf dem Gerät und E-Mails von Dogity kommen jetzt in der Sprache, die du eingestellt hast – Deutsch oder English. Schon erhaltene Benachrichtigungen bleiben, wie sie waren."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Registrierst du dich auf English, kommt schon die Willkommensmail auf English, und die Sprache bleibt gespeichert. Bestehende Konten bekommen weiter deutsche Mails und Mitteilungen, bis du die Sprache im Profil auf English stellst."),
      },
      {
        art: "neu",
        text: uebersetzbar("Die Datenschutzerklärung gibt es jetzt auch auf Englisch. Wer Dogity auf English nutzt, sieht sie dort automatisch und kann jederzeit zur deutschen Fassung wechseln – sie ist rechtlich verbindlich."),
      },
    ],
  },
  {
    version: "0.29",
    datum: "2026-10-16",
    titel: uebersetzbar("Fährte über Mitternacht, Betreuung beenden"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Unter „Verwalten“ bei deinem Hund siehst du jetzt, welche Trainer:innen ihn betreuen – und kannst die Betreuung selbst beenden. Die Trainer:in sieht den Hund danach nicht mehr und wird benachrichtigt."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Eine Fährte lässt sich jetzt bis 24 Stunden nach dem Legen erneut ablaufen – auch nach Mitternacht. Vorher ging das nur am selben Kalendertag, sodass abends gelegte Fährten am nächsten Morgen nicht mehr zu suchen waren."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Zwischen Mitternacht und 1 bzw. 2 Uhr nachts hielt die App noch den Vortag für „heute“: Eine kurz nach Mitternacht gespeicherte Fährte landete am Vortag, die Startseite zeigte Termine von gestern, und neue Termine waren mit dem Vortag vorbelegt. Jetzt zählt die Uhr deines Geräts."),
      },
    ],
  },
  {
    version: "0.28",
    datum: "2026-10-16",
    titel: uebersetzbar("E-Mails abbestellen"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Bei der Registrierung wählst du jetzt, ob wichtige Benachrichtigungen – Einladungen und Entscheidungen über Beitrittsanfragen – auch per E-Mail kommen. Das Häkchen „Wichtiges auch per E-Mail“ ist vorausgewählt, du kannst es abwählen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Jede dieser Mails und die Willkommensmail haben unten einen Link „Hier abmelden“. Damit bestellst du sie ohne Anmeldung ab – und auf der Seite dahinter schaltest du sie mit „Wieder anmelden“ genauso wieder ein."),
      },
    ],
  },
  {
    version: "0.27",
    datum: "2026-10-15",
    titel: uebersetzbar("Hoopers"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Hoopers ist dabei – nach der neuen VDH-Prüfungsordnung, die ab Januar 2027 gilt, mit den Klassen H0 bis H3. Wähle Hoopers unter Profil → „Sportarten, die ich trainiere“, dann kannst du Trainings eintragen und ein Ziel mit Trainingsplan anlegen. Der Plan übt nicht nur Hoop, Tonne, Gate und Tunnel, sondern auch das Führen auf Distanz: Start, Richtungszeichen, flüssiger Lauf und Reihenfolge."),
      },
    ],
  },
  {
    version: "0.26",
    datum: "2026-10-15",
    titel: uebersetzbar("Push-Nachrichten mit einem Tipp"),
    aenderungen: [
      {
        art: "verbessert",
        text: uebersetzbar("Push-Nachrichten schaltest du jetzt direkt auf der Startseite oder in der Glocke ein – ein Tipp genügt. Auf dem iPhone geht das in Dogity vom Home-Bildschirm."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Hinweise „Als App installieren“ und „Mitteilungen aufs Handy“ erscheinen nur noch auf Handy und Tablet, nicht mehr am Rechner. Push lässt sich am Rechner weiterhin unter Profil → Funktionen einschalten."),
      },
    ],
  },
  {
    version: "0.25",
    datum: "2026-10-15",
    titel: uebersetzbar("Push-Nachrichten und E-Mails"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Push-Nachrichten aufs Handy: Unter Profil → Funktionen schaltest du für jedes Gerät einzeln ein, dass neue Meldungen aus der Glocke auch als Mitteilung ankommen, selbst wenn Dogity geschlossen ist. Auf dem iPhone geht das nur, wenn du Dogity zum Home-Bildschirm hinzufügst (Teilen → Zum Home-Bildschirm). Am App-Symbol kann dann auch die Zahl der ungelesenen Meldungen stehen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Dogity kann dir E-Mails schicken: eine Willkommensmail nach der Registrierung und - bei „Passwort vergessen“ - den Link zum Zurücksetzen direkt in dein Postfach, statt dass der Betreiber sich meldet."),
      },
      {
        art: "neu",
        text: uebersetzbar("Einladungen (in einen Verein, eine Gruppe, als Trainer:in einer Gruppe oder zum Mitbesitz eines Hundes) und Entscheidungen über Beitrittsanfragen kann Dogity dir auch per E-Mail schicken. Unter Profil → Funktionen schaltest du das mit „Wichtiges auch per E-Mail“ ab. Wer um Aufnahme in eine Gruppe gebeten hat, bekommt jetzt außerdem Bescheid, ob die Trainer:in zugestimmt hat."),
      },
    ],
  },
  {
    version: "0.24",
    datum: "2026-10-15",
    titel: uebersetzbar("Neue Karte für Fährten"),
    aenderungen: [
      {
        art: "behoben",
        text: uebersetzbar("Beim Anlegen eines Ziels wählst du jetzt immer die Prüfung, etwa UPr 1. Vorher ging auch „Allgemein“ – dann standen im Plan Übungen aller Stufen, bei der UPr zum Beispiel „Steh“, das erst ab UPr 2 geprüft wird. Wer ohne Prüfung trainieren will, wählt den individuellen Plan."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Karte ist neu: gestochen scharf bei jeder Zoomstufe, mit einem echten dunklen Stil, und Acker, Wiese, Wald und Feldwege sind klar zu unterscheiden. Unten links zeigt ein Maßstab die Meter."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Beim Legen und Ablaufen dreht sich die Karte in Laufrichtung, sobald du ein paar Schritte gegangen bist - verschieben und zoomen kannst du sie dabei trotzdem. Der Knopf oben rechts zeigt, was gerade gilt, und schaltet zwischen „Laufrichtung“ und „Norden“ um; bei „Norden“ zeigt ein Pfeil, wohin du gehst."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("In Karten auf der Hundeseite verschiebst und zoomst du mit zwei Fingern; mit einem Finger scrollst du einfach die Seite weiter. Ein Tipp auf eine Linie oder einen Punkt zeigt, was er bedeutet."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Beim Hineinzoomen lädt die Karte kaum noch nach - hilfreich bei schlechtem Empfang auf dem Platz."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Lädt dich eine Trainer:in per E-Mail-Adresse in einen Verein ein, nimmst du die Einladung jetzt selbst an oder lehnst sie ab - auf der Startseite oder unter Vereine. Bis dahin bist du kein Mitglied, und der Verein sieht nur die Adresse, die eingegeben wurde."),
      },
    ],
  },
  {
    version: "0.23",
    datum: "2026-10-09",
    titel: uebersetzbar("Weniger tippen beim Eintragen"),
    aenderungen: [
      {
        art: "verbessert",
        text: uebersetzbar("Hast du genau einen Hund, führt „Hunde“ unten gleich auf seine Seite - die Liste mit nur einer Karte entfällt. Über „Zurück“ kommst du zur Liste, etwa um einen zweiten Hund anzulegen. „Fährte legen“ auf der Startseite öffnet jetzt direkt das Fenster zum Legen, ohne erst zur Hundeseite zu wechseln."),
      },
      {
        art: "neu",
        text: uebersetzbar("Trägst du eine Übung ein, die diese Woche in deinem Trainingsplan steht, zählt sie jetzt von selbst für den Plan - du musst kein „Plan-Ziel“ mehr wählen. Beim Eintragen steht dann „zählt für den Plan“ mit dem Stand der Woche; mit „nicht zählen“ nimmst du die Übung wieder heraus. Das gilt auch bei „Wie beim letzten Mal“. Trainings, die du nachträgst, zählen nur für die Woche, in der sie stattgefunden haben."),
      },
      {
        art: "neu",
        text: uebersetzbar("Training eintragen geht jetzt mit wenigen Tipps - und überall im selben Fenster: bei „Training erfassen“ auf der Startseite und auf der Hundeseite, und mit einem Tipp auf eine Übung im Plan. Du tippst die Übungen an, die ihr gemacht habt - aus „Diese Woche“, „Zuletzt geübt“ oder mit „Wie beim letzten Mal“. Mit „Andere Übung“ suchst du in allen Sportarten deines Hundes oder trägst eine eigene ein. Dann antwortest du einmal auf „Wie lief es?“ (Mäßig, Gut oder Top) und speicherst. Willst du es für eine Übung genauer, öffnest du „genauer“: Sterne, Erfolgreich, Kommentar. Der Tag ist Heute, Gestern oder ein anderer."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Dauer, Uhrzeit und Ort sind im Fenster schon vorbelegt und stehen unten, mit einem Tipp änderbar: die Dauer deiner letzten Einheit (sonst 30 Minuten), die Uhrzeit von jetzt und der Ort, wenn dein letztes Training höchstens 14 Tage her ist oder heute ein Gruppentermin mit Ort ansteht - sonst übernimmst du ihn mit „Standort verwenden“. Verfassung und eine Notiz zum ganzen Training findest du hinter „Mehr“."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Nach dem Speichern bringt dich „Ansehen“ zum Eintrag im Tagebuch. Ein Tipp neben das Fenster schließt es nicht mehr, sobald du schon Übungen gewählt hast - das X tut es. Auch ohne Internet trägst du ein: Das Training wird übertragen, sobald wieder eine Verbindung besteht."),
      },
      {
        art: "neu",
        text: uebersetzbar("Nach dem Ablaufen einer Fährte siehst du das Ergebnis sofort: Abweichung, Anteil auf der Fährte, gefundene Gegenstände und die Karte. Einen Kommentar schreibst du gleich dazu, mit „Fertig“ oder Wischen nach unten schließt du das Fenster. Der Ablauf steht danach wie gewohnt im Tagebuch."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Das Profil ist jetzt eine übersichtliche Liste und passt auf einen Bildschirm. Jede Zeile führt zu einer eigenen Seite: Konto (Name, E-Mail und Passwort ändern), Darstellung & Sprache (Hell oder Dunkel, Schriftgröße, Sprache), Funktionen, die Sportarten, die du trainierst, und Daten & Konto. Rechts in der Zeile siehst du gleich, was gerade eingestellt ist. „Abmelden“ steht ganz unten."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Länderauswahl für Prüfungsordnungen zeigt Dogity nur noch, wenn es mehr als ein Land mit Prüfungsordnungen gibt. Was du bisher gewählt hast, bleibt gespeichert."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("„Sachkunde üben“ öffnet jetzt in der App, mit der unteren Leiste, statt auf einer eigenen Seite. Öffnest du angemeldet einen Sachkunde-Link, landest du ebenfalls dort."),
      },
      {
        art: "neu",
        text: uebersetzbar("Neu bei Dogity? Auf der Startseite fragt dich jetzt eine Karte „Wie heißt dein Hund?“: Name eingeben, Rüde oder Hündin wählen, „Weiter“ - und gleich öffnet sich das Fenster, in dem du das erste Training einträgst. Rasse und Geburtstag trägst du später über „Bearbeiten“ im Menü der Hundeseite nach."),
      },
      {
        art: "neu",
        text: uebersetzbar("Hast du dein erstes Training gespeichert, fragt die Startseite einmal „Gut gemacht! Wie geht es weiter?“: Du kannst ein Prüfungsziel setzen oder einem Verein beitreten - oder die Karte ausblenden."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Legst du auf der Seite „Hunde“ einen Hund an, kannst du gleich „Erstes Training eintragen“ oder mit „Zum Hund“ auf seine Seite wechseln. Das Geschlecht ist nirgends mehr vorausgewählt: Du wählst Rüde oder Hündin selbst, damit nichts versehentlich falsch gespeichert wird."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Statistik zeigt, solange du noch keinen Hund angelegt hast, einen kurzen Hinweis mit Weg zur Startseite statt leerer Karten."),
      },
      {
        art: "neu",
        text: uebersetzbar("Nimmt eine Trainer:in deine Anfrage an einen Verein an, kann sie dich gleich in eine Gruppe einladen - du musst nicht mehr zweimal auf eine Freigabe warten. Die Einladung steht dann direkt oben auf deiner Startseite: Mit „Annehmen“ wirst du Mitglied der Gruppe, mit „Ablehnen“ nicht. Aufgenommen wirst du also nur, wenn du selbst zustimmst."),
      },
      {
        art: "neu",
        text: uebersetzbar("Trainer:innen sehen bei jeder Beitrittsanfrage unter „In Gruppe einladen:“ die Gruppen des Vereins. Gibt es nur eine, ist sie schon gewählt; mit „Keine“ nimmst du nur in den Verein auf."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Trainer:innen eines Vereins sahen bei ihrem eigenen Verein den Knopf „Beitreten“. Auf der Seite „Vereine“ steht dort jetzt „Trainer:in“. Außerdem ist die Seite jetzt vollständig übersetzt."),
      },
    ],
  },
  {
    version: "0.22",
    datum: "2026-10-08",
    titel: uebersetzbar("Die Startseite zeigt, was heute dran ist"),
    aenderungen: [
      {
        art: "verbessert",
        text: uebersetzbar("Die Startseite ist kompakter: Oben steht der nächste Gruppentermin (nur wenn in den nächsten sieben Tagen einer ansteht) mit „Ich komme“ und „Kann nicht“, dann das Wichtigste zum Erfassen von Training und Fährte in einer Karte. Zu jedem Hund gibt es nur noch eine Karte mit Prüfung, Woche und den offenen Übungen - der Block „Diese Woche“ ist darin aufgegangen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Auf Trainer-Feedback antwortest du jetzt gleich auf der Startseite mit „Danke“, „Verstanden“ oder einer Rückfrage."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Fehlt noch das Ergebnis einer Prüfung, trägst du es mit einem Tipp auf die Zeile direkt auf der Startseite ein. Hunde ohne Prüfungsziel und der Hinweis zum Vereinsbeitritt sind nur noch eine schmale Zeile, und Trainer:innen sehen offene Beitrittsanfragen und Trainings zum Bewerten gleich dort."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Neuerungen zeigt Dogity jetzt als kleinen Punkt am Reiter „Profil“, bis du sie dir angesehen hast - nicht mehr als Karte auf der Startseite."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Trainierst du an einem Tag, an dem schon eine Fährte eingetragen ist, behält das Training jetzt seine Uhrzeit und seinen Ort - und bekommt damit auch das Wetter."),
      },
      {
        art: "neu",
        text: uebersetzbar("Unter „Trainings bewerten“ musst du als Trainer:in nicht mehr zu jedem Training etwas schreiben: Mit „Fertig“ hakst du ein Training ab, mit „Passt so“ übernimmst du die Selbsteinschätzung der Hundeführer:in als deine Bewertung. Auch wenn alle Übungen bewertet sind, gilt das Training als erledigt - die Zahl „Zu erledigen“ kann dadurch wieder auf null kommen."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Hat die Trainer:in ein Training abgehakt, steht auf der Hundeseite nicht mehr „Noch kein Trainer-Feedback“."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Hundeseite ist aufgeräumt: Oben stehen „Training erfassen“ und „Fährte legen“, darunter zwei Reiter - „Plan“ mit deinen Zielen und „Tagebuch“ mit deinen Trainingstagen. Eine Zeile zeigt, wo du im Plan stehst und ob neues Feedback wartet. Wer mehrere Hunde hat, wechselt oben mit einem Tipp zwischen ihnen. Bearbeiten, Drucken, Mitbesitzer, Archivieren und Löschen findest du hinter den drei Punkten oben rechts, „Fährte legen“ öffnet ein eigenes Fenster."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Im Tagebuch ist jeder Trainingstag eine Zeile mit Datum, Dauer und einem Kurzbild. Ein Tipp klappt genau diesen Tag auf - mit Karte, Übungen, Notizen und Feedback. Mit den Filtern über der Liste siehst du nur Fährten oder nur Tage mit Feedback."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Der Plan auf der Hundeseite ist ruhiger: Du siehst die laufende Woche mit Fortschrittsbalken und deinen Übungen, ein Tipp auf eine Übung öffnet gleich den Eintrag. Alles zum Umbauen - Trainingstage, Neu generieren, Übungen ändern, hinzufügen oder entfernen - steht erst hinter „Plan bearbeiten“."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Was das Ziel selbst betrifft, findest du hinter den drei Punkten an der Plan-Karte unter „Ziel verwalten“: Anpassen, Übungen gewichten, „Ergebnis eintragen“ (früher „Ziel abschließen“) und „Ziel beenden ohne Ergebnis“ (früher „Abbrechen“)."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Eine Übung aus dem Plan zu entfernen und ein Ziel zu beenden ging bisher mit einem Tipp und ohne Rückfrage. Jetzt fragt Dogity vorher nach."),
      },
    ],
  },
  {
    version: "0.21",
    datum: "2026-10-02",
    titel: uebersetzbar("Schneller zum Training, klarere Anmeldung"),
    aenderungen: [
      {
        art: "verbessert",
        text: uebersetzbar("Auf der Startseite stehen „Training erfassen“ und „Fährte legen“ jetzt gleich unter der Begrüßung - auch mit mehreren Hunden musst du nicht mehr nach unten scrollen."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Wer noch keinen Hund hat, bekommt unter „Meine Hunde“ gleich das offene Formular zum Anlegen. Der Hinweis zum Installieren der App liegt nicht mehr über der unteren Leiste, lässt sich bequem wegtippen und kommt dann nicht wieder."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Bei der Registrierung, beim Zurücksetzen und beim Ändern des Passworts steht jetzt unter dem Feld, was ein Passwort braucht. Fehlermeldungen dazu sind auf Deutsch."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("„Passwort vergessen“ sagt jetzt ehrlich, wie es weitergeht: Der Betreiber wird informiert, setzt das Passwort zurück und meldet sich bei dir."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Bei der Fährte heißt es jetzt „Fährte legen“ und „Legen starten“ - „aufnehmen“ meint im Fährtensport das Suchen des Hundes."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Ist der Prüfungstermin eines Ziels vorbei, zeigt die Startseite keine Wochenübungen und keinen Wochenfortschritt mehr dafür - nur noch „Ergebnis eintragen“."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Das Feld „Erfolgreich“ beim Eintragen einer Übung ist größer und lässt sich auf dem Handy leichter antippen. Die Benachrichtigung über Trainer-Feedback nennt jetzt den Hund, und bei den Zusagen steht korrekt „1 kommt“."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Auf der Karte „Nächster Termin“ steht jetzt, wie viele angemeldet sind, und „Anwesenheit“ führt direkt zum Abhaken. „Zum Kalender“ ist dort ein kleines Symbol, damit die Knöpfe auf dem Handy in einer Zeile bleiben. Öffnest du eine Gruppe mit Anmeldungen, beginnt sie bei den Anmeldungen. Die Zeile „0 kommen · 0 können nicht · 0 offen“ erscheint nicht mehr, solange noch niemand geantwortet hat."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Bei „Trainings bewerten“ zählen nur noch Trainings der letzten acht Wochen. Ältere stehen nicht mehr als offene Aufgabe da."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("In der Terminplanung steht „Neuer Termin“ neben dem Titel, und die Filter erscheinen erst, wenn der Verein mehr als eine Gruppe hat. Eine neue wöchentliche Serie startet mit dem Wochentag des Beginns und läuft zwölf Wochen. Ergibt der Zeitraum keinen Termin, steht der Grund da und „Serie anlegen“ ist gesperrt. Beim Termin heißt es „Einheit übernehmen…“, und in den Werkzeugen „Einheiten und Bausteine“."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Beitrittsanfragen an den Verein hast du jetzt mit den Knöpfen „Annehmen“ und „Ablehnen“ unter dem Namen. Vor dem Ablehnen fragt die App nach. „Zum Trainer machen“ sehen nur noch die, die den Verein verwalten, und auch das fragt vorher nach."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Beim Abhaken der Anwesenheit stehen nur „Heute“ und vergangene Termine zur Wahl, das Feld für einen anderen Tag folgt direkt dahinter. In der Anmeldeliste steht statt „offen“ jetzt „nicht bezahlt“, der Filter heißt „Unbezahlt“."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Besser lesbar im hellen Modus und draußen: Ränder von Eingabefeldern, Knöpfen und Auswahlfeldern sowie der Rahmen beim Antippen sind deutlicher, Rot ist kräftiger. Beim Aufzeichnen sind GPS-Werte und Beschriftungen der Marker-Knöpfe auch in der Sonne gut zu lesen."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Abgesagte Termine sind jetzt gut lesbar: der Titel ist durchgestrichen und „Abgesagt“ steht deutlich in Rot, statt dass der ganze Termin verblasst. Sterne bei Bewertungen und leere Fortschrittsbalken heben sich besser ab."),
      },
    ],
  },
  {
    version: "0.20",
    datum: "2026-10-01",
    titel: uebersetzbar("Dein Verein lädt dich per QR-Code ein"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Vereine können jetzt einen Einladungslink mit QR-Code erstellen. Wer ihn öffnet, landet direkt bei der Beitrittsanfrage, und der Verein gibt jede Anfrage weiterhin selbst frei. Du findest die Karte „Neue Mitglieder einladen“ in der Trainer-Übersicht unter „Verein“."),
      },
      {
        art: "neu",
        text: uebersetzbar("Für das Vereinsheim gibt es einen Aushang zum Ausdrucken: großer QR-Code, drei kurze Schritte und der Link als Text. Der Link lässt sich jederzeit ersetzen oder abschalten."),
      },
      {
        art: "neu",
        text: uebersetzbar("Trainer:innen eines Vereins bekommen jetzt eine Benachrichtigung, sobald jemand beitreten möchte, und sehen in der Liste, wer über den Einladungslink gekommen ist."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Wer sich über „Kostenlos starten“ auf der Seite einer Prüfungsordnung registriert, findet beim ersten Ziel die passende Sportart und Prüfung schon vorgewählt."),
      },
      {
        art: "neu",
        text: uebersetzbar("Ist die Prüfung gelaufen, trägst du jetzt das Ergebnis ein: Prüfungstag, Punkte und eine Notiz. „Ziel abschließen“ ersetzt das bisherige „Als erreicht markieren“. Hast du nicht bestanden, bleibt das Ziel aktiv, du wählst einen neuen Termin, und der Plan reicht bis dorthin."),
      },
      {
        art: "neu",
        text: uebersetzbar("Nach einer bestandenen Prüfung schlägt Dogity die nächste Stufe vor, zum Beispiel IGP 2 nach IGP 1. Ein Tipp legt das Ziel mit der passenden Prüfung an, das Datum wählst du selbst. Auch auf der Seite jeder Prüfungsordnung steht jetzt, welche Stufe als Nächstes kommt."),
      },
      {
        art: "neu",
        text: uebersetzbar("Am Hund gibt es die Karte „Leistungen“ mit allen bestandenen Prüfungen, neueste zuerst. Sie steht auch in der Druckansicht. Ziele, die du früher ohne Ergebnis als erreicht markiert hast, kannst du dort nachtragen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Die Startseite zeigt dir je Hund das nächste Prüfungsziel mit Datum, den Tagen bis dahin und dem Fortschritt der Woche. Hunde ohne Ziel bekommen einen Hinweis, wie du eines setzt."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die drei Karten am Ende der Startseite („Meine Hunde“, „Sportarten“, „Sachkunde üben“) sind zu einer schmalen Zeile mit Links geworden."),
      },
      {
        art: "neu",
        text: uebersetzbar("Zu Gruppenterminen kannst du jetzt auf der Startseite zusagen: „Ich komme“ oder „Kann nicht“, jederzeit änderbar. Die anderen Mitglieder sehen nur, wie viele kommen; die Trainer:innen sehen auch die Namen. Sagst du ab, erfährt es die Trainer:in. Fällt ein Termin aus, bekommen alle Bescheid, die zugesagt hatten."),
      },
      {
        art: "neu",
        text: uebersetzbar("Mit „Zum Kalender“ legst du einen Gruppentermin in den Kalender deines Geräts. Übertragen werden nur Gruppe, Zeit, Ort und Dauer, keine Namen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Trainer:innen sehen ganz oben in der Trainer-Übersicht den nächsten Termin: wer kommt, wer nicht und wer noch nicht geantwortet hat. Von dort geht es direkt zur Planung oder in den Kalender. Gruppentraining und Terminplanung stehen jetzt direkt darunter, und in der Terminplanung steht die Zählung bei jedem Termin."),
      },
      {
        art: "neu",
        text: uebersetzbar("Eine Benachrichtigung über Trainer-Feedback öffnet jetzt den Eintrag im Tagebuch, klappt seinen Monat auf und hebt ihn kurz hervor. Neues Feedback trägt die Markierung „Neu“, bis du es gesehen hast."),
      },
      {
        art: "neu",
        text: uebersetzbar("Auf Feedback kannst du antworten: mit „Danke“, „Verstanden“ oder einer kurzen Rückfrage. Deine Trainer:in bekommt eine Benachrichtigung und sieht deine Antwort unter ihrem Feedback. Schreibt sie neues Feedback, beginnt die Antwort von vorn."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Glocke aktualisiert sich jetzt auch, sobald du in die App zurückkehrst, statt bis zu einer Minute zu warten."),
      },
      {
        art: "neu",
        text: uebersetzbar("Jede Fährte lässt sich jetzt als Bild teilen: Im Tagebuch tippst du bei der Fährte auf „Als Bild teilen“. Das Bild zeigt die Form der Fährte, deine Linie beim Ablaufen in Ampelfarben und die wichtigsten Zahlen. Es enthält keine Karte und keine Koordinaten, niemand sieht also, wo die Fährte lag. Den Hundenamen kannst du ausblenden. Das Bild entsteht auf deinem Gerät und wird nirgends hochgeladen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Am Hund zeigt jetzt ein Verlauf, wie sich die Fährtenarbeit entwickelt: die letzten Abläufe als Säulen mit dem Anteil auf der Fährte, eingefärbt nach Abweichung. Er erscheint, sobald drei Abläufe ausgewertet sind."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Auf der Trainer-Seite stand der Pfeil bei „Gruppentraining“ und „Terminplanung“ auf schmalen Bildschirmen unter dem Text. Jetzt steht er rechts daneben."),
      },
      {
        art: "neu",
        text: uebersetzbar("Gruppen können jetzt ein eigenes Anmeldeformular mit QR-Code anbieten, zum Beispiel für die Welpengruppe. Wer sich anmeldet, braucht kein Dogity-Konto: Name, Rufname und Rasse des Hundes, Wurftag und Telefonnummer genügen. Link und QR-Code findest du auf der Seite der Gruppe, dort gibt es auch einen Aushang zum Ausdrucken. Über eine neue Anmeldung informiert die Glocke die Trainer:innen der Gruppe."),
      },
      {
        art: "neu",
        text: uebersetzbar("Trainer:innen haken je Termin ab, wer da war, und sehen, wer bezahlt hat. Du wählst den Tag (heute, einen Termin aus der Terminplanung oder ein anderes Datum) und tippst die Teilnehmenden an, jeder Tipp wird sofort gespeichert. Bei jeder Anmeldung steht, wie oft sie schon da war, und „offen“ oder „bezahlt“ lässt sich mit einem Tipp umschalten."),
      },
      {
        art: "neu",
        text: uebersetzbar("Bisherige Anmeldungen aus einem Google-Formular lassen sich als CSV-Datei übernehmen. Vorher siehst du, wie viele Einträge neu, schon vorhanden oder fehlerhaft sind. Anmeldungen kannst du außerdem von Hand hinzufügen, bearbeiten und löschen."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Anmeldungen zu einer Gruppe werden automatisch gelöscht, wenn ein Jahr lang nichts mehr damit passiert ist, und mit der Gruppe selbst. Das steht auch in der Datenschutzerklärung."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Auf schmalen Bildschirmen ragten einige Knöpfe aus ihrer Karte und waren abgeschnitten, zum Beispiel „Bearbeiten“ im Profil und „+ Übung“ im Katalog. Die englische Oberfläche ist vollständiger: Trainer-Bereich, Terminplanung, Fährten im Tagebuch und Profil sind übersetzt, Datum und Zahlen folgen der gewählten Sprache. Im Trainingsplan steht keine namenlose Zeile mehr in Pausenwochen, die Druckansicht zeigt den Plan Woche für Woche, und der Plankopf nennt neben dem Zielwert auch, wie viele Übungen diese Woche wirklich geplant sind."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Beim Anmelden eines Welpen schlägt das Feld „Wurftag“ beim ersten Antippen den Tag vor acht Wochen vor - der Kalender geht dort auf, statt beim heutigen Tag."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Trainer-Übersicht ist aufgeräumt: Oben steht „Zu erledigen“ mit den offenen Beitrittsanfragen und den Trainings zum Bewerten, darunter „Meine Gruppen“ mit den Gruppen als kompakte Liste. Bewerten, Anfragen und alles rund um den Verein (Einladungslink, Mitglieder, Katalog) haben jetzt eigene Seiten, und von den betreuten Hunden siehst du zuerst drei."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Gruppenseite ist aufgeräumt: Oben stehen Mitglieder und Anmeldungen als zwei Ansichten zum Umschalten, bei der Welpengruppe zuerst das Abhaken der Anwesenheit. Anmeldelink und QR-Code öffnen sich auf Tipp, und ein Mitglied lädst du über einen eigenen Knopf ein. Name, Trainer:innen und „Gruppe auflösen“ liegen auf einer eigenen Einstellungsseite (Zahnrad oben rechts). Ein Mitglied zu entfernen fragt jetzt vorher nach, und die Benachrichtigung über eine neue Anmeldung öffnet gleich die Anmeldungen."),
      },
    ],
  },
  {
    version: "0.19",
    datum: "2026-09-28",
    titel: uebersetzbar("Mitbesitz nur noch mit Zustimmung"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Wer einen Hund mit dir teilen will, lädt dich jetzt ein - Mitbesitzer:in wirst du erst, wenn du unter „Meine Hunde“ annimmst. Bis dahin sieht die andere Person nur die E-Mail-Adresse, die sie eingegeben hat, nicht deinen Namen. Bestehende Mitbesitzer:innen bleiben, wie sie sind."),
      },
      {
        art: "neu",
        text: uebersetzbar("Deinen eigenen Mitbesitz an einem Hund kannst du jetzt selbst beenden. Wirst du von anderen entfernt, bekommst du eine Benachrichtigung."),
      },
      {
        art: "neu",
        text: uebersetzbar("Weitere Trainer:innen einer Gruppe werden jetzt eingeladen und nehmen unter „Vereine“ an, statt sofort eingetragen zu werden. Bei allen Einladungen sieht die Gruppe bis zur Zusage nur die eingegebene E-Mail-Adresse."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Kommentare und Notizen haben jetzt eine sichtbare Höchstlänge, statt beim Speichern mit einem unverständlichen Fehler zu scheitern. Ein offline erfasster Eintrag mit zu langem Text hält die Übertragung der übrigen nicht mehr auf."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Tippt jemand fünfmal ein falsches Passwort zu deiner E-Mail-Adresse, wirst du nicht mehr auf allen Geräten abgemeldet. Und die Anmeldung unterscheidet jetzt zwischen „zu viele Fehlversuche, bitte kurz warten“ und einer echten Sperre."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Nach dem Abmelden bleiben keine Hunde, Trainings und Statistiken mehr auf dem Gerät. Wer sich danach am selben Gerät anmeldet, sieht nichts mehr von dir. Offline Erfasstes wartet auf deine nächste Anmeldung und geht nur mit ihr hinaus."),
      },
    ],
  },
  {
    version: "0.18",
    datum: "2026-09-21",
    titel: uebersetzbar("Eine abgebrochene Fährte ist nicht mehr verloren"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Jeder Punkt einer Aufzeichnung wird sofort auf deinem Handy gesichert. Bricht sie ab - weil die Seite neu lädt oder das Handy die App beendet -, kannst du danach weiter aufzeichnen, speichern, was schon da ist, oder sie verwerfen. Das gilt beim Legen wie beim Ablaufen."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("„Legen beenden“ und „Ablauf beenden“ hältst du jetzt eine Sekunde gedrückt. Eine zufällige Berührung beim Herausziehen des Handys aus der Tasche beendet die Aufzeichnung nicht mehr, und Wischen nach unten lädt die Seite nicht neu."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Konnte eine Aufzeichnung beim Beenden nicht gespeichert werden, waren ihre Punkte weg. Jetzt bleibt sie gesichert, bis das Speichern klappt."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Läuft deine Anmeldung während einer Aufzeichnung ab, geht es erst nach dem Beenden zur Anmeldeseite - nicht mehr mitten auf der Fährte."),
      },
    ],
  },
  {
    version: "0.17",
    datum: "2026-09-21",
    titel: uebersetzbar("In eine Gruppe nur mit deiner Zusage"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Trainer:innen laden neue Mitglieder jetzt ein, statt sie direkt aufzunehmen. Mitglied wirst du erst, wenn du unter „Vereine“ annimmst - und erst dann kann jemand aus der Gruppe deine Hunde betreuen und ihr Tagebuch sehen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Unter „Vereine“ siehst du alle deine Gruppen und kannst jede davon verlassen. Damit endet auch die Betreuung deiner Hunde durch die Trainer:innen dieser Gruppe."),
      },
      {
        art: "neu",
        text: uebersetzbar("Du bekommst eine Benachrichtigung, sobald jemand einen deiner Hunde betreut."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Wer sein Passwort ändert oder zurücksetzt, meldet damit alle anderen Geräte ab."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Bei viel Betrieb konnte die App dich beim stündlichen Erneuern der Anmeldung abmelden, obwohl alles in Ordnung war. Das passiert nicht mehr."),
      },
    ],
  },
  {
    version: "0.16",
    datum: "2026-09-13",
    titel: uebersetzbar("Impressum, Datenschutz und deine Daten in deiner Hand"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Dogity hat ein Impressum und eine Datenschutzerklärung. Dort steht, welche Daten gespeichert werden, warum, wie lange - und wer sie außer dir sieht."),
      },
      {
        art: "neu",
        text: uebersetzbar("Im Profil kannst du unter „Deine Daten“ alles herunterladen, was Dogity über dich gespeichert hat: Konto, Hunde, Trainings, Fährten samt Punkten, Ziele, Verein und Lernfortschritt."),
      },
      {
        art: "neu",
        text: uebersetzbar("Ebenfalls im Profil: „Konto löschen“. Das entfernt dein Konto und deine Daten wirklich - Hunde, die du dir mit jemandem teilst, bleiben bei der anderen Person."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Beim Löschen eines Kontos blieben bisher Hunde, Trainings, Fährten und Einstellungen in der Datenbank stehen. Jetzt werden sie mit entfernt."),
      },
    ],
  },
  {
    version: "0.15",
    datum: "2026-09-10",
    titel: uebersetzbar("Weniger Tippen, weniger Scrollen"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Die Startseite führt direkter ans Ziel: Wer mehrere Hunde hat, tippt beim Training erfassen gleich den Hund an, und „Fährte legen“ hat eine eigene Kachel. Beides landet direkt an der richtigen Stelle der Hundeseite."),
      },
      {
        art: "neu",
        text: uebersetzbar("„Diese Woche“ auf der Startseite zeigt die offenen Wochenziele aus dem Trainingsplan - eintragen lässt sich jedes gleich dort."),
      },
      {
        art: "neu",
        text: uebersetzbar("Eine heute gelegte Fährte, die noch nicht abgelaufen ist, steht mit ihrem Fährtenalter auf der Startseite und lässt sich von dort ablaufen. Direkt nach dem Legen bietet die App das Ablaufen ebenfalls an."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Im Trainingsformular steht „Training speichern“ fest am unteren Rand. Kommentar, Bewertungskriterien und Notiz öffnen sich erst auf Wunsch - das Formular ist deutlich kürzer."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Hundeseite hat oben Sprungknöpfe zu Training, Fährte und Trainingsplan. Auf der Trainerseite stehen offene Beitrittsanfragen und die zu bewertenden Trainings jetzt oben, und „Neue Gruppe“ öffnet sich erst auf Knopfdruck."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die untere Leiste ist aufgeräumt: Home, Hunde, Statistiken und Profil - dazu Trainer und Admin, wo es passt. Sportarten und der eigene Verein sind im Profil zu finden."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Startseite lädt schneller und erscheint in einem Zug, statt Abschnitt für Abschnitt nachzurutschen. Wer über die Leiste zurück auf Home tippt, sieht sofort den letzten Stand."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Stufen „Einsteiger“, „Fortgeschritten“ und „Erfahren“ stehen nicht mehr an den Übungen - weder im Prüfungskatalog noch in der Übungsauswahl oder der Druckansicht. Bei Prüfungsübungen führten sie eher in die Irre."),
      },
      {
        art: "behoben",
        text: uebersetzbar("„Wie beim letzten Mal“ greift nach einer gelegten Fährte auf die letzte Einheit mit Übungen zurück. Vorher bezog es sich auf die Fährte und hatte nichts zu übernehmen."),
      },
    ],
  },
  {
    version: "0.14",
    datum: "2026-09-09",
    titel: uebersetzbar("Das Tagebuch ist übersichtlicher, der Dark Mode besser lesbar"),
    aenderungen: [
      {
        art: "verbessert",
        text: uebersetzbar("Das Trainingstagebuch ist neu gestaltet: Trainingstage haben eine eigene Kopfzeile, Kommentar, Übungen, Fährte und Trainer-Feedback sind als eigene Blöcke beschriftet und abgesetzt. Vorher stand alles in derselben Schrift und derselben Farbe untereinander."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Der Dark Mode ist neu abgestimmt: ein ruhiges, leicht bläuliches Grau statt dunkelblau getöntem Schwarz, und Karten heben sich heller von der Seite ab. Knöpfe bleiben kräftig blau, Links und Symbole erscheinen in einem helleren Blau - beides ist jetzt deutlich besser lesbar."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die gelegte Fährte wird auf der Karte nicht mehr grün gezeichnet, sondern kräftig magenta mit dunklem Rand - gut zu sehen auf Straßenkarte, Luftbild und im Dark Mode. Grün, Gelb und Rot bleiben damit allein der Abweichung des Ablaufs vorbehalten; beides war vorher im selben Grün. Eine Legende unter der Karte benennt jetzt, welche Linie was ist."),
      },
      {
        art: "behoben",
        text: uebersetzbar("„Wie beim letzten Mal\" zeigte in der Übungsauswahl eine Kennnummer statt des Übungsnamens, wenn man es antippte, bevor die Übungslisten geladen waren. Jetzt steht dort der Name, sobald die Listen da sind - und bis dahin der Platzhalter."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Mehrere Fährten an einem Tag gehören jetzt zu derselben Trainingseinheit. Bisher legte jede Aufnahme eine eigene an - „Ort, Zeit & Verfassung“ und der Kommentar standen dann doppelt da. Im Tagebuch stehen alle Fährten eines Tages jetzt in einem Block, nummeriert und jede mit eigener Karte."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Den Untergrund einer Fährte wählt man jetzt durch Antippen - Wiese, Acker, Stoppelfeld, Wald, Feldweg oder Sand, bei einem Untergrundwechsel auch mehrere. Auf dem iPhone erschien beim Legen mit dem Telefon in der Tasche sonst immer wieder „Eingabe widerrufen“: iOS bot damit an, das getippte Feld rückgängig zu machen."),
      },
    ],
  },
  {
    version: "0.13",
    datum: "2026-09-08",
    titel: uebersetzbar("Die Schrift lässt sich größer stellen"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Im Profil lässt sich die Schriftgröße in drei Stufen wählen. Sie gilt für die ganze App, und Knöpfe und Abstände wachsen mit - nicht nur die Buchstaben."),
      },
      {
        art: "neu",
        text: uebersetzbar("Die Einstellung gehört zum Konto und gilt damit auf jedem Gerät. Wer im Browser bereits eine größere Schrift eingestellt hat, behält diesen Vorsprung."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Formularfelder folgen jetzt der eingestellten Schriftgröße. Sie waren zuvor auf dem Telefon fest auf eine Größe eingestellt - ausgerechnet dort, wo man genau hinsehen muss."),
      },
    ],
  },
  {
    version: "0.12",
    datum: "2026-09-08",
    titel: uebersetzbar("Training eintragen geht jetzt schnell"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("„Wie beim letzten Mal\" übernimmt den Übungssatz der letzten Einheit. Zu tun bleibt, was sich wirklich unterscheidet: die Bewertungen. Sie werden bewusst nicht mitübernommen - an ihnen erkennt der Trainingsplan die Schwächen."),
      },
      {
        art: "neu",
        text: uebersetzbar("„Training erfassen\" steht jetzt auf der Startseite. Wer nur einen Hund führt, landet damit direkt im geöffneten Formular."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Sportart wird in die nächste Übung übernommen, statt jedes Mal neu abgefragt zu werden. Führt der Hund nur eine, steht sie von vornherein drin."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Dauer lässt sich antippen: 30, 45, 60 oder 90 Minuten. Das Zahlenfeld bleibt für alles andere."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Der Trainingsplan zeigt nur noch die laufende Woche. Die übrigen sind einen Knopfdruck entfernt - und das Tagebuch rückt deutlich weiter nach oben."),
      },
    ],
  },
  {
    version: "0.11",
    datum: "2026-09-07",
    titel: uebersetzbar("Alle Sachkundefragen zum Nachlesen"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Unter dem Fragentrainer stehen jetzt alle Fragen des Katalogs am Stück, mit Lösung. Wer den Stoff am Abend vor der Prüfung einmal durchlesen will, muss sich nicht mehr Runde für Runde durchtippen."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Sachkundeseiten lassen sich jetzt über Suchmaschinen finden. Bisher wurden die Fragen erst im Browser nachgeladen - wer nach ihnen suchte, fand eine leere Seite."),
      },
    ],
  },
  {
    version: "0.10",
    datum: "2026-09-03",
    titel: uebersetzbar("Englisch - und der Prüfungskatalog bekommt einen Geltungsbereich"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Die Oberfläche lässt sich auf Englisch umstellen. Prüfungsordnungen und Sachkundefragen bleiben dabei deutsch - eine übersetzte Prüfungsfrage wäre für die Prüfung wertlos."),
      },
      {
        art: "neu",
        text: uebersetzbar("Im Profil lässt sich wählen, in welchem Land die Prüfungsordnungen gelten. Inhalte gibt es bisher nur für Deutschland; andere Länder sind wählbar und noch leer. Tagebuch, Fährte und Trainingsplanung funktionieren davon unabhängig vollständig."),
      },
      {
        art: "neu",
        text: uebersetzbar("Sprache und Land werden getrennt gewählt. Wer in Deutschland trainiert und die App auf Englisch nutzt, behält die deutschen Prüfungsordnungen - die BH bleibt die BH."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Sachkunde erscheint nur noch im deutschen Geltungsbereich. Sie ist der Fragenkatalog des SWHV und hilft anderswo auch auf Deutsch nicht weiter."),
      },
    ],
  },
  {
    version: "0.9",
    datum: "2026-09-03",
    titel: uebersetzbar("Fährte im Vollbild, und Vereine verwalten sich selbst"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Fährten werden im Vollbild aufgezeichnet: große Karte, große Knöpfe für Gegenstand, Leckerlipot und eigene Markierungen. Der Bildschirm bleibt dabei an, solange die Aufzeichnung läuft."),
      },
      {
        art: "neu",
        text: uebersetzbar("Die Karte dreht sich in Laufrichtung mit. Wer den Kompass antippt, wechselt zwischen Laufrichtung, Norden und der Ausrichtung des Geräts."),
      },
      {
        art: "neu",
        text: uebersetzbar("Umschalter zwischen Straßenkarte und Luftbild, dazu ein dunkler Kartenmodus, der sich nach der Einstellung der App richtet."),
      },
      {
        art: "neu",
        text: uebersetzbar("Beim Fährtelegen werden Start- und Endzeit festgehalten, nicht mehr nur der Beginn. Die Liegezeit ergibt sich daraus von selbst."),
      },
      {
        art: "neu",
        text: uebersetzbar("Vereine verwalten sich selbst: Es gibt die Rollen Training und Verwaltung, der Verein lässt sich umbenennen und Mitglieder können zu Trainer:innen berufen werden - ohne Umweg über die globale Verwaltung."),
      },
      {
        art: "neu",
        text: uebersetzbar("Einen Verein gründen: Der Antrag wird gestellt, geprüft und freigegeben oder mit Begründung abgelehnt. Wer gründet, führt den Verein anschließend selbst."),
      },
      {
        art: "neu",
        text: uebersetzbar("Funktionen und Sportarten lassen sich im Profil ab- und anwählen - je Nutzer und je Hund. Wer keine Fährte läuft, bekommt sie auch nicht mehr angeboten. Voreingestellt ist weiterhin alles sichtbar."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Die Karte zeigte beim Start der Aufzeichnung ganz Deutschland und zoomte erst nach etlichen Sekunden auf den eigenen Standort."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Ein Klick auf den Kompass machte die Karte schwarz."),
      },
      {
        art: "behoben",
        text: uebersetzbar("In der Mitgliederliste des Vereins wurde auch Trainer:innen noch angeboten, sie zu Trainer:innen zu machen. Lange E-Mail-Adressen schoben die Knöpfe aus der Zeile."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Navigationsleiste läuft auf schmalen Handys nicht mehr über, auch nicht mit sieben Einträgen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Diese Seite. Unter „Neuerungen“ steht ab jetzt, was sich wann geändert hat und welche Fassung gerade läuft - zu finden über die Fußzeile, die Startseite und das Profil."),
      },
    ],
  },
  {
    version: "0.8",
    datum: "2026-09-02",
    titel: uebersetzbar("Verfassung des Hundes, geführter Erststart und spürbar weniger Wartezeit"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Die Verfassung des Hundes am Trainingstag lässt sich festhalten - wie fit, wie aufnahmefähig er war. Das erklärt später manche Bewertung, die sonst rätselhaft bliebe."),
      },
      {
        art: "neu",
        text: uebersetzbar("Ein geführter Erststart auf dem Dashboard: Hund anlegen, erstes Training erfassen, Ziel setzen - Schritt für Schritt statt vor einer leeren Seite."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die App startet deutlich schneller. Hundebilder werden nur noch übertragen, wenn sie sich geändert haben, und der Server stellt beim Start ein Drittel der bisherigen Datenbankabfragen."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Trainer:innen gehören jetzt regulär zum Verein. Wer zuvor nur einer Gruppe zugeordnet war, wurde übernommen."),
      },
    ],
  },
  {
    version: "0.7",
    datum: "2026-09-01",
    titel: uebersetzbar("Sachkunde üben wie beim Führerschein"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Der Fragentrainer zur Sachkunde der BH/VT: Frage für Frage mit sofortiger Auflösung, Fehlerspeicher und Wiedervorlage. Falsch beantwortete Fragen kommen wieder, bis sie sitzen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Sachkunde-Fragen lassen sich in der Verwaltung überarbeiten, ohne dass die nächste Aktualisierung des Katalogs die Korrektur wieder überschreibt."),
      },
      {
        art: "behoben",
        text: uebersetzbar("Zuordnungsfragen ließen sich nur aufdecken, nicht lösen. Bildantworten zeigten ihre Nummer nicht. Der Lernstand blieb leer."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Austritt aus Verein und Gruppe räumt sauber auf, und eine Wiederaufnahme ist danach wieder möglich."),
      },
    ],
  },
  {
    version: "0.6",
    datum: "2026-08-31",
    titel: uebersetzbar("Der Trainingsplan gehört den Trainer:innen"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Trainer:innen führen den Trainingsplan ihrer Gruppe. Der automatische Generator hält sich heraus, sobald jemand von Hand plant."),
      },
      {
        art: "neu",
        text: uebersetzbar("Ein Training lässt sich nachträglich korrigieren, statt es löschen und neu anlegen zu müssen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Eine Gruppe kann mehrere Trainer:innen haben, die sich die Arbeit teilen."),
      },
      {
        art: "neu",
        text: uebersetzbar("Turnierhundsport im Katalog: CaniCross-Disziplinen, Sprint-Vierkampf und die Vorprüfungen."),
      },
      {
        art: "behoben",
        text: uebersetzbar("In BH und IBGH hieß die Übung Leinenführigkeit statt Fußarbeit, und die Freifolge fehlte in der BH. Die Übungen kommen jetzt in der Reihenfolge der Prüfungsordnung."),
      },
    ],
  },
  {
    version: "0.5",
    datum: "2026-08-23",
    titel: uebersetzbar("Der Hund bekommt ein Gesicht"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Profilbild und Geburtsdatum beim Hund - und daraus überall sein Alter."),
      },
      {
        art: "neu",
        text: uebersetzbar("Turnierhundsport und Agility im Katalog der Prüfungsordnungen, Abteilung A sauber aufgeteilt."),
      },
    ],
  },
  {
    version: "0.4",
    datum: "2026-08-20",
    titel: uebersetzbar("Prüfungsordnungen öffentlich einsehbar"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("31 Prüfungsordnungen sind ohne Konto einsehbar - mit ihren Übungen und Punkten, jede auf einer eigenen Seite."),
      },
      {
        art: "neu",
        text: uebersetzbar("Das Datum eines Trainingstags lässt sich ändern, wenn ein Training später nachgetragen wird."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die hinterlegten Prüfungsordnungen wurden gegen die FCI-Prüfungsordnung 2025 gegengelesen und korrigiert."),
      },
    ],
  },
  {
    version: "0.3",
    datum: "2026-08-14",
    titel: uebersetzbar("Wetter ohne eine einzige Eingabe"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Die Temperatur wird beim Legen und beim Suchen der Fährte automatisch erfasst, samt Änderung dazwischen - genau die bestimmt maßgeblich, wie sich die Geruchsspur hält."),
      },
      {
        art: "verbessert",
        text: uebersetzbar("Die Ortssuche findet Hundeplätze und Trainingsgelände, nicht mehr nur Ortsnamen."),
      },
    ],
  },
  {
    version: "0.2",
    datum: "2026-08-13",
    titel: uebersetzbar("Fährten auswerten statt nur aufzeichnen"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Gelegte Fährte und Ablauf des Hundes lassen sich übereinanderlegen. Die Auswertung zeigt die Abweichung, gefundene Gegenstände und Stockungen - und unterscheidet dabei, ob der Hund verweist oder wirklich sucht."),
      },
    ],
  },
  {
    version: "0.1",
    datum: "2026-08-10",
    titel: uebersetzbar("Ein Trainingsplan, der mitdenkt"),
    aenderungen: [
      {
        art: "neu",
        text: uebersetzbar("Aus Prüfungstermin und Ziel entsteht ein Wochenplan, der schwache Übungen häufiger einplant und sitzende seltener."),
      },
      {
        art: "neu",
        text: uebersetzbar("Mit dem Regler „Übungen gewichten“ lässt sich von Hand nachsteuern, was mehr oder weniger geübt werden soll."),
      },
    ],
  },
];

/** Die Fassung, die in diesem Build steckt. Siehe Kopfkommentar. */
export const AKTUELLE_VERSION = VERSIONSHINWEISE[0].version;

/** Datum dieser Fassung als ISO-Datum. */
export const AKTUELLE_VERSION_DATUM = VERSIONSHINWEISE[0].datum;

/**
 * "3. September 2026" - ausgeschrieben, weil 03.09.2026 sich schlechter liest.
 *
 * Die Uhrzeit 12:00 UTC ist kein Füllwert: Ein reines "2026-09-03" liest
 * JavaScript als Mitternacht UTC, und daraus wird in jeder westlich von
 * Greenwich gelegenen Zeitzone der 2. September. Mittags als Anker liegt von
 * UTC-11 bis UTC+11 sicher im selben Tag. Die feste Zeitzone hält zusätzlich
 * Server-Rendering (Container läuft unter UTC) und Hydration im Browser auf
 * derselben Zeichenkette.
 */
export function formatiereVeroeffentlichung(isoDatum: string, sprache: string = "de"): string {
  return new Date(`${isoDatum}T12:00:00Z`).toLocaleDateString(sprache === "en" ? "en-GB" : "de-DE", {
    day: "numeric",
    month: "long",
    year: "numeric",
    timeZone: "Europe/Berlin",
  });
}

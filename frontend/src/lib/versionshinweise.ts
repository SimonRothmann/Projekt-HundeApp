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

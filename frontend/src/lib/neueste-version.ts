import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Die jüngste Fassung allein, samt den kleinen Bausteinen, die der Auszug
 * "Letzte Neuerung" braucht (Startseite, Profil).
 *
 * Eine eigene Datei, weil die vollständige Liste in versionshinweise.ts rund
 * 90 KB Quelltext (22 KB gepackt) groß ist und sonst mit der öffentlichen
 * Startseite ausgeliefert würde, nur damit sie ihren obersten Eintrag zeigen
 * kann. Dafür steht dieser Eintrag jetzt zweimal im Quelltext -
 * versionshinweise.test.ts hält ihn mit dem obersten Eintrag der Liste
 * gleich. Bei einer neuen Fassung also beides tauschen: den Eintrag oben in
 * VERSIONSHINWEISE einfügen und NEUESTE_VERSION durch denselben Eintrag
 * ersetzen (dazu die Nummer in aktuelle-version.ts); ein Test schlägt an,
 * wenn eines davon fehlt.
 *
 * Die Typen, AENDERUNGSART_LABEL und formatiereVeroeffentlichung wohnen hier,
 * damit der Auszug nichts aus versionshinweise.ts laden muss;
 * versionshinweise.ts gibt sie unverändert weiter.
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

/** Der oberste Eintrag von VERSIONSHINWEISE (der Test hält beide gleich). */
export const NEUESTE_VERSION: Versionshinweis = {
  version: "1.4",
  datum: "2026-11-16",
  titel: uebersetzbar("Ruhigere Seiten"),
  aenderungen: [
    {
      art: "verbessert",
      text: uebersetzbar("Weitere Seiten sind ruhiger: Im Plan stehen abgeschlossene Ziele zugeklappt unter den laufenden, die Woche und ihre Übungen sind eine Liste statt Kästen in Kästen. Bei der Gesundheit hat jeder Bereich eine Karte, Bearbeiten und Löschen liegen hinter den drei Punkten. Die Hundeliste ist eine Liste, die Fährtenbalken in der Statistik sind wie im Tagebuch gefärbt und mit Prozent beschriftet, das Wochendiagramm hat eine Zeitachse, und beim Laden erscheinen graue Platzhalter statt „Lädt…“."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Auf der Hundeseite steht das Foto deines Hundes jetzt groß neben dem Namen, der Wechsel zu deinen anderen Hunden läuft über runde Bilder darüber, und „Fährte legen“ und „Merken“ stehen ohne Rahmen unter „Training erfassen“."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Die Seiten für Trainer:innen sind ruhiger: Gruppe, Termine, Verein, Prüfungen und Arbeitseinsätze zeigen je Thema eine Karte oder Liste statt Kästen in Kästen, seltene Aktionen wie Entfernen, Löschen und Absagen liegen im ⋯-Menü, und Formulare zum Einladen klappen erst nach einem Tipp auf."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Der Trainingstag im Tagebuch und die Fährte sind aufgeräumt: weniger Rahmen, bei der Fährte steht die Karte jetzt zuerst. Datum ändern, Dauer ändern, Löschen und Verlassen findest du hinter dem Knopf mit den drei Punkten."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Eine Gruppe, in der du Mitglied bist, steht bei den Vereinen nur noch einmal. Einen eigenen Verein beantragst du über einen Link, das Formular klappt erst dann auf."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Auf der Startseite steht das Fährtenalter ab einer Stunde in Stunden und Minuten statt in über tausend Minuten."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Auf der Startseite steht die wartende Fährte jetzt oben und hervorgehoben, mit dem Fährtenalter groß und einem Knopf zum Ablaufen. Fällige Gesundheitstermine stehen gesammelt in einer Karte weiter oben, „Erfassen“ ist bei mehreren Hunden eine Zeile je Hund, Trainer-Feedback ist zugeklappt, bis du es öffnest, und das Ziel zeigt die nächsten zwei Übungen."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Auf der Fährtenkarte hat jede Art Punkt jetzt eine eigene Form: Gegenstand als Quadrat, Leckerlipot als Kreis, Verleitung als Dreieck, Marker als Raute. Die Legende unter der Karte zeigt, welche davon auf der Fährte vorkommen."),
    },
    {
      art: "neu",
      text: uebersetzbar("Neue Einstellung „Farbsichere Darstellung“ unter Profil → Darstellung & Sprache: Für Rot-Grün-Schwäche zeigen die Ampeln auf Karte, Auswertung und Verlauf Blau, Gelb und Rot statt Grün, Orange und Rot. Sie gilt für dein Konto auf jedem Gerät."),
    },
  ],
};

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

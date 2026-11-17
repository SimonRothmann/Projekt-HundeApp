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
  version: "1.6",
  datum: "2026-11-17",
  titel: uebersetzbar("Weniger tippen, sicher in der Tasche"),
  aenderungen: [
    {
      art: "neu",
      text: uebersetzbar("Beim Fährtelegen und Ablaufen sperrt sich die Bedienung nach 15 Sekunden ohne Berührung und sobald der Bildschirm ausgeht - in der Tasche löst so nichts mehr aus. Entsperren durch Wischen über den Schieber, sperren jederzeit mit dem Schloss oben. Tippst du „Beenden“ nur kurz an, steht der Hinweis jetzt im Knopf statt als Meldung darüber."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Nach dem Speichern einer Fährte stehen nur noch „Jetzt ablaufen“ und „Fertig“ da; eine weitere Fährte startest du bewusst über „Weitere Fährte legen“. Den Kommentar zum Ablauf schreibst du im Ergebnis-Fenster."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Training eintragen: Die Vorschläge springen beim Laden nicht mehr, das X fragt nach, bevor ein Entwurf verloren geht, und die Verfassung des Hundes steht direkt unter „Wie lief es?“."),
    },
    {
      art: "neu",
      text: uebersetzbar("Im Tagebuch kannst du nach Übung, Notiz und Ort suchen, ein Trainingstag hat „Übung hinzufügen“, und die Filter „Fährten“ und „Mit Feedback“ zeigen die Tage gleich aufgeklappt."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Termine: „Zum Kalender“ bleibt nach der Zusage stehen, der Ort auch. Liegt der nächste Termin weiter als eine Woche entfernt, zeigt die Startseite eine schmale Zeile, über die du schon zusagen kannst."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Hundeseite: Einen weiteren Hund legst du über „+“ oder „Verwalten“ an, „Prüfungsziel setzen“ öffnet gleich das Formular mit Schnellwahl fürs Datum, die Trainingspause findest du auch unter „Gesundheit“, und „Statistik“ führt direkt zur Auswertung des Hundes."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Für Trainer:innen: „Vorbereiten“ öffnet den Termin direkt, das Terminformular hat größere Tippflächen und eine feste Speichern-Leiste, beim Bewerten speichert „Speichern & fertig“ in einem Schritt, und die Vereinsseite hat eine Sprungleiste."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Rückfragen wie „Wirklich löschen?“ erscheinen in der App statt als Browser-Fenster, und die Begriffe sind einheitlich: „Training eintragen“, „Trainer:in“, „Fürs nächste Training merken“. „Sachkunde üben“ findest du jetzt auch im Profil."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Die Startseite lädt schneller und zeigt sofort den zuletzt gespeicherten Stand, und auch der erste Aufruf nach einem Update ist schneller."),
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

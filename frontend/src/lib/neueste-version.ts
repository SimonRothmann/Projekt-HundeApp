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
  version: "1.9",
  datum: "2026-11-23",
  titel: uebersetzbar("Leichter Einstieg"),
  aenderungen: [
    {
      art: "neu",
      text: uebersetzbar("Hund angelegt, aber noch nicht trainiert? Die Startseite zeigt jetzt „Erstes Training eintragen“ mit passenden Übungen – und schlägt die Einstiegsprüfung als Ziel in 12 Wochen vor, mit einem Tipp angelegt."),
    },
    {
      art: "neu",
      text: uebersetzbar("Drei Tage nach dem ersten Hund erinnert Dogity einmal ans erste Training, falls noch keins eingetragen ist – als Mitteilung in der Glocke und per Push."),
    },
    {
      art: "neu",
      text: uebersetzbar("Fährte und Trail: Einen falsch gesetzten Marker kannst du nach dem Speichern wieder löschen – unter „Einstellungen der Fährte“. Ein schon gelaufener Ablauf wird dann neu ausgewertet."),
    },
    {
      art: "behoben",
      text: uebersetzbar("Training eintragen ohne Netz: Die Vorschläge „Diese Woche“ und „Zuletzt geübt“ sind jetzt auch offline da."),
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

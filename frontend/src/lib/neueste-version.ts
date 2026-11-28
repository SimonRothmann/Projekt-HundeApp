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
  /** Optional: der ganze Punkt führt auf diese Adresse (neuer Tab). */
  link?: string;
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
  version: "1.11",
  datum: "2026-11-30",
  titel: uebersetzbar("Jagd und Schweißfährte"),
  aenderungen: [
    {
      art: "neu",
      text: uebersetzbar("Neue Sportart Jagd mit den Prüfungen VJP, HZP, VGP, VSwP und VFsP."),
    },
    {
      art: "neu",
      text: uebersetzbar("Schweißfährte als dritte Spurart neben Fährte und Trail: mit Wundbett und Verweiserpunkt, Verfahren (Tupf, Tropf, Fährtenschuh), Wildart, Schweißmenge und Stehzeit – dazu Leitfäden für die Prüfungsfährten in Metern."),
    },
    {
      art: "neu",
      text: uebersetzbar("Die Startseite zeigt, ab wann die Stehzeit erreicht ist, und Dogity erinnert dann daran – und noch einmal 6 Stunden, bevor die Schweißfährte nach 72 Stunden abläuft."),
    },
    {
      art: "neu",
      text: uebersetzbar("Arbeiten wie auf der Prüfung: verdeckt mit einer Anschussfläche von 30 × 30 m, Rückrufe live und in der Auswertung, Betten verwiesen oder überlaufen, zum Stück gekommen mit Totverbellen oder Totverweisen. Der Anschuss gilt als gefunden, sobald ihr bis 15 m an der gelegten Fährte seid – auch wenn das GPS ihn im Wald verfehlt."),
    },
    {
      art: "neu",
      text: uebersetzbar("Wetter über die Stehzeit: Bei Schweißfährte und Trail zeigen die Details Regen und Temperaturspanne zwischen Legen und Ablauf."),
    },
    {
      art: "verbessert",
      text: uebersetzbar("Eine Sportfährte lässt sich jetzt bis 4 Stunden nach dem Legen ablaufen statt bis 24 Stunden – die längste Liegezeit im Leitfaden sind 3 Stunden. Danach verschwindet sie von der Startseite."),
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

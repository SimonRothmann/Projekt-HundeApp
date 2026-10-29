import { ortsformat } from "@/lib/ortsformat";
import { uebersetzbar, type Sprache } from "@/lib/i18n/sprachen";
import type { AdminFeatureUsage } from "@/lib/types";

/**
 * Reine Hilfen für die Nutzungsstatistik im Adminbereich - ohne Bildschirm
 * prüfbar. Die Zahlen kommen fertig vom Server (siehe AdminUsageService).
 */

/**
 * Die Namen der Funktionen. Der Server liefert nur den stabilen Schlüssel;
 * ein Schlüssel, den es hier noch nicht gibt, erscheint unverändert (so fällt
 * eine neue Funktion auf, statt zu verschwinden).
 */
const FUNKTIONS_NAMEN: Record<string, string> = {
  training: uebersetzbar("Training eintragen"),
  "exercise-rating": uebersetzbar("Übungen eingetragen"),
  "track-laid": uebersetzbar("Fährte legen"),
  "track-walk": uebersetzbar("Fährte ablaufen"),
  goal: uebersetzbar("Prüfungsziel"),
  plan: uebersetzbar("Trainingsplan"),
  pause: uebersetzbar("Pausen"),
  health: uebersetzbar("Gesundheit"),
  "sachkunde-learn": uebersetzbar("Sachkunde lernen"),
  "sachkunde-exam": uebersetzbar("Sachkunde-Prüfung"),
  "dog-photo": uebersetzbar("Hundefoto"),
  "co-owner": uebersetzbar("Mitbesitz"),
  "trainer-care": uebersetzbar("Trainer-Betreuung"),
  "trainer-feedback": uebersetzbar("Rückmeldung zum Training"),
  "group-session": uebersetzbar("Gruppentraining-Termine"),
  "session-response": uebersetzbar("Zu- und Absagen zu Terminen"),
  "group-registration": uebersetzbar("Gruppenanmeldungen"),
  "club-membership": uebersetzbar("Vereinsbeitritt"),
  push: uebersetzbar("Push aktiviert"),
};

/** Der (noch zu übersetzende) deutsche Name einer Funktion; unbekannte Schlüssel bleiben, wie sie sind. */
export function funktionsName(key: string): string {
  return FUNKTIONS_NAMEN[key] ?? key;
}

export type NutzungStatus = "aktiv" | "ruhend" | "nie";

/**
 * Ob eine Funktion in den letzten 30 Tagen jemand genutzt hat ("aktiv"), nur
 * früher einmal ("ruhend") oder noch nie ("nie").
 */
export function nutzungStatus(f: Pick<AdminFeatureUsage, "accountsLast30Days" | "accountsEver">): NutzungStatus {
  if (f.accountsLast30Days > 0) return "aktiv";
  return f.accountsEver > 0 ? "ruhend" : "nie";
}

/**
 * Die meistgenutzten Funktionen zuerst (Konten der letzten 30 Tage), bei
 * Gleichstand nach Einträgen, dann nach Konten insgesamt. Die ungenutzten
 * landen so von selbst unten, die nie genutzten ganz am Ende. Verändert die
 * Liste nicht.
 */
export function sortiereFunktionen(liste: readonly AdminFeatureUsage[]): AdminFeatureUsage[] {
  return [...liste].sort(
    (a, b) =>
      b.accountsLast30Days - a.accountsLast30Days ||
      (b.entriesLast30Days ?? 0) - (a.entriesLast30Days ?? 0) ||
      b.accountsEver - a.accountsEver ||
      a.key.localeCompare(b.key),
  );
}

/** Der größte Wert einer Reihe (mindestens 0) - der Bezug für die Balken. */
export function groessterWert(werte: readonly number[]): number {
  return Math.max(0, ...werte);
}

/** Kurzer Wochentag ("Mo", "Mon") für Index 0 = Montag bis 6 = Sonntag. */
export function wochentagKurz(index: number, sprache: Sprache): string {
  // Der 1.1.2024 war ein Montag.
  return new Date(2024, 0, 1 + index).toLocaleDateString(ortsformat(sprache), { weekday: "short" });
}

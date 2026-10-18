import type { GpsTrack, TrainingSession } from "@/lib/types";

/**
 * Das Wetter einer Fährte oder eines Trainings trägt der Server NACH dem
 * Speichern im Hintergrund nach (Open-Meteo, meist nach ein bis zwei
 * Sekunden). Wer direkt danach hinschaut, sieht es noch nicht - die Seite lädt
 * deshalb EINMAL nach, kurz nachdem etwas gespeichert wurde und solange das
 * Wetter noch fehlt. Kein Dauer-Polling: Kommt es dann nicht, hat der
 * Wetterdienst nicht geliefert (die Fährte hat dafür den Knopf "Wetter laden").
 */

/** So lange nach dem Speichern wird nachgeladen (der Server fragt mit sechs Sekunden Zeitlimit). */
export const WETTER_NACHLADEN_NACH_MS = 5000;
/** Danach gilt das Speichern nicht mehr als frisch - ein später geöffneter Tag lädt nicht nach. */
export const WETTER_FRISCH_MS = 20_000;

/** Wann zuletzt etwas gespeichert wurde, das ein Wetter nach sich zieht (Epoch-ms; 0 = nie). */
let zuletztGespeichert = 0;

/**
 * Pfade, deren Speichern ein Wetter anfordert: Fährte (auch Ablauf) und Training
 * (anlegen, Ort/Zeit ändern, Tag verschieben). Über den Pfad statt an jeder
 * Aufrufstelle, damit auch die Offline-Warteschlange beim Hochladen dazugehört.
 */
const WETTER_PFAD = /^\/api\/(gps-tracks|trainings)(\/|\?|$)/;

/** Vom API-Helfer nach jedem erfolgreichen Schreibzugriff aufgerufen. */
export function schreibzugriffMelden(methode: string | undefined, pfad: string, jetzt: number = Date.now()): void {
  const art = (methode ?? "GET").toUpperCase();
  if (art === "GET" || art === "DELETE") return;
  if (WETTER_PFAD.test(pfad)) zuletztGespeichert = jetzt;
}

/**
 * In wie vielen Millisekunden die Seite nachladen soll - null, wenn das
 * Speichern zu lange her ist (oder nie stattfand). Mindestens eine Sekunde, damit
 * ein Aufruf kurz vor Ablauf nicht sofort wieder dieselbe Antwort holt.
 */
export function wetterNachladenIn(gespeichertUm: number, jetzt: number = Date.now()): number | null {
  if (gespeichertUm <= 0) return null;
  const vergangen = jetzt - gespeichertUm;
  if (vergangen < 0 || vergangen > WETTER_FRISCH_MS) return null;
  return Math.max(1000, WETTER_NACHLADEN_NACH_MS - vergangen);
}

/** Wann zuletzt etwas gespeichert wurde, das ein Wetter nach sich zieht. */
export function zuletztGespeichertUm(): number {
  return zuletztGespeichert;
}

/** Nur für Tests: den Merker zurücksetzen. */
export function wetterMerkerZuruecksetzen(): void {
  zuletztGespeichert = 0;
}

/** Training mit Ort und Uhrzeit, aber ohne Temperatur: Das Wetter steht noch aus. */
export function trainingWetterOffen(
  einheit: Pick<TrainingSession, "startTime" | "latitude" | "longitude" | "temperatureC">,
): boolean {
  // Lose Prüfung (== null): ältere Stände aus dem Lesecache kennen die Felder nicht.
  return !!einheit.startTime && einheit.latitude != null && einheit.longitude != null && einheit.temperatureC == null;
}

/**
 * Fährte, bei der noch ein Wetter aussteht: beim Legen (gelegte Spur, aber keine
 * Temperatur) oder beim Suchen (Ablauf gespeichert, aber keine Temperatur dazu).
 * Das Suchen-Wetter kommt erst Sekunden nach dem Ablauf - die Änderung zum Legen
 * ist der fachlich wichtigste Wert.
 */
export function faehrteWetterOffen(
  spur: Pick<GpsTrack, "points" | "walkRuns" | "laidTemperatureC" | "searchTemperatureC">,
): boolean {
  // Manuelle Marker (Typ 1) zählen nicht - der Server fragt nach dem ersten automatischen Punkt.
  const gelegt = spur.points.some((p) => p.pointType !== 1);
  if (!gelegt) return false;
  if (spur.laidTemperatureC == null) return true;
  // Das Suchen-Wetter fragt der Server nach dem ersten Punkt eines Ablaufs.
  return spur.searchTemperatureC == null && (spur.walkRuns ?? []).some((lauf) => lauf.points.length > 0);
}

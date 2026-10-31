/**
 * Rechnen mit Kalendertagen ("2026-10-03") - die EINE Stelle dafür.
 *
 * Ein Kalendertag hat keine Uhrzeit und keine Zeitzone. Gerechnet wird deshalb
 * mit Tagesnummern in UTC und nie mit `new Date("2026-10-03")` (UTC-Mitternacht,
 * westlich von Greenwich der Vortag) und nie mit lokaler Zeit (Sommerzeit
 * verschöbe den Tag). Nur `heuteIso` fragt die Ortszeit des Geräts: "Heute" ist
 * der Tag, den der Mensch am Gerät gerade hat.
 *
 * Eingaben dürfen auch ein Zeitstempel mit Tagesanfang sein ("2026-10-03T00:00:00"):
 * Es zählen nur die ersten zehn Zeichen.
 */

const TAG_MS = 24 * 60 * 60 * 1000;

const VEREINSZEIT = new Intl.DateTimeFormat("en-US", { timeZone: "Europe/Berlin", year: "numeric", month: "2-digit", day: "2-digit" });

/**
 * Der Kalendertag eines Zeitpunkts in der Vereinszeit (Europe/Berlin) - der Tag, in dem der Server
 * rechnet (Vereinszeit.Tag). Für Regeln, die Frontend und Server gleich auslegen müssen (Planwoche);
 * was die Person am Gerät als "heute" sieht, bleibt `heuteIso`.
 */
export function vereinszeitTag(zeitpunkt: number): string {
  const teile = VEREINSZEIT.formatToParts(zeitpunkt);
  const wert = (art: string) => teile.find((t) => t.type === art)?.value ?? "";
  return `${wert("year")}-${wert("month")}-${wert("day")}`;
}

/** Jahr, Monat (1-12) und Tag eines Kalendertags. */
export function tagTeile(iso: string): [number, number, number] {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  return [jahr, monat, tag];
}

/** Der heutige Kalendertag auf dem Gerät als "JJJJ-MM-TT". */
export function heuteIso(jetzt: number = Date.now()): string {
  const d = new Date(jetzt);
  const zweistellig = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${zweistellig(d.getMonth() + 1)}-${zweistellig(d.getDate())}`;
}

/**
 * Ein Kalendertag als Date zum Formatieren: Mitternacht Ortszeit. Für die Anzeige mit
 * toLocaleDateString - `new Date("2026-10-03")` wäre UTC-Mitternacht und zeigte westlich von
 * Greenwich den 2. Oktober. Nur anzeigen, nicht rechnen (dafür plusTage, tageBis).
 */
export function tagAlsDatum(iso: string): Date {
  const [jahr, monat, tag] = tagTeile(iso);
  return new Date(jahr, monat - 1, tag);
}

/** Anzahl ganzer Tage seit 1970 für einen Kalendertag - unabhängig von Zeitzone und Sommerzeit. */
export function tagesnummer(iso: string): number {
  const [jahr, monat, tag] = tagTeile(iso);
  return Math.round(Date.UTC(jahr, monat - 1, tag) / TAG_MS);
}

/** Tage von `heute` bis zum Datum: positiv = noch hin, 0 = heute, negativ = vorbei. */
export function tageBis(iso: string, heute: string = heuteIso()): number {
  return tagesnummer(iso) - tagesnummer(heute);
}

/** Der Kalendertag n Tage später (oder früher) als "JJJJ-MM-TT". */
export function plusTage(iso: string, tage: number): string {
  return new Date((tagesnummer(iso) + tage) * TAG_MS).toISOString().slice(0, 10);
}

/** Der Kalendertag n Monate später (der 31. + 1 Monat: der letzte Tag des Folgemonats). */
export function plusMonate(iso: string, monate: number): string {
  const [jahr, monat, tag] = tagTeile(iso);
  const ziel = new Date(Date.UTC(jahr, monat - 1 + monate, tag));
  if (ziel.getUTCMonth() !== (((monat - 1 + monate) % 12) + 12) % 12) ziel.setUTCDate(0);
  return ziel.toISOString().slice(0, 10);
}

/** Der Kalendertag n Jahre später (am 29.2. des Schaltjahres: der 28., wie DateOnly.AddYears im Backend). */
export function plusJahre(iso: string, jahre: number): string {
  const [jahr, monat, tag] = tagTeile(iso);
  const ziel = new Date(Date.UTC(jahr + jahre, monat - 1, tag));
  // Ein Überlauf (29.2. -> 1.3.) fiele in den nächsten Monat: zurück auf den letzten Tag.
  if (ziel.getUTCMonth() !== monat - 1) ziel.setUTCDate(0);
  return ziel.toISOString().slice(0, 10);
}

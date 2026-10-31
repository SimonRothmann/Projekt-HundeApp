/**
 * Vorbelegung der "Wöchentlichen Serie" im Termin-Formular.
 *
 * Reine Kalenderrechnung auf ISO-Daten ("2026-10-06"), gerechnet in
 * lib/kalendertag.ts: Ein Datum ohne Uhrzeit hat keine Zeitzone.
 */

import { plusTage } from "@/lib/kalendertag";

/** So viele Wochen läuft eine neue Serie vorbelegt - ein Quartal, das übliche Kursende. */
export const SERIE_STANDARD_WOCHEN = 12;

function tag(iso: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!m) return null;
  const d = new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])));
  return Number.isNaN(d.getTime()) ? null : d;
}

/** Das Datum "von" plus die Standard-Laufzeit, als ISO-Datum; bei ungültiger Eingabe unverändert. */
export function serieStandardEnde(von: string): string {
  return tag(von) ? plusTage(von, SERIE_STANDARD_WOCHEN * 7) : von;
}

/** Wochentag eines ISO-Datums wie Date.getDay() (0 = Sonntag); null bei ungültiger Eingabe. */
export function wochentagVon(iso: string): number | null {
  return tag(iso)?.getUTCDay() ?? null;
}

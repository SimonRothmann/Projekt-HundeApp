import { automatischePunkte, haversineMeters } from "@/lib/geo";
import { istLuecke } from "@/lib/ablauf-luecken";

/**
 * Der Rückruf bei der Schweißfährte (Paket SF2). Kommt der Hund von der Fährte ab und verbessert sich nicht
 * selbst, teilen die Richter das mit; mehr als zwei Rückrufe heißt nicht bestanden. Hier dieselbe Regel wie im
 * Backend (GpsTrackEvaluator.Rueckrufe), live auf dem Gerät und in der Auswertung dort:
 *
 * - Abgekommen ist das Gespann, solange es weiter als RUECKRUF_ABWEICHUNG_METER von der gelegten Linie weg ist.
 * - Legt es abgekommen insgesamt mehr als RUECKRUF_WEG_METER Weg zurück (Summe der Teilstrecken, die abgekommen
 *   enden), zählt ein Rückruf. Der nächste zählt erst nach einer Rückkehr auf die Fährte.
 * - Erst ab dem Finden des Anschusses (erster Ablaufpunkt höchstens ANSCHUSS_RADIUS_METER von der gelegten Linie); Lückenstücke
 *   (kein GPS, siehe lib/ablauf-luecken.ts) zählen nicht als Weg.
 *
 * Fortlaufend rechnend (ein Punkt nach dem anderen), damit die Aufzeichnung nicht bei jedem Punkt den ganzen
 * Weg neu misst. Gerechnet wird auf den rohen Punkten; das Backend vereinfacht eine sehr lange Legung vorher.
 */

/** Gelb-Grenze der Schweißfährte (SCHWEISS_GRENZEN.gelbBis); Backend: GpsTrackEvaluator.RueckrufAbweichungMeters. */
export const RUECKRUF_ABWEICHUNG_METER = 30;
/** Backend: GpsTrackEvaluator.RueckrufWegMeters. */
export const RUECKRUF_WEG_METER = 80;
/** Höchstens so viele Meter von der gelegten Linie gilt der Anschuss als gefunden (halbe Kante der Anschussfläche); Backend: GpsTrackEvaluator.AnschussRadiusMeters. */
export const ANSCHUSS_RADIUS_METER = 15;
/** Mehr Rückrufe als so viele: nicht bestanden. */
export const MAX_RUECKRUFE = 2;

type Koordinate = { latitude: number; longitude: number };
type Ablaufpunkt = Koordinate & { timestamp: string };

export type RueckrufZustand = {
  /** Wie viele Ablaufpunkte schon verrechnet sind. */
  verarbeitet: number;
  anschussGefunden: boolean;
  weg: number;
  /** Ob ein weiterer Rückruf zählen darf (nach der Rückkehr auf die Fährte). */
  bereit: boolean;
  vorher: Ablaufpunkt | null;
  stellen: Koordinate[];
};

export const NEUER_RUECKRUF_ZUSTAND: RueckrufZustand = {
  verarbeitet: 0,
  anschussGefunden: false,
  weg: 0,
  bereit: true,
  vorher: null,
  stellen: [],
};

/** Abstand eines Punkts zur Linie (Polylinie) in Metern, über die Teilstrecken in einer lokalen Ebene. */
function abstandZurLinie(p: Koordinate, linie: readonly Koordinate[]): number {
  if (linie.length === 1) return haversineMeters(p, linie[0]);
  const bezug = p.latitude;
  const ebene = (k: Koordinate) => ({
    x: ((k.longitude * Math.PI) / 180) * Math.cos((bezug * Math.PI) / 180) * 6371000,
    y: ((k.latitude * Math.PI) / 180) * 6371000,
  });
  const q = ebene(p);
  let min = Infinity;
  for (let i = 0; i < linie.length - 1; i++) {
    const a = ebene(linie[i]);
    const b = ebene(linie[i + 1]);
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const laenge2 = dx * dx + dy * dy;
    const t = laenge2 === 0 ? 0 : Math.min(1, Math.max(0, ((q.x - a.x) * dx + (q.y - a.y) * dy) / laenge2));
    min = Math.min(min, Math.hypot(q.x - (a.x + t * dx), q.y - (a.y + t * dy)));
  }
  return min;
}

/**
 * Verrechnet alle Ablaufpunkte nach `zustand.verarbeitet` und gibt den neuen Stand zurück. Weniger Punkte als
 * schon verrechnet (neue Aufzeichnung) beginnt von vorn. `linie`: die automatischen Punkte der Legung in
 * zeitlicher Reihenfolge.
 */
export function rueckrufFortsetzen(zustand: RueckrufZustand, linie: readonly Koordinate[], punkte: readonly Ablaufpunkt[]): RueckrufZustand {
  let z = punkte.length < zustand.verarbeitet ? NEUER_RUECKRUF_ZUSTAND : zustand;
  if (linie.length === 0 || punkte.length === z.verarbeitet) return { ...z, verarbeitet: punkte.length };

  const stellen = [...z.stellen];
  let { anschussGefunden, weg, bereit, vorher } = z;
  for (let i = z.verarbeitet; i < punkte.length; i++) {
    const p = punkte[i];
    if (!anschussGefunden) {
      anschussGefunden = abstandZurLinie(p, linie) <= ANSCHUSS_RADIUS_METER;
    } else if (abstandZurLinie(p, linie) <= RUECKRUF_ABWEICHUNG_METER) {
      weg = 0;
      bereit = true;
    } else if (vorher && !istLuecke(vorher, p)) {
      weg += haversineMeters(vorher, p);
      if (bereit && weg > RUECKRUF_WEG_METER) {
        stellen.push({ latitude: p.latitude, longitude: p.longitude });
        bereit = false;
      }
    }
    vorher = p;
  }
  z = { verarbeitet: punkte.length, anschussGefunden, weg, bereit, vorher, stellen };
  return z;
}

/** Die automatischen Punkte einer Legung, nach Zeit geordnet - die Linie, an der gemessen wird. */
export function rueckrufLinie<T extends Koordinate & { timestamp: string; pointType?: number }>(legung: readonly T[]): T[] {
  return automatischePunkte(legung).sort((a, b) => a.timestamp.localeCompare(b.timestamp));
}

/** Anzahl und Stellen aller Rückrufe eines Ablaufs auf einmal (für Tests und Auswertung ohne Server-Feld). */
export function rueckrufe(linie: readonly Koordinate[], punkte: readonly Ablaufpunkt[]): Koordinate[] {
  return rueckrufFortsetzen(NEUER_RUECKRUF_ZUSTAND, linie, punkte).stellen;
}

/** Mehr als MAX_RUECKRUFE Rückrufe: nicht bestanden. */
export function mitRueckrufenDurchgefallen(anzahl: number | null | undefined): boolean {
  return (anzahl ?? 0) > MAX_RUECKRUFE;
}

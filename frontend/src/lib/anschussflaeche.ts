/**
 * Die Anschussfläche beim verdeckten Arbeiten der Schweißfährte (Paket SF2): Auf der Prüfung sucht das
 * Gespann den Anschuss selbst in einer Fläche von etwa 30 × 30 m. Die Karte zeigt deshalb statt des Startpunkts
 * ein Quadrat, dessen Mitte absichtlich neben dem Anschuss liegt (Versatz bis 10 m), damit es ihn nicht verrät.
 * Der Versatz folgt aus der Kennung der Fährte und ist daher bei jedem Neuladen derselbe.
 */

export const ANSCHUSS_FLAECHE_KANTE_METER = 30;
export const ANSCHUSS_VERSATZ_MAX_METER = 10;

type Koordinate = { latitude: number; longitude: number };

/** FNV-1a über die Zeichen, als Zahl von 0 bis 2^32 - 1. */
function streuwert(text: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  return h >>> 0;
}

/** Versatz in Metern (Ost, Nord), je Achse zwischen -10 und 10, stabil je Kennung. */
export function anschussVersatz(kennung: string): { ost: number; nord: number } {
  const a = streuwert(kennung);
  const b = streuwert(`${kennung}#nord`);
  const meter = (w: number) => (w / 0xffffffff) * 2 * ANSCHUSS_VERSATZ_MAX_METER - ANSCHUSS_VERSATZ_MAX_METER;
  return { ost: meter(a), nord: meter(b) };
}

/**
 * Die vier Ecken des Quadrats (geschlossener Ring als [lon, lat]), das den Anschuss enthält - Mitte um den
 * Versatz verschoben. Die Kante bleibt 30 m, der Anschuss liegt immer innen (Versatz höchstens 10 m < 15 m).
 */
export function anschussFlaeche(anschuss: Koordinate, kennung: string): [number, number][] {
  const { ost, nord } = anschussVersatz(kennung);
  const meterJeGrad = (6371000 * Math.PI) / 180;
  const mitteLat = anschuss.latitude + nord / meterJeGrad;
  const mitteLon = anschuss.longitude + ost / (meterJeGrad * Math.cos((anschuss.latitude * Math.PI) / 180));
  const halb = ANSCHUSS_FLAECHE_KANTE_METER / 2;
  const dLat = halb / meterJeGrad;
  const dLon = halb / (meterJeGrad * Math.cos((anschuss.latitude * Math.PI) / 180));
  const sw: [number, number] = [mitteLon - dLon, mitteLat - dLat];
  const so: [number, number] = [mitteLon + dLon, mitteLat - dLat];
  const no: [number, number] = [mitteLon + dLon, mitteLat + dLat];
  const nw: [number, number] = [mitteLon - dLon, mitteLat + dLat];
  return [sw, so, no, nw, sw];
}

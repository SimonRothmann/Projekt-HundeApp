import { bearingDegrees, haversineMeters, automatischePunkte } from "@/lib/geo";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import type { GpsPoint, GpsTrack, GpsWalkRunCorner, GpsWalkRunLeg } from "@/lib/types";

/**
 * Windrichtung und Wind je Schenkel (Paket F4).
 *
 * Die Richtung folgt der Konvention des Wetterdienstes: die Richtung, AUS der der Wind kommt (270 = Wind
 * aus Westen), in Grad von Nord im Uhrzeigersinn. Alle Winkel hier sind solche Kompassgrade.
 */

/** Ab dieser Windstärke (km/h) sagt die Richtung etwas über einen Schenkel; darunter ist es Flaute. */
export const WIND_MIN_KMH = 5;

/** Die acht Himmelsrichtungen, ab Nord im Uhrzeigersinn. */
const WINDROSE = [
  uebersetzbar("N"),
  uebersetzbar("NO"),
  uebersetzbar("O"),
  uebersetzbar("SO"),
  uebersetzbar("S"),
  uebersetzbar("SW"),
  uebersetzbar("W"),
  uebersetzbar("NW"),
] as const;

/** Auf [0, 360) gebracht - auch für negative Winkel und Vielfache von 360. */
export function normalisiere(grad: number): number {
  return ((grad % 360) + 360) % 360;
}

/** Die Himmelsrichtung zu einem Kompasswinkel: Sektoren von je 45 Grad, Nord reicht von 337,5 bis 22,5. */
export function himmelsrichtung(grad: number): (typeof WINDROSE)[number] {
  return WINDROSE[Math.round(normalisiere(grad) / 45) % 8];
}

/**
 * Ob zu einer Windstärke eine Richtung gezeigt wird: bei Windstille (auf km/h gerundet 0) gibt es keine,
 * auch wenn der Dienst eine Zahl mitliefert.
 */
export function zeigtWindrichtung(grad: number | null | undefined, kmh: number | null | undefined): grad is number {
  return grad != null && kmh != null && Math.round(kmh) > 0;
}

export type WindZuSchenkel = "rueckenwind" | "gegenwind" | "seitenwind-links" | "seitenwind-rechts";

/**
 * Wie der Wind auf einen Schenkel trifft. `peilung`: Laufrichtung des Schenkels (Anfang nach Ende);
 * `windAus`: Richtung, aus der der Wind kommt. Sektoren je 90 Grad: Weht der Wind aus ±45 Grad der
 * Laufrichtung, ist es Gegenwind, aus ±45 Grad der Gegenrichtung Rückenwind, dazwischen Seitenwind - von
 * rechts, wenn er aus dem Sektor im Uhrzeigersinn neben der Laufrichtung kommt, sonst von links. Die
 * Grenzen (genau 45 und 135 Grad) zählen noch zu Gegen- bzw. Rückenwind.
 */
export function windZuSchenkel(peilung: number, windAus: number): WindZuSchenkel {
  // Differenz auf (-180, 180]: 0 = Wind kommt von vorn, 180 = von hinten, positiv = von rechts.
  let diff = normalisiere(windAus - peilung);
  if (diff > 180) diff -= 360;
  const betrag = Math.abs(diff);
  if (betrag <= 45) return "gegenwind";
  if (betrag >= 135) return "rueckenwind";
  return diff > 0 ? "seitenwind-rechts" : "seitenwind-links";
}

const WIND_BESCHRIFTUNG: Record<WindZuSchenkel, string> = {
  rueckenwind: uebersetzbar("Rückenwind"),
  gegenwind: uebersetzbar("Gegenwind"),
  "seitenwind-links": uebersetzbar("Seitenwind von links"),
  "seitenwind-rechts": uebersetzbar("Seitenwind von rechts"),
};

/** Der deutsche Text (Schlüssel für t()) zu einer Windlage. */
export function windBeschriftung(lage: WindZuSchenkel): string {
  return WIND_BESCHRIFTUNG[lage];
}

type SchenkelSpur = Partial<
  Pick<GpsTrack, "points" | "laidWindSpeedKmh" | "laidWindDirectionDeg" | "searchWindSpeedKmh" | "searchWindDirectionDeg">
>;

/**
 * Der Wind, nach dem die Schenkel eingeordnet werden: der beim Suchen, wenn dafür eine Richtung vorliegt,
 * sonst der beim Legen - und nur ab WIND_MIN_KMH. Liegt beim Suchen eine Richtung vor, aber zu schwacher
 * Wind, wird nicht auf den vom Legen zurückgegriffen: Er beschriebe nicht das Wetter der Suche.
 */
export function massgeblicherWind(spur: SchenkelSpur): { aus: number; kmh: number } | null {
  const beimSuchen = spur.searchWindDirectionDeg != null;
  const aus = beimSuchen ? spur.searchWindDirectionDeg : spur.laidWindDirectionDeg;
  const kmh = beimSuchen ? spur.searchWindSpeedKmh : spur.laidWindSpeedKmh;
  return aus != null && kmh != null && kmh >= WIND_MIN_KMH ? { aus, kmh } : null;
}

/**
 * Die Laufrichtung je Schenkel (Peilung Anfang nach Ende), in Schenkelreihenfolge. Anfang und Ende eines
 * Schenkels sind Start, Winkel und Ende der gelegten Fährte; die Winkel kennt der Ablauf mit ihrer Lage
 * (GpsWalkRunCorner). Hat ein Schenkel keine messbare Länge, oder passen Schenkel und Winkel nicht
 * zusammen (n Winkel = n + 1 Schenkel), gibt es keine Richtung (null).
 */
export function schenkelPeilungen(
  legs: readonly Pick<GpsWalkRunLeg, "number">[],
  corners: readonly Pick<GpsWalkRunCorner, "number" | "latitude" | "longitude">[],
  legung: readonly GpsPoint[],
): (number | null)[] {
  const linie = automatischePunkte(legung).sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  const nummer = legs.map((leg) => leg.number).sort((a, b) => a - b);
  if (linie.length < 2 || legs.length !== corners.length + 1) return legs.map(() => null);

  const winkel = [...corners].sort((a, b) => a.number - b.number);
  const stationen = [linie[0], ...winkel, linie[linie.length - 1]];
  const peilungen = nummer.map((_, i) => {
    const von = stationen[i];
    const nach = stationen[i + 1];
    // Unter 2 m ist keine Richtung zu messen (GPS-Rauschen).
    return haversineMeters(von, nach) < 2 ? null : bearingDegrees(von, nach);
  });
  // Zurück in die Reihenfolge der übergebenen Schenkel.
  return legs.map((leg) => peilungen[nummer.indexOf(leg.number)] ?? null);
}

/** Die Windlage je Schenkelnummer; Schenkel ohne Richtung oder ohne ausreichend Wind fehlen. */
export function windJeSchenkel(
  spur: SchenkelSpur,
  legs: readonly Pick<GpsWalkRunLeg, "number">[],
  corners: readonly Pick<GpsWalkRunCorner, "number" | "latitude" | "longitude">[],
): Map<number, WindZuSchenkel> {
  const ergebnis = new Map<number, WindZuSchenkel>();
  const wind = massgeblicherWind(spur);
  if (!wind || !spur.points) return ergebnis;

  const peilungen = schenkelPeilungen(legs, corners, spur.points);
  legs.forEach((leg, i) => {
    const peilung = peilungen[i];
    if (peilung != null) ergebnis.set(leg.number, windZuSchenkel(peilung, wind.aus));
  });
  return ergebnis;
}

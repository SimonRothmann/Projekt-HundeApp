import { bearingDegrees, haversineMeters } from "@/lib/geo";

type Koordinate = { latitude: number; longitude: number };

// Für die Peilungs-Berechnung (Karte in Laufrichtung drehen) werden nicht
// einfach die letzten zwei Punkte genommen: bei geringer Schrittgeschwindigkeit
// liegen aufeinanderfolgende Punkte oft nur wenige Meter auseinander und die
// gemessene Richtung springt durch GPS-Rauschen wild um. Gemessen wird deshalb
// zum jüngsten Punkt, der mindestens so weit zurückliegt. Der Mindestabstand
// verhindert zusätzlich Sprünge im Stillstand.
export const BEARING_MIN_DISTANCE_M = 3;
// EMA-Glättung für die Rotation selbst: harte Sprünge in der Kartenanzeige
// wirken schwindelerregend. α klein = langsam nachdrehen, aber ruhig.
export const HEADING_SMOOTH_ALPHA = 0.2;

/**
 * Die Laufrichtung aus den letzten Punkten, in Grad (0 = Nord).
 *
 * Gemessen wird vom jüngsten früheren Punkt, der mindestens
 * BEARING_MIN_DISTANCE_M vom neuesten entfernt liegt - beugt Sprüngen bei
 * Stillstand vor. Gibt es keinen, zählt der älteste Punkt. Ohne zwei Punkte
 * gibt es keine Richtung (null): Der Aufrufer behält die bisherige.
 */
export function rohePeilung(punkte: Koordinate[]): number | null {
  if (punkte.length < 2) return null;
  const neuester = punkte[punkte.length - 1];

  let anker = punkte[punkte.length - 2];
  for (let i = punkte.length - 2; i >= 0; i--) {
    anker = punkte[i];
    if (haversineMeters(anker, neuester) >= BEARING_MIN_DISTANCE_M) break;
  }
  return bearingDegrees(anker, neuester);
}

/**
 * Glättet die Peilung gegen den bisherigen Wert (exponentiell).
 *
 * Mit der kürzesten Winkeldifferenz gerechnet: Ohne die Behandlung würde ein
 * Sprung von 350° auf 10° als -340°-Bewegung geglättet, obwohl es nur +20°
 * sind. Ergebnis liegt in [0, 360).
 */
export function glaettePeilung(bisher: number | null, roh: number): number {
  if (bisher === null) return roh;
  let diff = roh - bisher;
  if (diff > 180) diff -= 360;
  if (diff < -180) diff += 360;
  return (bisher + HEADING_SMOOTH_ALPHA * diff + 360) % 360;
}

/**
 * Wie die Karte ausgerichtet ist (nur live umschaltbar, per Kompass-Knopf):
 * - "north-arrow": Nord oben, Richtungspfeil an der eigenen Position
 *   (klassisch, wie Google Maps 'North Up')
 * - "heading": Karte selbst in Laufrichtung gedreht (klassischer Navi-Modus)
 * - "north": Nord oben ohne Zusatzpfeil (statische Ansicht)
 */
export type Ausrichtung = "north-arrow" | "heading" | "north";

/** Der nächste Modus im Kreis, wie ihn der Kompass-Knopf durchschaltet. */
export function naechsteAusrichtung(aktuell: Ausrichtung): Ausrichtung {
  return aktuell === "north-arrow" ? "heading" : aktuell === "heading" ? "north" : "north-arrow";
}

/**
 * Das bearing, das die Karte haben soll. MapLibre-bearing ist die
 * Himmelsrichtung, die oben steht - bei der Laufrichtung also genau die
 * Peilung. null = noch keine Richtung bekannt, die Karte bleibt wie sie ist.
 */
export function zielPeilung(ausrichtung: Ausrichtung, geglaettet: number | null): number | null {
  return ausrichtung === "heading" ? geglaettet : 0;
}

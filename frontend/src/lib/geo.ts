type Coordinate = { latitude: number; longitude: number };

export function haversineMeters(a: Coordinate, b: Coordinate): number {
  const earthRadiusMeters = 6371000;
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const dLat = toRad(b.latitude - a.latitude);
  const dLon = toRad(b.longitude - a.longitude);
  const sinDLat = Math.sin(dLat / 2);
  const sinDLon = Math.sin(dLon / 2);
  const h = sinDLat * sinDLat + Math.cos(toRad(a.latitude)) * Math.cos(toRad(b.latitude)) * sinDLon * sinDLon;
  return 2 * earthRadiusMeters * Math.asin(Math.sqrt(h));
}

// Peilung (Bearing) von a nach b als geografischer Kurswinkel in Grad
// (0 = Nord, 90 = Ost, 180 = Süd, 270 = West). Nach der Standardformel für
// die Anfangs-Peilung entlang eines Großkreises. Ergebnis liegt im
// Bereich [0, 360).
export function bearingDegrees(a: Coordinate, b: Coordinate): number {
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const φ1 = toRad(a.latitude);
  const φ2 = toRad(b.latitude);
  const λ1 = toRad(a.longitude);
  const λ2 = toRad(b.longitude);
  const y = Math.sin(λ2 - λ1) * Math.cos(φ2);
  const x = Math.cos(φ1) * Math.sin(φ2) - Math.sin(φ1) * Math.cos(φ2) * Math.cos(λ2 - λ1);
  const θ = Math.atan2(y, x);
  return ((θ * 180) / Math.PI + 360) % 360;
}

/**
 * Die Art eines GPS-Punkts (GpsPointType im Backend, GpsPoint.pointType): 0 =
 * automatisch aufgezeichnet, 1 = von Hand gesetzter Marker. Ein Marker liegt
 * i.d.R. nicht exakt auf der gelaufenen Linie und gehört nicht in Länge,
 * Fährtenalter oder Linie. GpsWalkPoint kennt kein pointType-Feld (immer
 * undefined) - dort zählt jeder Punkt als automatisch. Die EINE Stelle für diese
 * Unterscheidung.
 */
export const PUNKT_AUTOMATISCH = 0;
export const PUNKT_MARKER = 1;

type MitPunktart = { pointType?: number };

export const istMarker = (p: MitPunktart): boolean => p.pointType === PUNKT_MARKER;

/** Die Punkte der gelaufenen Linie: alles außer den Markern, in der Reihenfolge der Eingabe. */
export function automatischePunkte<T extends MitPunktart>(punkte: readonly T[]): T[] {
  return punkte.filter((p) => !istMarker(p));
}

/** Die von Hand gesetzten Marker, in der Reihenfolge der Eingabe. */
export function markerPunkte<T extends MitPunktart>(punkte: readonly T[]): T[] {
  return punkte.filter(istMarker);
}

export function estimateLengthMeters(points: (Coordinate & { pointType?: number })[]): number {
  const automaticPoints = automatischePunkte(points);
  let total = 0;
  for (let i = 1; i < automaticPoints.length; i++) {
    total += haversineMeters(automaticPoints[i - 1], automaticPoints[i]);
  }
  return Math.round(total);
}

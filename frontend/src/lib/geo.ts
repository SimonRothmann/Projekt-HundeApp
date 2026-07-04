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

// pointType 1 = manuell gesetzter Marker (siehe GpsPointType in lib/types.ts) -
// liegt i.d.R. nicht exakt auf der gelaufenen Linie und würde die
// Distanzschätzung verzerren. GpsWalkPoint kennt kein pointType-Feld (immer
// undefined), dort gehen folglich alle Punkte in die Schätzung ein.
export function estimateLengthMeters(points: (Coordinate & { pointType?: number })[]): number {
  const automaticPoints = points.filter((p) => p.pointType !== 1);
  let total = 0;
  for (let i = 1; i < automaticPoints.length; i++) {
    total += haversineMeters(automaticPoints[i - 1], automaticPoints[i]);
  }
  return Math.round(total);
}

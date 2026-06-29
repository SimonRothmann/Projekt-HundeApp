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

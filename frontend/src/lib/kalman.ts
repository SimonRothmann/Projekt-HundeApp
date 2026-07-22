// Kalman-Filter für GPS-Positionsglättung (Random-Walk-Modell).
// Eigenes Modul (statt Teil von use-gps-recorder), damit die reine
// Filter-Mathematik ohne React-Hook-Kontext unit-testbar ist.

// Erwartete Bewegung zwischen zwei Samples in Metern. Bei
// Gehgeschwindigkeit (~1 m/s) und ~1 Hz GPS-Rate ≈ 0.5-1 m. Wird als
// Prozessrauschen Q genutzt: höher = mehr Vertrauen in neue Messung
// (reagiert schneller auf Richtungswechsel), niedriger = mehr Glättung.
const KALMAN_PROCESS_NOISE_M = 0.5;

// Kalman-Zustand pro Koordinate. P ist die Schätzunsicherheit in Grad² -
// startet mit der ersten Messunsicherheit und konvergiert schnell gegen den
// Gleichgewichtswert.
export type KalmanState = {
  lat: number;
  lon: number;
  P_lat: number;
  P_lon: number;
};

/** Initialzustand aus der ersten akzeptierten GPS-Messung. */
export function kalmanInit(lat: number, lon: number, accuracyM: number): KalmanState {
  const cosLat = Math.cos((lat * Math.PI) / 180);
  return {
    lat,
    lon,
    P_lat: (accuracyM / 111111) ** 2,
    P_lon: (accuracyM / (111111 * (cosLat === 0 ? 1 : cosLat))) ** 2,
  };
}

// Einen Kalman-Predict-Update-Schritt für eine 2D-Position (lat/lon getrennt,
// da der Fehlerkreis rund ist und keine Kreuzkorrelation nötig ist).
// Koordinaten bleiben in Grad; Unsicherheiten werden intern in Meter²
// umgerechnet, damit die Prozess- und Messrauschparameter in Metern angegeben
// werden können statt in Grad (intuitiver und von der Breitengrad-abhängigen
// Grad-Meter-Relation entkoppelt).
export function kalmanStep(state: KalmanState, lat: number, lon: number, accuracyM: number): KalmanState {
  const cosLat = Math.cos((state.lat * Math.PI) / 180);
  const mPerDegLat = 111111;
  const mPerDegLon = 111111 * (cosLat === 0 ? 1 : cosLat);

  // Prozessrauschen Q: erwartete Positionsänderung zwischen Samples (in Grad²)
  const Q_lat = (KALMAN_PROCESS_NOISE_M / mPerDegLat) ** 2;
  const Q_lon = (KALMAN_PROCESS_NOISE_M / mPerDegLon) ** 2;

  // Messrauschen R: von der GPS-API gemeldeter Fehlerkreisradius → 1-σ-Varianz.
  // Die Geolocation-Spec definiert accuracy als 95%-Konfidenzkreis-Radius,
  // viele Browser (inkl. iOS Safari) liefern aber de facto den 1-σ-Wert -
  // konservativer Ansatz: als 1-σ behandeln (σ = accuracy, R = accuracy²).
  const R_lat = (accuracyM / mPerDegLat) ** 2;
  const R_lon = (accuracyM / mPerDegLon) ** 2;

  // Predict (Random-Walk-Modell: keine Geschwindigkeitsschätzung, da Gehen
  // unregelmäßig ist und ein Velocity-Modell bei häufigen Richtungswechseln
  // nicht besser abschneidet)
  const P_pred_lat = state.P_lat + Q_lat;
  const P_pred_lon = state.P_lon + Q_lon;

  // Kalman-Gain: K nahe 1 → neue Messung stark gewichten (Unsicherheit groß
  // oder GPS-Fehler klein), K nahe 0 → alte Schätzung bevorzugen.
  const K_lat = P_pred_lat / (P_pred_lat + R_lat);
  const K_lon = P_pred_lon / (P_pred_lon + R_lon);

  return {
    lat: state.lat + K_lat * (lat - state.lat),
    lon: state.lon + K_lon * (lon - state.lon),
    P_lat: (1 - K_lat) * P_pred_lat,
    P_lon: (1 - K_lon) * P_pred_lon,
  };
}

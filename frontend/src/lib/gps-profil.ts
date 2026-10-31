/**
 * Wie eine GPS-Aufzeichnung filtert und wie ihre Genauigkeitsanzeige färbt -
 * je Aufzeichnungsart EIN Profil, damit die Farben der Anzeige an den
 * Filterwerten hängen und nicht als eigene Zahlen daneben stehen.
 */

export type GpsProfil = {
  /** Punkte mit größerem Fehlerkreis werden verworfen. Kleiner = genauer, aber längere Wartezeit beim Kaltstart. */
  maxAccuracyMeters: number;
  /**
   * Obergrenze für die adaptive Lockerung des Filters. Ohne Netz (kein aGPS, keine WLAN-Ortung)
   * konvergiert der GPS-Chip oft nur bis 10-20 m - ein harter 8-m-Filter würde dann NIE einen Punkt
   * akzeptieren und die Aufzeichnung bliebe leer. Deshalb startet der Filter bei `maxAccuracyMeters`
   * und wird, wenn lange kein Punkt durchkam, schrittweise bis hierher gelockert. Gleich
   * `maxAccuracyMeters`: keine Lockerung.
   */
  relaxedMaxAccuracyMeters: number;
  /** Kalman-Filter: adaptiv gewichtete Glättung (schlechte Messung = weniger Gewicht). */
  kalman: boolean;
  /** Die Anzeige ist bis hierher grün ... */
  gutBis: number;
  /** ... bis hierher gelb, darüber rot. */
  mittelBis: number;
};

/**
 * Fährte und Trail legen: Erstaufnahme und Wiederholung sollen deckungsgleich sein, deshalb 8 m und
 * Glättung. Grün ist, was der Filter ohne Lockerung annimmt (8 m).
 */
export const GPS_PROFIL_LEGEN: GpsProfil = {
  maxAccuracyMeters: 8,
  relaxedMaxAccuracyMeters: 20,
  kalman: true,
  gutBis: 8,
  mittelBis: 15,
};

/**
 * Ablauf (Suchen): Standardwerte, 25 m und ohne Glättung - bewusst, bis ein Feldtest anderes zeigt.
 * Strengere Filter könnten bei schlechtem Empfang leere Läufe ergeben; den Ablauf misst die
 * Auswertung ohnehin über Abweichungen von 3 bis 25 m. Gelb reicht bis zur Filtergrenze.
 */
export const GPS_PROFIL_ABLAUF: GpsProfil = {
  maxAccuracyMeters: 25,
  relaxedMaxAccuracyMeters: 25,
  kalman: false,
  gutBis: 10,
  mittelBis: 25,
};

/** Mindestabstand zum letzten Punkt; eliminiert GPS-Rauschen im Stillstand (für alle Aufzeichnungen gleich). */
export const MIN_ABSTAND_METER = 2;

export type GenauigkeitsStufe = "gut" | "mittel" | "schlecht";

/** Wie gut die gemeldete Genauigkeit (Fehlerkreis in Metern) für dieses Profil ist. */
export function genauigkeitsStufe(genauigkeit: number, profil: GpsProfil): GenauigkeitsStufe {
  if (genauigkeit <= profil.gutBis) return "gut";
  return genauigkeit <= profil.mittelBis ? "mittel" : "schlecht";
}

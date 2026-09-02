/**
 * Gemeinsamer Zugang zu IndexedDB für die beiden Speicher der App
 * (Lesecache und Offline-Warteschlange).
 *
 * Warum überhaupt geteilt: Beide Module hatten dieselbe Öffnungsroutine
 * doppelt - und beide hatten damit denselben Fehler. Jeder einzelne Lese-
 * und Schreibvorgang öffnete eine EIGENE Verbindung und schloss sie nie.
 * Das kostet Zeit (eine Hundeliste holt über die Profilbilder zwei Zugriffe
 * pro Hund; gemessen rund das Dreifache gegenüber einer geteilten
 * Verbindung) und sammelt offene Verbindungen an. Offene Verbindungen
 * blockieren jedes künftige onupgradeneeded: Eine Erhöhung der Version wäre
 * stillschweigend hängen geblieben, statt das Schema zu ändern.
 */

/**
 * Öffnet eine Datenbank einmal und gibt danach immer dieselbe Verbindung
 * zurück.
 *
 * Gemerkt wird das Promise, nicht die Datenbank: So warten gleichzeitige
 * Aufrufe auf denselben Öffnungsvorgang, statt mehrere anzustoßen.
 */
export function sharedDb(
  name: string,
  version: number,
  upgrade: (db: IDBDatabase) => void,
): () => Promise<IDBDatabase> {
  let pending: Promise<IDBDatabase> | null = null;

  return () =>
    (pending ??= new Promise<IDBDatabase>((resolve, reject) => {
      const request = indexedDB.open(name, version);
      request.onupgradeneeded = () => upgrade(request.result);
      request.onsuccess = () => {
        // Schließt der Browser die Verbindung von sich aus (Speicherdruck)
        // oder verlangt ein anderer Tab eine Schemaänderung, muss der
        // nächste Zugriff neu öffnen dürfen - sonst hinge der Speicher
        // dauerhaft an einer toten Verbindung und lieferte nichts mehr.
        request.result.onclose = () => {
          pending = null;
        };
        request.result.onversionchange = () => {
          request.result.close();
          pending = null;
        };
        resolve(request.result);
      };
      request.onerror = () => {
        pending = null;
        reject(request.error);
      };
    }));
}

import { api, ApiError } from "@/lib/api";

/**
 * Offline-Warteschlange für Schreibvorgänge, die laut PRODUCT_REQUIREMENTS.md
 * "Offline" auch ohne Internet funktionieren müssen (Training erfassen, GPS
 * speichern). Persistiert in IndexedDB (siehe ARCHITECTURE.md "Offline
 * Architektur": Service Worker + IndexedDB + Synchronisationsengine) und wird
 * synchronisiert, sobald die Verbindung wieder verfügbar ist.
 */

const DB_NAME = "dogity-offline";
const STORE_NAME = "pending-requests";
const DB_VERSION = 1;

export type QueuedRequest = {
  id: string;
  path: string;
  method: "POST" | "PUT" | "DELETE";
  body: unknown;
  label: string;
  createdAt: string;
};

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      request.result.createObjectStore(STORE_NAME, { keyPath: "id" });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function enqueueRequest(item: Omit<QueuedRequest, "id" | "createdAt">): Promise<void> {
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    tx.objectStore(STORE_NAME).add({
      ...item,
      id: nextQueueId(),
      createdAt: new Date().toISOString(),
    });
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

// FIFO über den Key: IndexedDB getAll() liefert in aufsteigender
// KEY-Reihenfolge, nicht in Einfüge-Reihenfolge - mit rein zufälligen
// UUID-Keys (früher) war die Abspielreihenfolge daher effektiv zufällig,
// eine offline erfasste Fährte konnte VOR ihrem ebenfalls offline erfassten
// Training gesendet werden (404, weil die Session serverseitig noch fehlt).
// Zeitstempel-Präfix + monotone Sequenz (für mehrere Einträge in derselben
// Millisekunde) machen den Key chronologisch sortierbar, das UUID-Suffix
// hält ihn kollisionsfrei über Seiten-Reloads hinweg.
let enqueueSeq = 0;
function nextQueueId(): string {
  return `${Date.now()}-${(enqueueSeq++).toString().padStart(6, "0")}-${crypto.randomUUID()}`;
}

export async function listQueuedRequests(): Promise<QueuedRequest[]> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readonly");
    const request = tx.objectStore(STORE_NAME).getAll();
    request.onsuccess = () => resolve(request.result as QueuedRequest[]);
    request.onerror = () => reject(request.error);
  });
}

async function removeQueuedRequest(id: string): Promise<void> {
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    tx.objectStore(STORE_NAME).delete(id);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

/**
 * Dauerhaft aussichtslose Server-Antworten: 4xx-Fehler, die auch beim
 * nächsten Versuch identisch scheitern würden (z.B. 404 auf ein inzwischen
 * gelöschtes Training, 400 wegen Validierung). Ausgenommen sind die drei
 * 4xx-Codes mit Retry-Charakter: 401 (Session abgelaufen - nach erneutem
 * Login klappt es wieder), 408 (Timeout) und 429 (Rate-Limit).
 */
function isPermanentFailure(err: unknown): err is ApiError {
  return (
    err instanceof ApiError &&
    err.status >= 400 &&
    err.status < 500 &&
    err.status !== 401 &&
    err.status !== 408 &&
    err.status !== 429
  );
}

/**
 * Spielt offen gebliebene Requests in Aufnahmereihenfolge ab. Bricht bei
 * Netzwerkfehlern/5xx/401 ab, damit die Reihenfolge erhalten bleibt (z.B.
 * Training vor zugehöriger Fährte) und später erneut versucht wird.
 *
 * Dauerhaft aussichtslose Einträge (siehe isPermanentFailure) werden
 * dagegen VERWORFEN und der Aufrufer per onItemFailed informiert - vorher
 * blockierte ein einziges solches Item (z.B. 404 auf ein gelöschtes
 * Training) die gesamte Queue für immer, alle nachfolgenden Einträge wären
 * nie mehr synchronisiert worden.
 */
export async function syncQueuedRequests(
  onItemSynced?: (item: QueuedRequest) => void,
  onItemFailed?: (item: QueuedRequest, error: ApiError) => void,
): Promise<number> {
  const items = await listQueuedRequests();
  let syncedCount = 0;

  for (const item of items) {
    try {
      if (item.method === "POST") await api.post(item.path, item.body);
      else if (item.method === "PUT") await api.put(item.path, item.body);
      else await api.delete(item.path);

      await removeQueuedRequest(item.id);
      syncedCount++;
      onItemSynced?.(item);
    } catch (err) {
      if (!isPermanentFailure(err)) break;
      await removeQueuedRequest(item.id);
      onItemFailed?.(item, err);
    }
  }

  return syncedCount;
}

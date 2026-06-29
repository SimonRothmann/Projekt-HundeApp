import { api } from "@/lib/api";

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
      id: crypto.randomUUID(),
      createdAt: new Date().toISOString(),
    });
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
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
 * Spielt offen gebliebene Requests in Aufnahmereihenfolge ab. Bricht bei
 * der ersten weiterhin fehlschlagenden Anfrage ab, damit die Reihenfolge
 * erhalten bleibt (z.B. Training vor zugehöriger Fährte).
 */
export async function syncQueuedRequests(onItemSynced?: (item: QueuedRequest) => void): Promise<number> {
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
    } catch {
      break;
    }
  }

  return syncedCount;
}

"use client";

/**
 * IndexedDB-basierter Read-Cache für API-GET-Antworten.
 *
 * Das Backend ist cross-origin zum Frontend → der Service Worker kann
 * /api/* Antworten nicht abfangen. Dieser Cache schließt die Lücke:
 * beim Mount einer Seite werden gecachte Daten sofort angezeigt (auch
 * offline), danach wird im Hintergrund die frische Version geladen und
 * der Cache aktualisiert (Stale-While-Revalidate).
 *
 * Bewusst eigene Datenbank (dogity-read-cache) statt Erweiterung der
 * Schreib-Warteschlange (dogity-offline) - keine gegenseitige Kopplung,
 * eigenständig löschbar (Clear Cache löscht Read-Cache ohne Schreibqueue
 * zu verlieren und umgekehrt).
 */

const DB_NAME = "dogity-read-cache";
const STORE_NAME = "entries";
const DB_VERSION = 1;

type CacheEntry<T> = {
  key: string;
  data: T;
  cachedAt: number;
};

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      request.result.createObjectStore(STORE_NAME, { keyPath: "key" });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function getCachedData<T>(key: string): Promise<T | null> {
  try {
    const db = await openDb();
    return new Promise((resolve) => {
      const tx = db.transaction(STORE_NAME, "readonly");
      const req = tx.objectStore(STORE_NAME).get(key);
      req.onsuccess = () => {
        const entry = req.result as CacheEntry<T> | undefined;
        resolve(entry?.data ?? null);
      };
      req.onerror = () => resolve(null);
    });
  } catch {
    return null;
  }
}

export async function setCachedData<T>(key: string, data: T): Promise<void> {
  try {
    const db = await openDb();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, "readwrite");
      tx.objectStore(STORE_NAME).put({ key, data, cachedAt: Date.now() } satisfies CacheEntry<T>);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } catch {
    // Schreiben in den Cache ist optional - Fehler ignorieren.
  }
}

import { api, ApiError, USER_KEY } from "@/lib/api";
import { sharedDb } from "@/lib/idb";
import { istPlanZielFehler, ohnePlanVerknuepfung } from "@/lib/trainingsplan";

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
  method: "POST";
  body: unknown;
  label: string;
  createdAt: string;
  /**
   * Wer den Eintrag erfasst hat. Abgespielt wird er nur mit dessen Anmeldung
   * (siehe gehoertZu). Fehlt bei Einträgen von vor 2026-09-28 - die laufen
   * wie bisher mit, wer gerade angemeldet ist.
   */
  userId?: string;
  /**
   * Der Server hat den Eintrag abgelehnt (4xx). Er bleibt liegen, bis die Person entscheidet:
   * erneut senden oder verwerfen. Vorher wurde er gelöscht und nur kurz gemeldet.
   */
  abgelehnt?: { status: number; meldung: string };
  /** Wie oft der Server mit einem Fehler (5xx) geantwortet hat; Netzfehler zählen nicht. */
  versuche?: number;
  /** Frühester nächster Versuch (ms seit 1970) nach einem Serverfehler. */
  naechsterVersuch?: number;
  /** Nach zu vielen Serverfehlern aufgegeben: wird nicht mehr von selbst gesendet, nur auf Knopfdruck. */
  haengt?: boolean;
  /** Die Meldung des letzten Serverfehlers - für die Anzeige bei "hängt". */
  letzteMeldung?: string;
  /**
   * localStorage-Schlüssel der Kopie der Aufzeichnung (siehe aufzeichnung-sicherung.ts). Sie wird erst
   * gelöscht, wenn der Server den Eintrag bestätigt hat - oder die Person ihn verwirft.
   */
  sicherung?: string;
};

/** In welchem Zustand ein Eintrag in der Warteschlange liegt. */
export type EintragZustand = "wartet" | "abgelehnt" | "haengt";

export function zustandVon(item: QueuedRequest): EintragZustand {
  return item.abgelehnt ? "abgelehnt" : item.haengt ? "haengt" : "wartet";
}

/** So viele Serverfehler hintereinander, dann gilt ein Eintrag als hängend. */
export const MAX_VERSUCHE = 6;
const ABSTAND_BASIS_MS = 30_000;
const ABSTAND_MAX_MS = 30 * 60_000;

/** Wartezeit bis zum nächsten Versuch nach dem n-ten Serverfehler: 30 s, 1, 2, 4, 8 Minuten ... höchstens 30. */
export function abstandNachVersuch(n: number): number {
  return Math.min(ABSTAND_BASIS_MS * 2 ** Math.max(0, n - 1), ABSTAND_MAX_MS);
}

// Wer wissen will, wann sich die Warteschlange ändert (der Hinweis auf abgelehnte Einträge): Es wird nur
// gemeldet, nicht mitgeliefert - die Liste liest, wer sie braucht, frisch aus der Datenbank.
const hoerer = new Set<() => void>();
let stand = 0;

function melden() {
  stand++;
  hoerer.forEach((h) => h());
}

export function warteschlangeAbonnieren(h: () => void) {
  hoerer.add(h);
  return () => {
    hoerer.delete(h);
  };
}

/** Zähler, der sich bei jeder Änderung erhöht - für useSyncExternalStore. */
export function warteschlangeStand() {
  return stand;
}

/** Die gerade angemeldete Person, wie sie auth-context im localStorage ablegt. */
function aktuellePerson(): string | null {
  try {
    if (typeof window === "undefined") return null;
    const gespeichert = window.localStorage.getItem(USER_KEY);
    return gespeichert ? ((JSON.parse(gespeichert) as { userId?: string }).userId ?? null) : null;
  } catch {
    return null;
  }
}

/**
 * Ob ein Eintrag der Person gehört. Vorher lief die Warteschlange mit
 * JEDER Anmeldung: Meldete sich nach A jemand anderes am Gerät an, gingen
 * A's Einträge mit B's Anmeldung hinaus - bei fremden Hunden als 404
 * verworfen (A's Training weg, B sieht A's Beschriftung im Hinweis), bei
 * einem gemeinsamen Hund unter B's Namen gespeichert.
 */
function gehoertZu(item: QueuedRequest, userId: string | null): boolean {
  return item.userId === undefined || item.userId === userId;
}

// Eine geteilte Verbindung statt einer pro Zugriff - Begründung in idb.ts.
// Hier zählt das doppelt: Das Abspielen der Warteschlange greift pro Eintrag
// zu, eine offline erfasste Trainingsserie also viele Male hintereinander.
const openDb = sharedDb(DB_NAME, DB_VERSION, (db) => {
  db.createObjectStore(STORE_NAME, { keyPath: "id" });
});

export async function enqueueRequest(item: Omit<QueuedRequest, "id" | "createdAt">): Promise<void> {
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    tx.objectStore(STORE_NAME).add({
      ...item,
      id: nextQueueId(),
      createdAt: new Date().toISOString(),
      userId: aktuellePerson() ?? undefined,
    } satisfies QueuedRequest);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
  melden();
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

// Name der Web-Lock-Sperre: Die Warteschlange liegt in IndexedDB und gehört
// allen offenen Tabs und der installierten App zugleich.
const ABGLEICH_SPERRE = "dogity-offline-sync";

/**
 * Lässt einen Lauf nie doppelt starten: Wer ihn anstößt, während er läuft,
 * verfällt. Sonst gingen bei zwei gleichzeitigen Abgleichen Einträge doppelt
 * hinaus oder die Reihenfolge (Training vor Fährte) geriete durcheinander.
 *
 * Das gilt auch über Tabs hinweg (Web Locks): Jeder offene Tab stößt den
 * Abgleich an und liest dieselbe Warteschlange. Wo es die API nicht gibt,
 * schützt nur die Sperre je Seite.
 */
export function nichtGleichzeitig(lauf: () => Promise<void>): () => Promise<void> {
  let laeuft = false;
  return async () => {
    if (laeuft) return;
    laeuft = true;
    try {
      const sperren = typeof navigator === "undefined" ? undefined : navigator.locks;
      if (!sperren) {
        await lauf();
        return;
      }
      await sperren.request(ABGLEICH_SPERRE, { ifAvailable: true }, async (sperre) => {
        if (sperre) await lauf();
      });
    } finally {
      laeuft = false;
    }
  };
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

/** Offene Einträge der gerade angemeldeten Person - etwa für den Hinweis beim Abmelden. */
export async function listOwnQueuedRequests(): Promise<QueuedRequest[]> {
  const userId = aktuellePerson();
  return (await listQueuedRequests()).filter((item) => gehoertZu(item, userId));
}

/**
 * Entfernt alle Einträge einer Person. Für die Kontolöschung: Das Konto gibt
 * es danach nicht mehr, niemand könnte die Einträge je abschicken.
 */
export async function removeQueuedRequestsOf(userId: string): Promise<void> {
  const eigene = (await listQueuedRequests()).filter((item) => item.userId === userId);
  for (const item of eigene) await removeQueuedRequest(item);
}

async function removeQueuedRequest(item: QueuedRequest): Promise<void> {
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    tx.objectStore(STORE_NAME).delete(item.id);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
  // Erst jetzt, wo der Eintrag aus der Warteschlange ist (Server hat ihn bestätigt oder die Person hat
  // ihn verworfen), darf auch die Kopie der Aufzeichnung weg.
  if (item.sicherung) {
    try {
      window.localStorage.removeItem(item.sicherung);
    } catch {
      // Gesperrter Speicher: Die Kopie bleibt liegen, der Eintrag ist trotzdem weg.
    }
  }
  melden();
}

/** Ändert Felder eines Eintrags, der noch in der Warteschlange liegt (ein inzwischen entfernter bleibt weg). */
async function aktualisieren(id: string, aenderung: Partial<QueuedRequest>): Promise<void> {
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    const store = tx.objectStore(STORE_NAME);
    const lesen = store.get(id);
    lesen.onsuccess = () => {
      if (lesen.result) store.put({ ...(lesen.result as QueuedRequest), ...aenderung });
    };
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
  melden();
}

async function eigenerEintrag(id: string): Promise<QueuedRequest | undefined> {
  const userId = aktuellePerson();
  return (await listQueuedRequests()).find((item) => item.id === id && gehoertZu(item, userId));
}

/** Noch einmal senden: Der Eintrag verliert Ablehnung, Fehlerzähler und Wartezeit; der nächste Abgleich nimmt ihn mit. */
export async function eintragErneutSenden(id: string): Promise<void> {
  if (!(await eigenerEintrag(id))) return;
  await aktualisieren(id, {
    abgelehnt: undefined,
    haengt: undefined,
    versuche: undefined,
    naechsterVersuch: undefined,
    letzteMeldung: undefined,
  });
}

/** Wirft einen Eintrag samt Kopie der Aufzeichnung endgültig weg - nur nach Rückfrage der Person aufrufen. */
export async function eintragVerwerfen(id: string): Promise<void> {
  const item = await eigenerEintrag(id);
  if (item) await removeQueuedRequest(item);
}

/** Abgelehnte und hängende Einträge der angemeldeten Person - der Hinweis in der App-Hülle zeigt sie. */
export async function listProblemRequests(): Promise<QueuedRequest[]> {
  return (await listOwnQueuedRequests()).filter((item) => zustandVon(item) !== "wartet");
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
 * Ein Training, dessen Plan-Ziel es nicht mehr gibt (Plan woanders neu erzeugt),
 * wird einmal ohne Verknüpfung gesendet statt verworfen - siehe
 * ohnePlanVerknuepfung.
 */
async function postMitPlanAusweg(item: QueuedRequest): Promise<void> {
  try {
    await api.post(item.path, item.body);
  } catch (err) {
    const ohne = item.path === "/api/trainings" && istPlanZielFehler(err) ? ohnePlanVerknuepfung(item.body) : null;
    if (ohne === null) throw err;
    await api.post(item.path, ohne);
  }
}

/** Serverfehler, die am Eintrag liegen können: 5xx außer 502/503 (so antwortet der Proxy beim Neustart des Servers). */
function istServerfehlerDesEintrags(err: unknown): err is ApiError {
  return err instanceof ApiError && err.status >= 500 && err.status !== 502 && err.status !== 503;
}

const KENNUNG = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function kennung(wert: unknown): string | null {
  return typeof wert === "string" && KENNUNG.test(wert) ? wert.toLowerCase() : null;
}

/** Die Kennung, unter der ein Eintrag beim Server entsteht (vom Gerät vergeben) - null, wenn er keine mitbringt. */
function erzeugteKennung(item: QueuedRequest): string | null {
  return kennung((item.body as { id?: unknown } | null)?.id);
}

/**
 * Welche anderen Einträge ein Eintrag voraussetzt, erkannt an Kennungen in Pfad und Rumpf:
 *  - Ablauf zu einer Fährte (`/api/gps-tracks/{id}/walk-runs`) und Fährte zu einem Training
 *    (`trainingSessionId`) brauchen den anderen Eintrag wirklich ("hart"): Ohne ihn gäbe es ein 404.
 *  - Ein Training, das Merkzettel-Einträge abhakt (`erledigteMerkzettelIds`), kommt auch ohne sie an,
 *    nur bliebe der Haken aus ("weich"): Es wartet, solange der Merkzettel-Eintrag noch versucht wird,
 *    nicht aber, wenn der selbst hängt oder abgelehnt ist.
 */
function voraussetzungen(item: QueuedRequest): { kennung: string; weich: boolean }[] {
  const gefunden: { kennung: string; weich: boolean }[] = [];
  const imPfad = /^\/api\/gps-tracks\/([^/]+)\//.exec(item.path);
  const pfadKennung = kennung(imPfad?.[1]);
  if (pfadKennung) gefunden.push({ kennung: pfadKennung, weich: false });

  const body = item.body as { trainingSessionId?: unknown; erledigteMerkzettelIds?: unknown } | null;
  const einheit = kennung(body?.trainingSessionId);
  if (einheit) gefunden.push({ kennung: einheit, weich: false });
  if (Array.isArray(body?.erledigteMerkzettelIds)) {
    for (const wert of body.erledigteMerkzettelIds) {
      const k = kennung(wert);
      if (k) gefunden.push({ kennung: k, weich: true });
    }
  }
  return gefunden;
}

/**
 * Spielt offen gebliebene Requests in Aufnahmereihenfolge ab und gibt keinen Eintrag still auf:
 *
 *  - Erfolg (2xx): Der Eintrag verschwindet samt Kopie der Aufzeichnung.
 *  - Abgelehnt (4xx, siehe isPermanentFailure): Der Eintrag BLEIBT, markiert als abgelehnt, und wird nicht
 *    mehr von selbst gesendet. Die Person entscheidet (eintragErneutSenden / eintragVerwerfen). Die
 *    Schlange läuft mit den übrigen Einträgen weiter.
 *  - Serverfehler (5xx): begrenzt wiederholt, mit wachsendem Abstand (abstandNachVersuch). Nach
 *    MAX_VERSUCHEN gilt der Eintrag als hängend und blockiert nichts mehr.
 *  - Kein Netz, 401 (Anmeldung), 408, 429, 502/503 (Server startet neu): Abbruch des Laufs, später
 *    nochmals - das liegt nicht am Eintrag.
 *
 * Ein Eintrag, der einen anderen voraussetzt (siehe voraussetzungen), wartet, solange dieser in der
 * Schlange liegt; die Reihenfolge allein genügt dafür nicht mehr, weil ein Eintrag davor jetzt liegen
 * bleiben darf, ohne dass die Schlange anhält.
 *
 * Ein erneutes Senden desselben Eintrags ist für den Server harmlos: Fährte, Ablauf, Training und
 * Merkzettel tragen eine Kennung vom Gerät, der Server legt nichts doppelt an.
 *
 * onItemFailed wird einmal gemeldet, wenn ein Eintrag abgelehnt wird oder zu hängen beginnt - damit die
 * Person es mitbekommt, auch wenn sie den Hinweis in der App-Hülle noch nicht gesehen hat.
 */
export async function syncQueuedRequests(
  onItemSynced?: (item: QueuedRequest) => void,
  onItemFailed?: (item: QueuedRequest, error: ApiError, art: "abgelehnt" | "haengt") => void,
  jetzt: number = Date.now(),
): Promise<number> {
  // Nur die eigenen - fremde bleiben liegen, bis ihre Person sich wieder
  // anmeldet (siehe gehoertZu).
  const userId = aktuellePerson();
  const items = (await listQueuedRequests()).filter((item) => gehoertZu(item, userId));
  let syncedCount = 0;

  // Was in der Schlange liegt und von einem anderen Eintrag vorausgesetzt werden kann.
  const imStau = new Map<string, QueuedRequest>();
  for (const item of items) {
    const k = erzeugteKennung(item);
    if (k) imStau.set(k, item);
  }

  for (const item of items) {
    if (item.abgelehnt || item.haengt) continue;
    if ((item.naechsterVersuch ?? 0) > jetzt) continue;
    const wartetAufAnderen = voraussetzungen(item).some(({ kennung: k, weich }) => {
      const davor = imStau.get(k);
      return davor !== undefined && davor.id !== item.id && !(weich && (davor.abgelehnt || davor.haengt));
    });
    if (wartetAufAnderen) continue;

    try {
      await postMitPlanAusweg(item);

      await removeQueuedRequest(item);
      const k = erzeugteKennung(item);
      if (k) imStau.delete(k);
      syncedCount++;
      onItemSynced?.(item);
    } catch (err) {
      if (isPermanentFailure(err)) {
        const abgelehnt = { status: err.status, meldung: err.message };
        await aktualisieren(item.id, { abgelehnt });
        // Auch in der Liste dieses Laufs vermerken: Ein davon abhängiger Eintrag weiter hinten sieht es.
        item.abgelehnt = abgelehnt;
        onItemFailed?.(item, err, "abgelehnt");
        continue;
      }
      if (istServerfehlerDesEintrags(err)) {
        const versuche = (item.versuche ?? 0) + 1;
        const haengt = versuche >= MAX_VERSUCHE;
        await aktualisieren(item.id, {
          versuche,
          letzteMeldung: err.message,
          ...(haengt ? { haengt: true } : { naechsterVersuch: jetzt + abstandNachVersuch(versuche) }),
        });
        item.haengt = haengt || undefined;
        if (haengt) onItemFailed?.(item, err, "haengt");
        continue;
      }
      break;
    }
  }

  return syncedCount;
}

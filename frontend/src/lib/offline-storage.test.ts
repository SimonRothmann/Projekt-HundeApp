// fake-indexeddb/auto ersetzt globalThis.indexedDB durch eine In-Memory-
// Implementierung - MUSS vor den Modulen importiert werden, die indexedDB
// beim Aufruf verwenden.
import "fake-indexeddb/auto";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getCachedData, leseCacheLeeren, setCachedData } from "@/lib/read-cache";
import {
  enqueueRequest,
  listOwnQueuedRequests,
  listQueuedRequests,
  removeQueuedRequestsOf,
  syncQueuedRequests,
} from "@/lib/offline-queue";
import { api, ApiError, USER_KEY } from "@/lib/api";

// Nur das api-Objekt mocken, damit kein fetch stattfindet - die echte
// ApiError-Klasse muss erhalten bleiben, weil syncQueuedRequests per
// instanceof zwischen permanenten 4xx-Fehlern und Netzwerkfehlern
// unterscheidet.
vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()),
  api: {
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe("read-cache", () => {
  it("liefert null für unbekannte Keys", async () => {
    expect(await getCachedData("nie-gesetzt")).toBeNull();
  });

  it("Roundtrip: gespeicherte Daten kommen identisch zurück", async () => {
    const value = { dogs: [{ id: "1", name: "Bello" }], count: 1 };
    await setCachedData("dogs-list", value);
    expect(await getCachedData("dogs-list")).toEqual(value);
  });

  it("überschreibt bestehende Einträge (Stale-While-Revalidate-Update)", async () => {
    await setCachedData("k", "alt");
    await setCachedData("k", "neu");
    expect(await getCachedData("k")).toBe("neu");
  });

  it("wird beim Abmelden bzw. Personenwechsel vollständig geleert", async () => {
    await setCachedData("dogs-list", [{ id: "1", name: "Bello" }]);
    await setCachedData("dog-page-1", { dog: { name: "Bello" } });
    await setCachedData("stats-dashboard", { einheiten: 3 });

    await leseCacheLeeren();

    expect(await getCachedData("dogs-list")).toBeNull();
    expect(await getCachedData("dog-page-1")).toBeNull();
    expect(await getCachedData("stats-dashboard")).toBeNull();
  });
});

describe("offline-queue", () => {
  beforeEach(async () => {
    // Queue leeren: alle noch offenen Einträge erfolgreich "synchronisieren".
    vi.mocked(api.post).mockResolvedValue(undefined);
    vi.mocked(api.put).mockResolvedValue(undefined);
    vi.mocked(api.delete).mockResolvedValue(undefined);
    await syncQueuedRequests();
    vi.clearAllMocks();
  });

  it("enqueue + list: Eintrag landet mit id/createdAt in der Queue", async () => {
    await enqueueRequest({ path: "/api/trainings", method: "POST", body: { a: 1 }, label: "Training" });
    const items = await listQueuedRequests();
    expect(items).toHaveLength(1);
    expect(items[0].path).toBe("/api/trainings");
    expect(items[0].id).toBeTruthy();
    expect(items[0].createdAt).toBeTruthy();
  });

  it("sync spielt Einträge ab und leert die Queue", async () => {
    vi.mocked(api.post).mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/a", method: "POST", body: {}, label: "A" });
    await enqueueRequest({ path: "/api/b", method: "POST", body: {}, label: "B" });

    const synced = await syncQueuedRequests();

    expect(synced).toBe(2);
    expect(await listQueuedRequests()).toHaveLength(0);
    expect(api.post).toHaveBeenCalledTimes(2);
  });

  it("sync bricht beim ersten Fehler ab und behält die Reihenfolge (Training vor Fährte)", async () => {
    vi.mocked(api.post)
      .mockRejectedValueOnce(new TypeError("offline"))
      .mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/trainings", method: "POST", body: {}, label: "Training" });
    await enqueueRequest({ path: "/api/gps-tracks", method: "POST", body: {}, label: "Fährte" });

    const synced = await syncQueuedRequests();

    // Erster Request scheitert → Abbruch, NICHTS wird entfernt und die
    // Fährte wird nicht vor ihrem Training gesendet.
    expect(synced).toBe(0);
    expect(await listQueuedRequests()).toHaveLength(2);
    expect(api.post).toHaveBeenCalledTimes(1);
  });

  it("spielt Einträge in Einfüge-Reihenfolge ab (FIFO trotz IndexedDB-Key-Sortierung)", async () => {
    vi.mocked(api.post).mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/1", method: "POST", body: {}, label: "A" });
    await enqueueRequest({ path: "/api/2", method: "POST", body: {}, label: "B" });
    await enqueueRequest({ path: "/api/3", method: "POST", body: {}, label: "C" });

    await syncQueuedRequests();

    // getAll() sortiert nach Key - mit den früheren Zufalls-UUID-Keys war
    // diese Reihenfolge zufällig, seit den Zeitstempel-Keys ist sie FIFO.
    expect(vi.mocked(api.post).mock.calls.map((c) => c[0])).toEqual(["/api/1", "/api/2", "/api/3"]);
  });

  it("verwirft dauerhaft aussichtslose Einträge (4xx) und synchronisiert den Rest weiter", async () => {
    vi.mocked(api.post)
      .mockRejectedValueOnce(new ApiError(404, ["Training nicht gefunden."]))
      .mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/gps-tracks", method: "POST", body: {}, label: "Fährte" });
    await enqueueRequest({ path: "/api/trainings", method: "POST", body: {}, label: "Training" });

    const failed: string[] = [];
    const synced = await syncQueuedRequests(undefined, (item) => failed.push(item.label));

    // 404 blockiert die Queue nicht mehr: Eintrag verworfen + gemeldet,
    // der nachfolgende Eintrag wird trotzdem synchronisiert.
    expect(synced).toBe(1);
    expect(failed).toEqual(["Fährte"]);
    expect(await listQueuedRequests()).toHaveLength(0);
    expect(api.post).toHaveBeenCalledTimes(2);
  });

  it("bricht bei 401 ab und behält den Eintrag (Retry nach erneutem Login)", async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new ApiError(401, []));
    await enqueueRequest({ path: "/api/trainings", method: "POST", body: {}, label: "Training" });

    const failed: string[] = [];
    const synced = await syncQueuedRequests(undefined, (item) => failed.push(item.label));

    expect(synced).toBe(0);
    expect(failed).toEqual([]);
    expect(await listQueuedRequests()).toHaveLength(1);
  });
});

/**
 * Die Warteschlange gehört der Person, die erfasst hat. Vorher spielte sie
 * mit JEDER Anmeldung ab: Nach einem Wechsel am Gerät gingen A's Einträge
 * mit B's Anmeldung hinaus.
 */
describe("offline-queue: nur mit der eigenen Anmeldung", () => {
  class Speicher implements Storage {
    private werte = new Map<string, string>();
    get length() {
      return this.werte.size;
    }
    clear() {
      this.werte.clear();
    }
    getItem(k: string) {
      return this.werte.get(k) ?? null;
    }
    key(i: number) {
      return [...this.werte.keys()][i] ?? null;
    }
    removeItem(k: string) {
      this.werte.delete(k);
    }
    setItem(k: string, v: string) {
      this.werte.set(k, v);
    }
  }

  const speicher = new Speicher();

  function angemeldet(userId: string | null) {
    if (userId) speicher.setItem(USER_KEY, JSON.stringify({ userId }));
    else speicher.removeItem(USER_KEY);
  }

  beforeEach(async () => {
    vi.stubGlobal("window", { localStorage: speicher });
    // Reste aus den Tests oben (Einträge ohne Person) abspielen lassen.
    angemeldet(null);
    vi.mocked(api.post).mockResolvedValue(undefined);
    vi.mocked(api.put).mockResolvedValue(undefined);
    vi.mocked(api.delete).mockResolvedValue(undefined);
    await syncQueuedRequests();
    vi.clearAllMocks();
    vi.mocked(api.post).mockResolvedValue(undefined);
  });

  afterEach(async () => {
    await removeQueuedRequestsOf("anna");
    await removeQueuedRequestsOf("ben");
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("merkt sich, wer den Eintrag erfasst hat", async () => {
    angemeldet("anna");
    await enqueueRequest({ path: "/api/trainings", method: "POST", body: {}, label: "Training" });

    expect((await listQueuedRequests())[0].userId).toBe("anna");
  });

  it("spielt fremde Einträge nicht ab und behält sie für ihre Person", async () => {
    angemeldet("anna");
    await enqueueRequest({ path: "/api/anna", method: "POST", body: {}, label: "Annas Training" });
    angemeldet("ben");
    await enqueueRequest({ path: "/api/ben", method: "POST", body: {}, label: "Bens Training" });

    const synced = await syncQueuedRequests();

    expect(synced).toBe(1);
    expect(vi.mocked(api.post).mock.calls.map((c) => c[0])).toEqual(["/api/ben"]);
    expect((await listQueuedRequests()).map((i) => i.userId)).toEqual(["anna"]);

    // Meldet sich Anna wieder an, geht ihr Eintrag hinaus.
    angemeldet("anna");
    expect(await syncQueuedRequests()).toBe(1);
    expect(await listQueuedRequests()).toHaveLength(0);
  });

  it("spielt ohne Anmeldung nichts ab, was einer Person gehört", async () => {
    angemeldet("anna");
    await enqueueRequest({ path: "/api/anna", method: "POST", body: {}, label: "Training" });
    angemeldet(null);

    expect(await syncQueuedRequests()).toBe(0);
    expect(api.post).not.toHaveBeenCalled();
  });

  it("zählt für den Abmelde-Hinweis nur die eigenen offenen Einträge", async () => {
    angemeldet("anna");
    await enqueueRequest({ path: "/api/1", method: "POST", body: {}, label: "A" });
    await enqueueRequest({ path: "/api/2", method: "POST", body: {}, label: "B" });
    angemeldet("ben");
    await enqueueRequest({ path: "/api/3", method: "POST", body: {}, label: "C" });

    expect(await listOwnQueuedRequests()).toHaveLength(1);
    angemeldet("anna");
    expect(await listOwnQueuedRequests()).toHaveLength(2);
  });

  it("entfernt bei der Kontolöschung nur die Einträge dieser Person", async () => {
    angemeldet("anna");
    await enqueueRequest({ path: "/api/1", method: "POST", body: {}, label: "A" });
    angemeldet("ben");
    await enqueueRequest({ path: "/api/2", method: "POST", body: {}, label: "B" });

    await removeQueuedRequestsOf("anna");

    expect((await listQueuedRequests()).map((i) => i.userId)).toEqual(["ben"]);
  });
});

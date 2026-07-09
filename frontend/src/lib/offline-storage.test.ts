// fake-indexeddb/auto ersetzt globalThis.indexedDB durch eine In-Memory-
// Implementierung - MUSS vor den Modulen importiert werden, die indexedDB
// beim Aufruf verwenden.
import "fake-indexeddb/auto";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import { enqueueRequest, listQueuedRequests, syncQueuedRequests } from "@/lib/offline-queue";
import { api } from "@/lib/api";

// api wird nur von syncQueuedRequests genutzt - komplett mocken, damit kein
// fetch stattfindet.
vi.mock("@/lib/api", () => ({
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
});

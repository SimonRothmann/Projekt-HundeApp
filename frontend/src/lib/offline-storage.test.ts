// fake-indexeddb/auto ersetzt globalThis.indexedDB durch eine In-Memory-
// Implementierung - MUSS vor den Modulen importiert werden, die indexedDB
// beim Aufruf verwenden.
import "fake-indexeddb/auto";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getCachedData, leseCacheLeeren, setCachedData } from "@/lib/read-cache";
import {
  MAX_VERSUCHE,
  abstandNachVersuch,
  eintragErneutSenden,
  eintragVerwerfen,
  enqueueRequest,
  listOwnQueuedRequests,
  listProblemRequests,
  zustandVon,
  listQueuedRequests,
  nichtGleichzeitig,
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

/** Queue leeren, auch abgelehnte und hängende Einträge (die gehen nicht mehr von selbst hinaus). */
async function leeren() {
  for (const item of await listQueuedRequests()) await eintragVerwerfen(item.id);
}

describe("offline-queue", () => {
  beforeEach(async () => {
    await leeren();
    vi.resetAllMocks();
    vi.mocked(api.post).mockResolvedValue(undefined);
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

  it("behält vom Server abgelehnte Einträge (4xx), markiert sie und synchronisiert den Rest weiter", async () => {
    vi.mocked(api.post)
      .mockRejectedValueOnce(new ApiError(404, ["Training nicht gefunden."]))
      .mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/gps-tracks", method: "POST", body: {}, label: "Fährte" });
    await enqueueRequest({ path: "/api/trainings", method: "POST", body: {}, label: "Training" });

    const failed: string[] = [];
    const synced = await syncQueuedRequests(undefined, (item, _fehler, art) => failed.push(`${item.label}:${art}`));

    // 404 blockiert die Queue nicht: Der nachfolgende Eintrag geht hinaus, der abgelehnte BLEIBT.
    expect(synced).toBe(1);
    expect(failed).toEqual(["Fährte:abgelehnt"]);
    const uebrig = await listQueuedRequests();
    expect(uebrig).toHaveLength(1);
    expect(uebrig[0].label).toBe("Fährte");
    expect(zustandVon(uebrig[0])).toBe("abgelehnt");
    expect(uebrig[0].abgelehnt).toEqual({ status: 404, meldung: "Training nicht gefunden." });
    expect(await listProblemRequests()).toHaveLength(1);
  });

  it("sendet einen abgelehnten Eintrag nicht von selbst noch einmal", async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new ApiError(400, ["Ungültig."])).mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/a", method: "POST", body: {}, label: "A" });
    await syncQueuedRequests();
    vi.mocked(api.post).mockClear();

    expect(await syncQueuedRequests()).toBe(0);
    expect(api.post).not.toHaveBeenCalled();
  });

  it("Erneut senden nimmt den Eintrag beim nächsten Abgleich wieder mit - und löscht ihn erst nach 2xx", async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new ApiError(400, ["Ungültig."])).mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/a", method: "POST", body: {}, label: "A" });
    await syncQueuedRequests();
    const [item] = await listQueuedRequests();

    await eintragErneutSenden(item.id);
    expect(zustandVon((await listQueuedRequests())[0])).toBe("wartet");
    expect(await syncQueuedRequests()).toBe(1);
    expect(await listQueuedRequests()).toHaveLength(0);
  });

  it("Verwerfen entfernt den Eintrag endgültig", async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new ApiError(400, ["Ungültig."]));
    await enqueueRequest({ path: "/api/a", method: "POST", body: {}, label: "A" });
    await syncQueuedRequests();

    await eintragVerwerfen((await listQueuedRequests())[0].id);
    expect(await listQueuedRequests()).toHaveLength(0);
  });

  it("löscht die Kopie der Aufzeichnung erst nach der Bestätigung des Servers", async () => {
    const speicher = new Map<string, string>([["dogity_aufzeichnung_wartend:faehrte:t1", "{}"]]);
    vi.stubGlobal("window", {
      localStorage: { getItem: (k: string) => speicher.get(k) ?? null, removeItem: (k: string) => speicher.delete(k) },
    });
    try {
      vi.mocked(api.post).mockRejectedValueOnce(new ApiError(404, ["Hund nicht gefunden."])).mockResolvedValue(undefined);
      await enqueueRequest({
        path: "/api/gps-tracks",
        method: "POST",
        body: {},
        label: "Fährte",
        sicherung: "dogity_aufzeichnung_wartend:faehrte:t1",
      });

      await syncQueuedRequests();
      // Abgelehnt: Eintrag UND Kopie bleiben.
      expect(speicher.size).toBe(1);

      await eintragErneutSenden((await listQueuedRequests())[0].id);
      await syncQueuedRequests();
      expect(await listQueuedRequests()).toHaveLength(0);
      expect(speicher.size).toBe(0);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("wiederholt nach einem Serverfehler (5xx) mit wachsendem Abstand und blockiert nichts dahinter", async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new ApiError(500, ["Fehler."])).mockResolvedValue(undefined);
    await enqueueRequest({ path: "/api/a", method: "POST", body: {}, label: "A" });
    await enqueueRequest({ path: "/api/b", method: "POST", body: {}, label: "B" });

    const jetzt = 1_000_000;
    expect(await syncQueuedRequests(undefined, undefined, jetzt)).toBe(1);

    const [a] = await listQueuedRequests();
    expect(a.label).toBe("A");
    expect(a.versuche).toBe(1);
    expect(a.naechsterVersuch).toBe(jetzt + abstandNachVersuch(1));
    expect(zustandVon(a)).toBe("wartet");

    // Vor Ablauf der Wartezeit wird er nicht versucht ...
    vi.mocked(api.post).mockClear();
    expect(await syncQueuedRequests(undefined, undefined, jetzt + 1000)).toBe(0);
    expect(api.post).not.toHaveBeenCalled();
    // ... danach schon.
    expect(await syncQueuedRequests(undefined, undefined, jetzt + abstandNachVersuch(1))).toBe(1);
    expect(await listQueuedRequests()).toHaveLength(0);
  });

  it("wächst der Abstand nach jedem Serverfehler, bis zu einer Obergrenze", () => {
    expect(abstandNachVersuch(1)).toBe(30_000);
    expect(abstandNachVersuch(2)).toBe(60_000);
    expect(abstandNachVersuch(3)).toBe(120_000);
    expect(abstandNachVersuch(50)).toBe(30 * 60_000);
  });

  it("markiert einen Eintrag nach zu vielen Serverfehlern als hängend und meldet es einmal", async () => {
    vi.mocked(api.post).mockRejectedValue(new ApiError(500, ["Fehler."]));
    await enqueueRequest({ path: "/api/a", method: "POST", body: {}, label: "A" });

    const gemeldet: string[] = [];
    let jetzt = 0;
    for (let i = 0; i < MAX_VERSUCHE + 2; i++) {
      jetzt += 60 * 60_000;
      await syncQueuedRequests(undefined, (item, _f, art) => gemeldet.push(`${item.label}:${art}`), jetzt);
    }

    const [a] = await listQueuedRequests();
    expect(zustandVon(a)).toBe("haengt");
    expect(a.versuche).toBe(MAX_VERSUCHE);
    expect(gemeldet).toEqual(["A:haengt"]);
    expect(api.post).toHaveBeenCalledTimes(MAX_VERSUCHE);
    expect(await listProblemRequests()).toHaveLength(1);
  });

  it("zählt Netzfehler und 502/503 nicht als Serverfehler des Eintrags, hält aber den Lauf an", async () => {
    vi.mocked(api.post).mockRejectedValue(new ApiError(503, []));
    await enqueueRequest({ path: "/api/a", method: "POST", body: {}, label: "A" });
    await enqueueRequest({ path: "/api/b", method: "POST", body: {}, label: "B" });

    expect(await syncQueuedRequests()).toBe(0);

    const items = await listQueuedRequests();
    expect(items.map((i) => i.versuche)).toEqual([undefined, undefined]);
    expect(api.post).toHaveBeenCalledTimes(1);
  });

  describe("Abhängigkeiten", () => {
    const TRACK = "11111111-1111-4111-8111-111111111111";
    const LAUF = "22222222-2222-4222-8222-222222222222";
    const NOTIZ = "33333333-3333-4333-8333-333333333333";

    it("ein Ablauf wartet auf seine noch nicht angelegte Fährte - und geht danach im selben Lauf hinaus", async () => {
      await enqueueRequest({ path: "/api/gps-tracks", method: "POST", body: { id: TRACK }, label: "Fährte" });
      await enqueueRequest({ path: `/api/gps-tracks/${TRACK}/walk-runs`, method: "POST", body: { id: LAUF }, label: "Ablauf" });
      await enqueueRequest({ path: "/api/trainings", method: "POST", body: {}, label: "Training" });
      vi.mocked(api.post).mockRejectedValueOnce(new ApiError(500, ["Fehler."])).mockResolvedValue(undefined);

      // Fährte scheitert mit 500: Der Ablauf wartet, das unabhängige Training geht hinaus.
      expect(await syncQueuedRequests(undefined, undefined, 0)).toBe(1);
      expect(vi.mocked(api.post).mock.calls.map((c) => c[0])).toEqual(["/api/gps-tracks", "/api/trainings"]);
      expect((await listQueuedRequests()).map((i) => i.label)).toEqual(["Fährte", "Ablauf"]);

      // Nach der Wartezeit: erst die Fährte, im selben Lauf dann der Ablauf.
      vi.mocked(api.post).mockClear();
      expect(await syncQueuedRequests(undefined, undefined, 10 * 60_000)).toBe(2);
      expect(vi.mocked(api.post).mock.calls.map((c) => c[0])).toEqual(["/api/gps-tracks", `/api/gps-tracks/${TRACK}/walk-runs`]);
    });

    it("ein Ablauf wartet auch auf eine abgelehnte oder hängende Fährte (er könnte nur mit 404 scheitern)", async () => {
      vi.mocked(api.post).mockRejectedValueOnce(new ApiError(400, ["Ungültig."])).mockResolvedValue(undefined);
      await enqueueRequest({ path: "/api/gps-tracks", method: "POST", body: { id: TRACK }, label: "Fährte" });
      await enqueueRequest({ path: `/api/gps-tracks/${TRACK}/walk-runs`, method: "POST", body: { id: LAUF }, label: "Ablauf" });

      expect(await syncQueuedRequests()).toBe(0);
      expect(api.post).toHaveBeenCalledTimes(1);
      expect(await syncQueuedRequests()).toBe(0);
      expect(api.post).toHaveBeenCalledTimes(1);
    });

    it("ein Training mit Merkzettel-Haken wartet, solange der Merkzettel-Eintrag noch versucht wird - nicht, wenn er hängt", async () => {
      vi.mocked(api.post).mockRejectedValueOnce(new ApiError(500, ["Fehler."])).mockResolvedValue(undefined);
      await enqueueRequest({ path: "/api/dogs/x/merkzettel", method: "POST", body: { id: NOTIZ }, label: "Merkzettel" });
      await enqueueRequest({ path: "/api/trainings", method: "POST", body: { erledigteMerkzettelIds: [NOTIZ] }, label: "Training" });

      // Merkzettel in Wartezeit: das Training wartet mit.
      expect(await syncQueuedRequests(undefined, undefined, 0)).toBe(0);
      expect(api.post).toHaveBeenCalledTimes(1);

      // Ein hängender Merkzettel hält das Training nicht mehr auf.
      const [merk] = await listQueuedRequests();
      vi.mocked(api.post).mockRejectedValue(new ApiError(500, ["Fehler."]));
      let jetzt = 0;
      for (let i = 0; i < MAX_VERSUCHE; i++) {
        jetzt += 60 * 60_000;
        await syncQueuedRequests(undefined, undefined, jetzt);
      }
      expect(zustandVon((await listQueuedRequests()).find((i) => i.id === merk.id)!)).toBe("haengt");
      vi.mocked(api.post).mockReset();
      vi.mocked(api.post).mockResolvedValue(undefined);
      expect(await syncQueuedRequests(undefined, undefined, jetzt + 60 * 60_000)).toBe(1);
      expect((await listQueuedRequests()).map((i) => i.label)).toEqual(["Merkzettel"]);
    });
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
    await leeren();
    vi.resetAllMocks();
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

describe("nichtGleichzeitig", () => {
  it("lässt einen Auslöser verfallen, solange der Lauf noch geht", async () => {
    let fertig!: () => void;
    const lauf = vi.fn(() => new Promise<void>((resolve) => (fertig = resolve)));
    const gesperrt = nichtGleichzeitig(lauf);

    const erster = gesperrt();
    await gesperrt();
    expect(lauf).toHaveBeenCalledTimes(1);

    fertig();
    await erster;
  });

  it("gibt die Sperre nach dem Lauf wieder frei - auch nach einem Fehler", async () => {
    const lauf = vi.fn().mockRejectedValueOnce(new Error("kaputt")).mockResolvedValue(undefined);
    const gesperrt = nichtGleichzeitig(lauf);

    await expect(gesperrt()).rejects.toThrow("kaputt");
    await gesperrt();
    expect(lauf).toHaveBeenCalledTimes(2);
  });

  it("verfällt, wenn ein anderer Tab die Sperre hält", async () => {
    const request = vi.fn(async (_name: string, _opt: unknown, cb: (sperre: unknown) => Promise<void>) => cb(null));
    vi.stubGlobal("navigator", { locks: { request } });
    try {
      const lauf = vi.fn().mockResolvedValue(undefined);
      await nichtGleichzeitig(lauf)();
      expect(request).toHaveBeenCalledWith("dogity-offline-sync", { ifAvailable: true }, expect.any(Function));
      expect(lauf).not.toHaveBeenCalled();
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("läuft, wenn die Sperre frei ist", async () => {
    const request = vi.fn(async (_name: string, _opt: unknown, cb: (sperre: unknown) => Promise<void>) => cb({}));
    vi.stubGlobal("navigator", { locks: { request } });
    try {
      const lauf = vi.fn().mockResolvedValue(undefined);
      await nichtGleichzeitig(lauf)();
      expect(lauf).toHaveBeenCalledTimes(1);
    } finally {
      vi.unstubAllGlobals();
    }
  });
});

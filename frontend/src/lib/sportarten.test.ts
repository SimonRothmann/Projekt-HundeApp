import { beforeEach, describe, expect, it, vi } from "vitest";

const get = vi.fn();
vi.mock("@/lib/api", () => ({ api: { get: (...a: unknown[]) => get(...a) } }));

import { ladeSportarten, sportartenCacheLeeren } from "./sportarten";

const liste = [{ id: "1", name: "BH" }];

describe("ladeSportarten", () => {
  beforeEach(() => {
    get.mockReset();
    sportartenCacheLeeren();
  });

  it("holt die Liste bei zwei Aufrufen nur einmal", async () => {
    get.mockResolvedValue(liste);
    const [a, b] = await Promise.all([ladeSportarten(), ladeSportarten()]);
    await ladeSportarten();
    expect(a).toBe(liste);
    expect(b).toBe(liste);
    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith("/api/sports");
  });

  it("lädt nach einem Fehler beim nächsten Aufruf neu", async () => {
    get.mockRejectedValueOnce(new Error("offline")).mockResolvedValueOnce(liste);
    await expect(ladeSportarten()).rejects.toThrow("offline");
    await expect(ladeSportarten()).resolves.toBe(liste);
    expect(get).toHaveBeenCalledTimes(2);
  });

  it("lädt nach dem Leeren neu", async () => {
    get.mockResolvedValue(liste);
    await ladeSportarten();
    sportartenCacheLeeren();
    await ladeSportarten();
    expect(get).toHaveBeenCalledTimes(2);
  });

  it("lädt nach fünf Minuten neu", async () => {
    vi.useFakeTimers();
    try {
      get.mockResolvedValue(liste);
      await ladeSportarten();
      vi.advanceTimersByTime(4 * 60_000);
      await ladeSportarten();
      expect(get).toHaveBeenCalledTimes(1);
      vi.advanceTimersByTime(2 * 60_000);
      await ladeSportarten();
      expect(get).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });
});

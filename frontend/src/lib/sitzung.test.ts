import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ABGEMELDET_KEY, REFRESH_KEY, TOKEN_KEY, USER_KEY, ZUGRIFF_BIS_KEY } from "@/lib/api";
import type { AuthResponse } from "@/lib/types";

// Meldet sich jemand anderes an als zuletzt, dürfen Zwischenspeicher und Push der vorigen Person
// nicht bleiben - auch wenn deren Sitzung nur abgelaufen war und kein Abmelden aufgeräumt hat.

const { leseCacheLeeren, pushLokalLoesen } = vi.hoisted(() => ({
  leseCacheLeeren: vi.fn(async () => {}),
  pushLokalLoesen: vi.fn(async () => {}),
}));
vi.mock("@/lib/read-cache", () => ({ leseCacheLeeren }));
vi.mock("@/lib/push", () => ({ pushLokalLoesen }));

import { sitzungSpeichern } from "@/lib/sitzung";

let werte: Map<string, string>;

function antwort(userId: string): AuthResponse {
  return { userId, email: `${userId}@example.org`, firstName: "Ida", lastName: "Test", roles: ["User"], accessExpiresAt: `bis-${userId}` };
}

beforeEach(() => {
  werte = new Map();
  leseCacheLeeren.mockClear();
  pushLokalLoesen.mockClear();
  vi.stubGlobal("window", {
    localStorage: {
      getItem: (k: string) => werte.get(k) ?? null,
      setItem: (k: string, v: string) => werte.set(k, v),
      removeItem: (k: string) => werte.delete(k),
    },
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("sitzungSpeichern", () => {
  it("legt Nutzer und Ablaufzeit ab und gibt den Nutzer ohne Ablaufzeit zurück", async () => {
    const nutzer = await sitzungSpeichern(antwort("a"));

    expect(nutzer).toEqual({ userId: "a", email: "a@example.org", firstName: "Ida", lastName: "Test", roles: ["User"] });
    expect(werte.get(ZUGRIFF_BIS_KEY)).toBe("bis-a");
    expect(JSON.parse(werte.get(USER_KEY)!)).toEqual(nutzer);
  });

  it("räumt alte Schlüssel und einen offenen Abmelde-Merker der vorigen Sitzung weg", async () => {
    werte.set(TOKEN_KEY, "alt");
    werte.set(REFRESH_KEY, "alt");
    werte.set(ABGEMELDET_KEY, "1");

    await sitzungSpeichern(antwort("a"));

    expect([...werte.keys()].sort()).toEqual(["dogity_letzte_person", USER_KEY, ZUGRIFF_BIS_KEY].sort());
  });

  it("leert bei einer anderen Person Zwischenspeicher und Push, vor dem Ablegen der neuen Sitzung", async () => {
    await sitzungSpeichern(antwort("a"));
    leseCacheLeeren.mockClear();
    pushLokalLoesen.mockClear();
    let ablaufBeimLeeren: string | undefined;
    leseCacheLeeren.mockImplementationOnce(async () => {
      ablaufBeimLeeren = werte.get(ZUGRIFF_BIS_KEY);
    });

    await sitzungSpeichern(antwort("b"));

    expect(leseCacheLeeren).toHaveBeenCalledOnce();
    expect(pushLokalLoesen).toHaveBeenCalledOnce();
    // Abgewartet, bevor die neue Sitzung gilt: die ersten Seiten lesen den Speicher erst danach.
    expect(ablaufBeimLeeren).toBe("bis-a");
    expect(werte.get(ZUGRIFF_BIS_KEY)).toBe("bis-b");
  });

  it("lässt bei derselben Person alles stehen (abgelaufene Sitzung, erneute Anmeldung)", async () => {
    await sitzungSpeichern(antwort("a"));
    leseCacheLeeren.mockClear();
    pushLokalLoesen.mockClear();

    await sitzungSpeichern(antwort("a"));

    expect(leseCacheLeeren).not.toHaveBeenCalled();
    expect(pushLokalLoesen).not.toHaveBeenCalled();
  });

  it("leert bei der ersten Anmeldung auf dem Gerät (kein Merker)", async () => {
    await sitzungSpeichern(antwort("a"));

    expect(leseCacheLeeren).toHaveBeenCalledOnce();
  });

  it("kommt auch ohne nutzbaren Merker (localStorage verweigert ihn) zur Anmeldung und leert einmal zu viel", async () => {
    const speicher = (window as unknown as { localStorage: Record<string, unknown> }).localStorage;
    const setzen = speicher.setItem as (k: string, v: string) => void;
    speicher.getItem = (k: string) => {
      if (k === "dogity_letzte_person") throw new Error("gesperrt");
      return werte.get(k) ?? null;
    };
    speicher.setItem = (k: string, v: string) => {
      if (k === "dogity_letzte_person") throw new Error("gesperrt");
      setzen(k, v);
    };

    await sitzungSpeichern(antwort("a"));
    await sitzungSpeichern(antwort("a"));

    expect(leseCacheLeeren).toHaveBeenCalledTimes(2);
    expect(werte.get(ZUGRIFF_BIS_KEY)).toBe("bis-a");
  });
});

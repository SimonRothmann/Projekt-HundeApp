import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { REFRESH_KEY, TOKEN_KEY, USER_KEY } from "@/lib/api";
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
  return { token: `jwt-${userId}`, refreshToken: `refresh-${userId}`, userId, email: `${userId}@example.org`, firstName: "Ida", lastName: "Test", roles: ["User"] };
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
  it("legt Token und Nutzer ab und gibt den Nutzer ohne Token zurück", async () => {
    const nutzer = await sitzungSpeichern(antwort("a"));

    expect(nutzer).toEqual({ userId: "a", email: "a@example.org", firstName: "Ida", lastName: "Test", roles: ["User"] });
    expect(werte.get(TOKEN_KEY)).toBe("jwt-a");
    expect(werte.get(REFRESH_KEY)).toBe("refresh-a");
    expect(JSON.parse(werte.get(USER_KEY)!)).toEqual(nutzer);
  });

  it("leert bei einer anderen Person Zwischenspeicher und Push, vor dem Ablegen der neuen Sitzung", async () => {
    await sitzungSpeichern(antwort("a"));
    leseCacheLeeren.mockClear();
    pushLokalLoesen.mockClear();
    let tokenBeimLeeren: string | undefined;
    leseCacheLeeren.mockImplementationOnce(async () => {
      tokenBeimLeeren = werte.get(TOKEN_KEY);
    });

    await sitzungSpeichern(antwort("b"));

    expect(leseCacheLeeren).toHaveBeenCalledOnce();
    expect(pushLokalLoesen).toHaveBeenCalledOnce();
    // Abgewartet, bevor die neue Sitzung gilt: die ersten Seiten lesen den Speicher erst danach.
    expect(tokenBeimLeeren).toBe("jwt-a");
    expect(werte.get(TOKEN_KEY)).toBe("jwt-b");
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
    expect(werte.get(TOKEN_KEY)).toBe("jwt-a");
  });
});

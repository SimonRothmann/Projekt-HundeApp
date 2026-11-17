import { afterEach, describe, expect, it, vi } from "vitest";
import { direktZumHund, hundeseiteVomReiter, leseZuletztHund, merkeZuletztHund } from "./hundeliste";
import type { Dog } from "./types";

const hund = (id: string, archiviert = false) => ({ id, name: id, archivedAt: archiviert ? "2026-01-01T00:00:00Z" : null }) as unknown as Dog;
const ohne = { zuletzt: null, listeGewuenscht: false };

describe("direktZumHund", () => {
  it("führt bei genau einem aktiven Hund auf dessen Seite", () => {
    expect(direktZumHund([hund("a")], ohne)).toBe("a");
  });

  it("zählt archivierte Hunde nicht mit", () => {
    expect(direktZumHund([hund("a"), hund("b", true)], ohne)).toBe("a");
  });

  it("führt bei mehreren Hunden ohne Merker zum ersten aktiven", () => {
    expect(direktZumHund([hund("a", true), hund("b"), hund("c")], ohne)).toBe("b");
  });

  it("führt zum zuletzt angesehenen Hund, wenn er noch aktiv ist", () => {
    expect(direktZumHund([hund("a"), hund("b")], { zuletzt: "b", listeGewuenscht: false })).toBe("b");
  });

  it("übergeht einen zuletzt angesehenen Hund, der archiviert, gelöscht oder fremd ist", () => {
    expect(direktZumHund([hund("a"), hund("b", true)], { zuletzt: "b", listeGewuenscht: false })).toBe("a");
    expect(direktZumHund([hund("a")], { zuletzt: "weg", listeGewuenscht: false })).toBe("a");
  });

  it("bleibt ohne aktiven Hund auf der Seite (Leerzustand)", () => {
    expect(direktZumHund([], ohne)).toBeNull();
    expect(direktZumHund([hund("a", true)], ohne)).toBeNull();
  });

  it("bleibt in der Liste, wenn sie ausdrücklich verlangt ist", () => {
    expect(direktZumHund([hund("a")], { zuletzt: "a", listeGewuenscht: true })).toBeNull();
  });
});

describe("hundeseiteVomReiter", () => {
  it("markiert die Herkunft Reiter über ?from=", () => {
    expect(hundeseiteVomReiter("abc")).toBe("/dogs/abc?from=%2Fdogs");
  });
});

describe("zuletzt angesehener Hund", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("merkt sich die Kennung und liest sie wieder", () => {
    const speicher = new Map<string, string>();
    vi.stubGlobal("window", {
      localStorage: { getItem: (k: string) => speicher.get(k) ?? null, setItem: (k: string, v: string) => void speicher.set(k, v) },
    });
    expect(leseZuletztHund()).toBeNull();
    merkeZuletztHund("a");
    expect(leseZuletztHund()).toBe("a");
  });

  it("übersteht einen gesperrten Speicher", () => {
    const wirft = () => {
      throw new Error("gesperrt");
    };
    vi.stubGlobal("window", { localStorage: { getItem: wirft, setItem: wirft } });
    expect(() => merkeZuletztHund("a")).not.toThrow();
    expect(leseZuletztHund()).toBeNull();
  });
});

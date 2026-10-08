import { afterEach, describe, expect, it, vi } from "vitest";
import { leseGelesen, merkeGelesen, merkeGelesenImErststart, sollHinweisZeigen } from "./neuerungen-gelesen";
import { AKTUELLE_VERSION } from "./versionshinweise";

describe("sollHinweisZeigen", () => {
  it("zeigt nichts, wenn die laufende Fassung schon gesehen wurde", () => {
    expect(sollHinweisZeigen("0.9", "0.9", false)).toBe(false);
  });

  it("zeigt den Hinweis nach einer neuen Fassung wieder", () => {
    expect(sollHinweisZeigen("0.8", "0.9", false)).toBe(true);
  });

  it("zeigt ihn auch, wenn noch nie etwas vermerkt wurde", () => {
    expect(sollHinweisZeigen(null, "0.9", false)).toBe(true);
  });

  it("schweigt während des Erststarts - für Neulinge ist alles neu", () => {
    expect(sollHinweisZeigen(null, "0.9", true)).toBe(false);
    expect(sollHinweisZeigen("0.8", "0.9", true)).toBe(false);
  });
});

describe("Gelesen-Vermerk", () => {
  function fensterMit(speicher: Record<string, string>, kaputt = false) {
    const ereignisse: string[] = [];
    vi.stubGlobal("window", {
      localStorage: {
        getItem: (k: string) => {
          if (kaputt) throw new Error("gesperrt");
          return speicher[k] ?? null;
        },
        setItem: (k: string, v: string) => {
          if (kaputt) throw new Error("gesperrt");
          speicher[k] = v;
        },
      },
      dispatchEvent: (e: Event) => {
        ereignisse.push(e.type);
        return true;
      },
    });
    return ereignisse;
  }

  afterEach(() => vi.unstubAllGlobals());

  it("merkt sich die Fassung und meldet die Änderung", () => {
    const ereignisse = fensterMit({});
    expect(leseGelesen()).toBeNull();
    merkeGelesen("0.9");
    expect(leseGelesen()).toBe("0.9");
    expect(ereignisse).toEqual(["dogity:neuerungen-gelesen"]);
  });

  it("stürzt ohne Speicher nicht ab", () => {
    fensterMit({}, true);
    expect(leseGelesen()).toBeNull();
    expect(() => merkeGelesen("0.9")).not.toThrow();
  });

  it("vermerkt im Erststart still die aktuelle Fassung, sonst nichts", () => {
    const speicher: Record<string, string> = {};
    fensterMit(speicher);
    merkeGelesenImErststart(false);
    expect(leseGelesen()).toBeNull();
    merkeGelesenImErststart(true);
    expect(leseGelesen()).toBe(AKTUELLE_VERSION);
  });
});

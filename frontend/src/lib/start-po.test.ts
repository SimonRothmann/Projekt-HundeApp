import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  findeStartPo,
  istGueltigerPoSlug,
  leseStartPo,
  loescheStartPo,
  merkeStartPo,
  poSlugAus,
  START_PO_SCHLUESSEL,
  type SportMitOrdnungen,
} from "./start-po";

function sport(id: string, name: string, clubId: string | null = null) {
  return { id, code: name, name, description: null, clubId };
}

function ordnung(id: string, name: string) {
  return { id, name, sourceUrl: null, lastSyncedAt: null, latestKnownVersionLabel: null, description: null };
}

const katalog: SportMitOrdnungen[] = [
  { sport: sport("s-bh", "BH"), regulations: [ordnung("aaaaaaaa-1", "BH")] },
  { sport: sport("s-ibgh1", "IBGH1"), regulations: [ordnung("bbbbbbbb-1", "IBGH1")] },
  {
    sport: sport("s-igp", "IGP"),
    regulations: [ordnung("cccccccc-1", "Fährte A"), ordnung("dddddddd-2", "Fährte A")],
  },
  { sport: sport("s-club", "Vereinssport", "club-1"), regulations: [ordnung("eeeeeeee-1", "Geheim")] },
];

describe("Kürzel der Prüfungsordnung", () => {
  it.each(["bh", "ibgh1", "fci-igp-1", "a1"])("erkennt %s als gültig", (slug) => {
    expect(istGueltigerPoSlug(slug)).toBe(true);
  });

  it.each([null, undefined, "", "BH", "a b", "a/b", "../x", "-x", "x-", "a--b", "ä", "a".repeat(81)])(
    "weist %s ab",
    (wert) => {
      expect(istGueltigerPoSlug(wert)).toBe(false);
      expect(poSlugAus(wert)).toBeNull();
    },
  );
});

describe("Merken auf dem Gerät", () => {
  beforeEach(() => {
    const speicher = new Map<string, string>();
    vi.stubGlobal("window", {
      localStorage: {
        getItem: (k: string) => speicher.get(k) ?? null,
        setItem: (k: string, v: string) => void speicher.set(k, v),
        removeItem: (k: string) => void speicher.delete(k),
      },
    });
  });

  it("merkt, liest und löscht das Kürzel", () => {
    merkeStartPo("ibgh1");
    expect(leseStartPo()).toBe("ibgh1");
    loescheStartPo();
    expect(leseStartPo()).toBeNull();
  });

  it("merkt kein unbrauchbares Kürzel", () => {
    merkeStartPo("../boese");
    expect(window.localStorage.getItem(START_PO_SCHLUESSEL)).toBeNull();
  });

  it("liest ein von außen eingeschleustes, unbrauchbares Kürzel nicht", () => {
    window.localStorage.setItem(START_PO_SCHLUESSEL, "<script>");
    expect(leseStartPo()).toBeNull();
  });

  it("übersteht einen gesperrten Speicher", () => {
    const werfen = () => {
      throw new Error("gesperrt");
    };
    vi.stubGlobal("window", { localStorage: { getItem: werfen, setItem: werfen, removeItem: werfen } });

    expect(() => merkeStartPo("bh")).not.toThrow();
    expect(leseStartPo()).toBeNull();
    expect(() => loescheStartPo()).not.toThrow();
  });
});

describe("findeStartPo", () => {
  it("findet Sportart und Prüfungsordnung zum Kürzel", () => {
    expect(findeStartPo("ibgh1", katalog)).toEqual({ sportId: "s-ibgh1", regulationId: "bbbbbbbb-1" });
  });

  it("vergibt Kürzel bei gleichen Namen wie der öffentliche Katalog: die zweite bekommt die Id", () => {
    expect(findeStartPo("faehrte-a", katalog)).toEqual({ sportId: "s-igp", regulationId: "cccccccc-1" });
    expect(findeStartPo("faehrte-a-dddddddd", katalog)).toEqual({ sportId: "s-igp", regulationId: "dddddddd-2" });
  });

  it("kennt vereinseigene Sportarten nicht", () => {
    expect(findeStartPo("geheim", katalog)).toBeNull();
  });

  it("liefert nichts für ein unbekanntes Kürzel", () => {
    expect(findeStartPo("gibt-es-nicht", katalog)).toBeNull();
  });
});

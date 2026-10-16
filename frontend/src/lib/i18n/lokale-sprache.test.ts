import { describe, expect, it } from "vitest";
import { leseLokaleSprache, SPRACH_SPEICHER, speichereLokaleSprache } from "./lokale-sprache";

/** Ein Speicher im Arbeitsspeicher - der Test läuft ohne Browser. */
function speicher(start: Record<string, string> = {}) {
  const daten = { ...start };
  return {
    daten,
    getItem: (schluessel: string) => daten[schluessel] ?? null,
    setItem: (schluessel: string, wert: string) => void (daten[schluessel] = wert),
    removeItem: (schluessel: string) => void delete daten[schluessel],
  };
}

describe("leseLokaleSprache", () => {
  it("gibt null zurück, solange nichts gewählt wurde", () => {
    expect(leseLokaleSprache(speicher())).toBeNull();
  });

  it("liest eine gespeicherte Wahl", () => {
    expect(leseLokaleSprache(speicher({ [SPRACH_SPEICHER]: "en" }))).toBe("en");
  });

  it("ignoriert Werte, die keine Sprache sind", () => {
    // Ein von Hand verbogener Eintrag oder eine Sprache aus einer späteren Fassung.
    expect(leseLokaleSprache(speicher({ [SPRACH_SPEICHER]: "klingonisch" }))).toBeNull();
  });

  it("übersteht einen Speicher, der wirft", () => {
    const kaputt = {
      getItem: () => {
        throw new Error("gesperrt");
      },
      setItem: () => {},
      removeItem: () => {},
    };
    expect(leseLokaleSprache(kaputt)).toBeNull();
  });

  it("übersteht fehlenden Speicher (Server-Rendern, gesperrte Website-Daten)", () => {
    expect(leseLokaleSprache(null)).toBeNull();
  });
});

describe("speichereLokaleSprache", () => {
  it("legt die Wahl ab", () => {
    const s = speicher();
    speichereLokaleSprache("en", s);
    expect(s.daten[SPRACH_SPEICHER]).toBe("en");
    expect(leseLokaleSprache(s)).toBe("en");
  });

  it("wirft nicht, wenn das Speichern scheitert", () => {
    const voll = {
      getItem: () => null,
      setItem: () => {
        throw new Error("Kontingent erschöpft");
      },
      removeItem: () => {},
    };
    expect(() => speichereLokaleSprache("de", voll)).not.toThrow();
  });
});

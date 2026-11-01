import { afterEach, describe, expect, it, vi } from "vitest";
import { merkeWeggeklickt, warWeggeklickt } from "./weggeklickt";

function fensterMit(speicher: Record<string, string>, kaputt = false) {
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
  });
}

describe("Weggeklickt-Vermerk", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("merkt sich je Schlüssel, dass der Hinweis weg ist", () => {
    fensterMit({});
    expect(warWeggeklickt("a")).toBe(false);
    merkeWeggeklickt("a");
    expect(warWeggeklickt("a")).toBe(true);
    expect(warWeggeklickt("b")).toBe(false);
  });

  it("wirft nie, wenn der Speicher gesperrt ist - der Hinweis kommt dann wieder", () => {
    fensterMit({}, true);
    expect(() => merkeWeggeklickt("a")).not.toThrow();
    expect(warWeggeklickt("a")).toBe(false);
  });
});

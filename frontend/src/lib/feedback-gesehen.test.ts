import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  eintragIdAus,
  feedbackKennung,
  GESEHEN_MAX,
  GESEHEN_SCHLUESSEL,
  istNeuesFeedback,
  leseGesehen,
  loescheGesehen,
  markiereGesehen,
  mitKennung,
} from "./feedback-gesehen";

const ID = "8b0c2c9e-1d3a-4f57-9a64-2f0d6a1b7c11";
const AM = "2026-09-30T10:00:00+00:00";

function speicherAnlegen() {
  const speicher = new Map<string, string>();
  vi.stubGlobal("window", {
    localStorage: {
      getItem: (k: string) => speicher.get(k) ?? null,
      setItem: (k: string, v: string) => void speicher.set(k, v),
      removeItem: (k: string) => void speicher.delete(k),
    },
  });
  return speicher;
}

describe("eintragIdAus", () => {
  it("akzeptiert eine GUID und liefert sie klein geschrieben", () => {
    expect(eintragIdAus(ID)).toBe(ID);
    expect(eintragIdAus(ID.toUpperCase())).toBe(ID);
  });

  it.each([null, undefined, "", "abc", "8b0c2c9e-1d3a-4f57-9a64", `${ID}x`, `../${ID}`, `${ID}#x`, "8b0c2c9e1d3a4f579a642f0d6a1b7c11", "zzzzzzzz-1d3a-4f57-9a64-2f0d6a1b7c11"])(
    "weist %s ab",
    (wert) => {
      expect(eintragIdAus(wert)).toBeNull();
    },
  );
});

describe("istNeuesFeedback", () => {
  const eintrag = { id: ID, trainerFeedback: "Gut!", feedbackAt: AM };

  it("ist neu, solange die Kennung nicht gemerkt ist", () => {
    expect(istNeuesFeedback(eintrag, new Set())).toBe(true);
    expect(istNeuesFeedback(eintrag, [])).toBe(true);
  });

  it("ist nicht mehr neu, wenn Eintrag und Zeitpunkt gemerkt sind", () => {
    const gesehen = new Set([feedbackKennung(ID, AM)]);
    expect(istNeuesFeedback(eintrag, gesehen)).toBe(false);
  });

  it("gilt überarbeitetes Feedback (anderer Zeitpunkt) wieder als neu", () => {
    const gesehen = new Set([feedbackKennung(ID, AM)]);
    expect(istNeuesFeedback({ ...eintrag, feedbackAt: "2026-10-02T09:00:00+00:00" }, gesehen)).toBe(true);
  });

  it("markiert nichts, wo es kein Feedback gibt", () => {
    expect(istNeuesFeedback({ id: ID, trainerFeedback: null, feedbackAt: null }, new Set())).toBe(false);
    expect(istNeuesFeedback({ id: ID, trainerFeedback: "Gut!", feedbackAt: null }, new Set())).toBe(false);
  });
});

describe("mitKennung", () => {
  it("hängt hinten an und kennt keine Doppelten", () => {
    expect(mitKennung(["a", "b"], "a")).toEqual(["b", "a"]);
  });

  it("kappt auf die Höchstzahl und lässt die ältesten fallen", () => {
    expect(mitKennung(["a", "b", "c"], "d", 3)).toEqual(["b", "c", "d"]);
  });
});

describe("Speicher auf dem Gerät", () => {
  beforeEach(() => {
    speicherAnlegen();
  });

  it("merkt sich ein Feedback und liest es wieder", () => {
    expect(leseGesehen()).toEqual([]);
    markiereGesehen(ID, AM);
    expect(leseGesehen()).toEqual([feedbackKennung(ID, AM)]);
  });

  it("schreibt dieselbe Kennung nicht doppelt", () => {
    markiereGesehen(ID, AM);
    markiereGesehen(ID, AM);
    expect(leseGesehen()).toHaveLength(1);
  });

  it("bleibt bei höchstens GESEHEN_MAX Kennungen", () => {
    for (let i = 0; i < GESEHEN_MAX + 25; i++) markiereGesehen(`id-${i}`, AM);
    const liste = leseGesehen();
    expect(liste).toHaveLength(GESEHEN_MAX);
    // Die neuesten bleiben.
    expect(liste[liste.length - 1]).toBe(feedbackKennung(`id-${GESEHEN_MAX + 24}`, AM));
    expect(liste).not.toContain(feedbackKennung("id-0", AM));
  });

  it("liest unbrauchbaren Inhalt als leere Liste", () => {
    const speicher = speicherAnlegen();
    speicher.set(GESEHEN_SCHLUESSEL, "{kaputt");
    expect(leseGesehen()).toEqual([]);
    speicher.set(GESEHEN_SCHLUESSEL, JSON.stringify({ a: 1 }));
    expect(leseGesehen()).toEqual([]);
    speicher.set(GESEHEN_SCHLUESSEL, JSON.stringify(["ok", 5, null]));
    expect(leseGesehen()).toEqual(["ok"]);
  });

  it("löscht die Liste", () => {
    markiereGesehen(ID, AM);
    loescheGesehen();
    expect(leseGesehen()).toEqual([]);
  });
});

describe("ohne Speicher", () => {
  it("wirft nie, wenn localStorage fehlt oder gesperrt ist", () => {
    vi.stubGlobal("window", {
      localStorage: {
        getItem: () => {
          throw new Error("gesperrt");
        },
        setItem: () => {
          throw new Error("gesperrt");
        },
        removeItem: () => {
          throw new Error("gesperrt");
        },
      },
    });
    expect(leseGesehen()).toEqual([]);
    expect(() => markiereGesehen(ID, AM)).not.toThrow();
    expect(() => loescheGesehen()).not.toThrow();
  });
});

import { describe, expect, it } from "vitest";
import {
  FAEHRTE_LEITFAEDEN,
  leitfadenFortschritt,
  leitfadenNachId,
  leitfadenZeile,
  liegezeitRest,
  vorgabeLeitfaden,
} from "./faehrte-leitfaden";

const ziel = (regulationName: string | null, status: 0 | 1 | 2 = 0, targetDate = "2027-01-01") => ({ regulationName, status, targetDate });
const deutsch = (text: string, werte?: Record<string, string | number>) =>
  text.replace(/\{(\w+)\}/g, (_, n: string) => String(werte?.[n] ?? ""));

describe("Leitfaden-Tabelle", () => {
  it("hat eindeutige Ids und Katalogkennungen", () => {
    const kennungen = FAEHRTE_LEITFAEDEN.flatMap((l) => l.seedKeys);
    expect(new Set(FAEHRTE_LEITFAEDEN.map((l) => l.id)).size).toBe(FAEHRTE_LEITFAEDEN.length);
    expect(new Set(kennungen).size).toBe(kennungen.length);
  });

  it("formuliert die Zeile aus den belegten Werten, ohne fehlende", () => {
    expect(leitfadenZeile(leitfadenNachId("igp1")!, deutsch)).toBe(
      "IGP 1: mind. 300 Schritte · 3 Schenkel · 2 Winkel · 3 Gegenstände · eigene Fährte · mind. 20 Min. Liegezeit",
    );
    // SKG BH 1: der Katalog nennt keine Schenkelzahl.
    expect(leitfadenZeile(leitfadenNachId("skgbh1")!, deutsch)).toBe(
      "SKG BH 1: ca. 150 Schritte · 1 Winkel · 1 Gegenstand · eigene Fährte · mind. 20 Min. Liegezeit",
    );
  });
});

describe("vorgabeLeitfaden", () => {
  it("nimmt die Stufe des aktiven Ziels, auch bei der Einzelprüfung", () => {
    expect(vorgabeLeitfaden([ziel("FCI-FPr 2")], "igp1")?.id).toBe("igp2");
  });

  it("überspringt erreichte Ziele und Ordnungen ohne Fährte, nimmt dann die gemerkte Stufe", () => {
    expect(vorgabeLeitfaden([ziel("FCI-IGP 3", 1), ziel("BH"), ziel(null)], "ifh1")?.id).toBe("ifh1");
  });

  it("nimmt bei mehreren Zielen das mit dem nächsten Termin", () => {
    expect(vorgabeLeitfaden([ziel("FCI-IGP 3", 0, "2027-06-01"), ziel("FCI-IGP 1", 0, "2027-03-01")], null)?.id).toBe("igp1");
  });

  it("liefert ohne Ziel und ohne gemerkte (oder unbekannte) Stufe keinen Leitfaden", () => {
    expect(vorgabeLeitfaden([], null)).toBeNull();
    expect(vorgabeLeitfaden(null, "gibtsnicht")).toBeNull();
  });
});

describe("leitfadenFortschritt", () => {
  const igp1 = leitfadenNachId("igp1")!;

  it("rechnet Meter mit der Schrittlänge in Schritte und zählt Gegenstand-Marker", () => {
    const f = leitfadenFortschritt(igp1, 90, 75, 1);
    expect(f).toEqual({ schritteIst: 120, schritteErreicht: false, gegenstaendeIst: 1, gegenstaendeErreicht: false });
  });

  it("meldet erreichte Ziele", () => {
    const f = leitfadenFortschritt(igp1, 230, null, 3);
    expect(f.schritteErreicht).toBe(true);
    expect(f.gegenstaendeErreicht).toBe(true);
  });

  it("beginnt bei null", () => {
    expect(leitfadenFortschritt(igp1, 0, 75, 0).schritteIst).toBe(0);
  });
});

describe("liegezeitRest", () => {
  it("zählt bis zur Mindest-Liegezeit herunter und bleibt dann bei 0", () => {
    const igp1 = leitfadenNachId("igp1")!;
    expect(liegezeitRest(igp1, 12)).toBe(8);
    expect(liegezeitRest(igp1, 20)).toBe(0);
    expect(liegezeitRest(igp1, 90)).toBe(0);
  });
});

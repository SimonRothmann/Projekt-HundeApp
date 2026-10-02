import { describe, expect, it } from "vitest";
import { SERIE_STANDARD_WOCHEN, serieStandardEnde, wochentagVon } from "./termin-serie";

describe("serieStandardEnde", () => {
  it("liegt zwölf Wochen nach dem Beginn", () => {
    expect(SERIE_STANDARD_WOCHEN).toBe(12);
    expect(serieStandardEnde("2026-10-06")).toBe("2026-12-29");
  });

  it("rechnet über Monats- und Jahreswechsel", () => {
    expect(serieStandardEnde("2026-11-10")).toBe("2027-02-02");
  });

  it("rechnet über die Zeitumstellung, ohne einen Tag zu verschieben", () => {
    expect(serieStandardEnde("2026-10-20")).toBe("2027-01-12");
    expect(serieStandardEnde("2027-03-01")).toBe("2027-05-24");
  });

  it("lässt ungültige Eingaben unverändert", () => {
    expect(serieStandardEnde("")).toBe("");
    expect(serieStandardEnde("morgen")).toBe("morgen");
  });
});

describe("wochentagVon", () => {
  it("zählt wie Date.getDay (0 = Sonntag)", () => {
    expect(wochentagVon("2026-10-04")).toBe(0);
    expect(wochentagVon("2026-10-05")).toBe(1);
    expect(wochentagVon("2026-10-06")).toBe(2);
    expect(wochentagVon("2026-10-10")).toBe(6);
  });

  it("liefert null, wenn es kein Datum ist", () => {
    expect(wochentagVon("")).toBeNull();
    expect(wochentagVon("06.10.2026")).toBeNull();
  });
});

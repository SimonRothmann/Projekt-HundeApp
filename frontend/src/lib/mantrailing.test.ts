import { describe, expect, it } from "vitest";
import {
  ANZEIGEVERHALTEN,
  ART_FAEHRTE,
  ART_MANTRAILING,
  FAEHRTE_GRENZEN,
  MANTRAILING_GRENZEN,
  anzeigeverhaltenLabel,
  istMantrailing,
  minutenAusStundenText,
  spurGrenzen,
  stundenText,
  trailAenderungen,
} from "./mantrailing";

describe("istMantrailing", () => {
  it("erkennt den Sportart-Code, die Sportart und die Art einer Spur", () => {
    expect(istMantrailing("MANTRAILING")).toBe(true);
    expect(istMantrailing({ code: "MANTRAILING" })).toBe(true);
    expect(istMantrailing({ kind: ART_MANTRAILING })).toBe(true);
  });

  it("hält alles andere für Fährte - auch alte Stände ohne Art", () => {
    expect(istMantrailing("FAERTE")).toBe(false);
    expect(istMantrailing({ code: "FPR" })).toBe(false);
    expect(istMantrailing({ kind: ART_FAEHRTE })).toBe(false);
    expect(istMantrailing({})).toBe(false);
    expect(istMantrailing({ kind: null })).toBe(false);
    expect(istMantrailing(null)).toBe(false);
    expect(istMantrailing(undefined)).toBe(false);
  });
});

describe("spurGrenzen", () => {
  it("Fährte 3/6 m, Mantrailing 10/25 m", () => {
    expect(FAEHRTE_GRENZEN).toEqual({ gruenBis: 3, gelbBis: 6 });
    expect(MANTRAILING_GRENZEN).toEqual({ gruenBis: 10, gelbBis: 25 });
    expect(spurGrenzen({ kind: ART_MANTRAILING })).toBe(MANTRAILING_GRENZEN);
    expect(spurGrenzen({ kind: ART_FAEHRTE })).toBe(FAEHRTE_GRENZEN);
  });

  it("nimmt ohne Angabe die Fährte - bestehende Läufe bleiben, wie sie waren", () => {
    expect(spurGrenzen()).toBe(FAEHRTE_GRENZEN);
    expect(spurGrenzen(null)).toBe(FAEHRTE_GRENZEN);
    expect(spurGrenzen({})).toBe(FAEHRTE_GRENZEN);
  });
});

describe("Anzeigeverhalten", () => {
  it("hat sechs Werte in der Reihenfolge des Backends", () => {
    expect(ANZEIGEVERHALTEN.map((a) => a.value)).toEqual([0, 1, 2, 3, 4, 5]);
    expect(anzeigeverhaltenLabel(1)).toBe("Vorsitzen");
  });

  it("liefert ohne Angabe nichts", () => {
    expect(anzeigeverhaltenLabel(null)).toBeNull();
    expect(anzeigeverhaltenLabel(undefined)).toBeNull();
    expect(anzeigeverhaltenLabel(99)).toBeNull();
  });
});

describe("Trailalter in Stunden", () => {
  it("zeigt Minuten als Stunden mit Komma", () => {
    expect(stundenText(90)).toBe("1,5");
    expect(stundenText(120)).toBe("2");
    expect(stundenText(75)).toBe("1,25");
    expect(stundenText(0)).toBe("0");
    expect(stundenText(null)).toBe("");
    expect(stundenText(90, "en")).toBe("1.5");
  });

  it("liest Stunden mit Komma oder Punkt", () => {
    expect(minutenAusStundenText("1,5")).toBe(90);
    expect(minutenAusStundenText("2")).toBe(120);
    expect(minutenAusStundenText(" 1.25 ")).toBe(75);
    expect(minutenAusStundenText("168")).toBe(168 * 60);
  });

  it("unterscheidet leer von falsch", () => {
    expect(minutenAusStundenText("")).toBeNull();
    expect(minutenAusStundenText("  ")).toBeNull();
    expect(minutenAusStundenText("abc")).toBeUndefined();
    expect(minutenAusStundenText("-1")).toBeUndefined();
    expect(minutenAusStundenText("1,5,2")).toBeUndefined();
    expect(minutenAusStundenText("169")).toBeUndefined();
  });
});

describe("trailAenderungen", () => {
  const spur = { ageMinutes: 90, doubleBlind: true, negativeTrail: false, scentArticle: "Mütze" };

  it("meldet nichts, wenn sich nichts geändert hat", () => {
    const lauf = { personFound: true, indication: 1 };
    expect(trailAenderungen(spur, lauf, { gefunden: true, anzeige: 1, alterText: "1,5" })).toEqual({ ergebnis: null, trail: null });
  });

  it("meldet Fund und Anzeige", () => {
    const r = trailAenderungen(spur, {}, { gefunden: true, anzeige: 0, alterText: "1,5" });
    expect(r).toEqual({ ergebnis: { personFound: true, indication: 0 }, trail: null });
  });

  it("lässt die Anzeige ohne Fund weg und beim Negativ-Trail", () => {
    expect(trailAenderungen(spur, {}, { gefunden: false, anzeige: 2, alterText: "1,5" })).toEqual({
      ergebnis: { personFound: false, indication: null },
      trail: null,
    });
    expect(trailAenderungen({ ...spur, negativeTrail: true }, {}, { gefunden: true, anzeige: 2, alterText: "1,5" })).toEqual({
      ergebnis: { personFound: true, indication: null },
      trail: null,
    });
  });

  it("meldet ein geändertes Trailalter samt den unveränderten Trail-Angaben", () => {
    const r = trailAenderungen(spur, {}, { gefunden: null, anzeige: null, alterText: "2" });
    expect(r).toEqual({
      ergebnis: null,
      trail: { ageMinutes: 120, doubleBlind: true, negativeTrail: false, scentArticle: "Mütze" },
    });
  });

  it("speichert ein vorgeschlagenes Alter, wenn die Spur noch keines hat", () => {
    const r = trailAenderungen({ ageMinutes: null }, {}, { gefunden: null, anzeige: null, alterText: "1,25" });
    expect(r).toEqual({ ergebnis: null, trail: { ageMinutes: 75, doubleBlind: false, negativeTrail: false, scentArticle: null } });
  });

  it("meldet ein unlesbares Alter als Fehler und ein leeres als gelöscht", () => {
    expect(trailAenderungen(spur, {}, { gefunden: null, anzeige: null, alterText: "viel" })).toEqual({ fehler: "alter" });
    expect(trailAenderungen(spur, {}, { gefunden: null, anzeige: null, alterText: "" })).toMatchObject({ trail: { ageMinutes: null } });
  });
});

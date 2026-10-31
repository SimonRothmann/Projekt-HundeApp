import { describe, expect, it } from "vitest";
import { icsDateiname } from "./kalender";
import { heuteIso, plusJahre, plusMonate, plusTage, tageBis, tagesnummer } from "./kalendertag";

// Der Fehler zeigte sich nur in Deutschland nach Mitternacht (UTC ist dort
// noch "gestern"). Auf einem Entwicklungsrechner in UTC bliebe er unsichtbar,
// deshalb legt dieser Test die Zeitzone selbst fest.
process.env.TZ = "Europe/Berlin";

describe("Kalendertag in Ortszeit", () => {
  it("00:30 Uhr im Sommer ist schon der neue Tag, obwohl UTC noch beim Vortag liegt", () => {
    // 2026-06-20T22:30:00Z = 21.06.2026 00:30 in Berlin (MESZ, UTC+2)
    const jetzt = Date.parse("2026-06-20T22:30:00Z");
    expect(new Date(jetzt).toISOString().slice(0, 10)).toBe("2026-06-20");
    expect(heuteIso(jetzt)).toBe("2026-06-21");
  });

  it("00:30 Uhr im Winter ist ebenfalls schon der neue Tag", () => {
    // 2026-12-14T23:30:00Z = 15.12.2026 00:30 in Berlin (MEZ, UTC+1)
    expect(heuteIso(Date.parse("2026-12-14T23:30:00Z"))).toBe("2026-12-15");
  });

  it("23:30 Uhr bleibt beim alten Tag", () => {
    expect(heuteIso(Date.parse("2026-06-20T21:30:00Z"))).toBe("2026-06-20");
  });

  it("der Dateiname eines Termins trägt den Tag in Ortszeit", () => {
    // Beginn 00:30 Uhr Ortszeit am 6. Oktober
    expect(icsDateiname("2026-10-05T22:30:00Z")).toBe("Gruppentraining-2026-10-06.ics");
  });
});

describe("Rechnen mit Kalendertagen", () => {
  it("plusTage rechnet über Monats- und Jahresgrenzen", () => {
    expect(plusTage("2026-10-30", 3)).toBe("2026-11-02");
    expect(plusTage("2026-12-30", 3)).toBe("2027-01-02");
    expect(plusTage("2027-03-01", -1)).toBe("2027-02-28");
  });

  it("plusTage verschiebt über die Sommerzeit-Umstellung hinweg keinen Tag", () => {
    expect(plusTage("2026-03-28", 1)).toBe("2026-03-29");
    expect(plusTage("2026-03-28", 2)).toBe("2026-03-30");
    expect(plusTage("2026-10-24", 2)).toBe("2026-10-26");
  });

  it("plusMonate hält sich an das Monatsende", () => {
    expect(plusMonate("2026-11-30", 3)).toBe("2027-02-28");
    expect(plusMonate("2026-10-31", 1)).toBe("2026-11-30");
    expect(plusMonate("2026-11-15", 3)).toBe("2027-02-15");
    expect(plusMonate("2026-12-15", 1)).toBe("2027-01-15");
    expect(plusMonate("2026-05-31", -3)).toBe("2026-02-28");
  });

  it("plusJahre kappt den 29.2. auf den 28.2., vorwärts wie rückwärts", () => {
    expect(plusJahre("2028-02-29", 1)).toBe("2029-02-28");
    expect(plusJahre("2028-02-29", -4)).toBe("2024-02-29");
    expect(plusJahre("2028-02-29", -1)).toBe("2027-02-28");
    expect(plusJahre("2026-10-03", -25)).toBe("2001-10-03");
    expect(plusJahre("2026-10-01", -2)).toBe("2024-10-01");
    expect(plusJahre("2028-02-29", -2)).toBe("2026-02-28");
  });

  it("tageBis zählt ganze Kalendertage, auch bei Zeitstempel mit Tagesanfang", () => {
    expect(tageBis("2026-10-05", "2026-10-03")).toBe(2);
    expect(tageBis("2026-10-03T00:00:00", "2026-10-03")).toBe(0);
    expect(tageBis("2026-10-01", "2026-10-03")).toBe(-2);
    expect(tagesnummer("1970-01-02")).toBe(1);
  });
});

import { describe, expect, it } from "vitest";
import { erzeugeIcs, faltIcsZeile, icsDateiname, icsZeitpunkt, kannInKalender, maskiereIcsText, type KalenderTermin } from "./kalender";

const JETZT = new Date("2026-10-01T08:00:00Z");
const termin: KalenderTermin = {
  id: "8b0c2c9e-1d3a-4f57-9a64-2f0d6a1b7c11",
  startsAt: "2026-10-05T16:30:00.000Z",
  durationMinutes: 90,
  titel: "Dienstagsgruppe: Gruppentraining",
  ort: "Hundeplatz am Wald",
  beschreibung: "Gruppentraining mit Dogity. Dauer: 90 Min.",
};

const oktette = (s: string) => new TextEncoder().encode(s).length;
/** Die Zeilen der Datei, so wie ein Leser sie nach dem Entfalten sähe. */
const entfaltet = (ics: string) => ics.replace(/\r\n /g, "").split("\r\n");

describe("maskiereIcsText", () => {
  it("maskiert Backslash, Semikolon, Komma und Zeilenumbruch", () => {
    expect(maskiereIcsText("a,b;c\\d\ne")).toBe("a\\,b\\;c\\\\d\\ne");
  });

  it("behandelt Windows- und Mac-Zeilenumbrüche wie einen", () => {
    expect(maskiereIcsText("a\r\nb\rc")).toBe("a\\nb\\nc");
  });

  it("maskiert den Backslash zuerst, sonst würde er doppelt maskiert", () => {
    expect(maskiereIcsText("\\,")).toBe("\\\\\\,");
  });

  it("lässt Umlaute unberührt", () => {
    expect(maskiereIcsText("Fährte im Grünen")).toBe("Fährte im Grünen");
  });
});

describe("faltIcsZeile", () => {
  it("lässt kurze Zeilen stehen", () => {
    expect(faltIcsZeile("SUMMARY:kurz")).toBe("SUMMARY:kurz");
  });

  it("lässt eine Zeile von genau 75 Oktetten stehen", () => {
    const zeile = "X".repeat(75);
    expect(faltIcsZeile(zeile)).toBe(zeile);
  });

  it("bricht nach 75 Oktetten um, Fortsetzungen beginnen mit einem Leerzeichen", () => {
    const zeile = "X".repeat(76);
    expect(faltIcsZeile(zeile)).toBe("X".repeat(75) + "\r\n X");
  });

  it("hält jede Zeile - auch die Fortsetzungen mit Leerzeichen - bei höchstens 75 Oktetten", () => {
    const gefaltet = faltIcsZeile("DESCRIPTION:" + "Fährte über Stock und Stein ".repeat(20));
    for (const teil of gefaltet.split("\r\n")) expect(oktette(teil)).toBeLessThanOrEqual(75);
  });

  it("schneidet nie mitten in einem Umlaut und gibt entfaltet den Originaltext zurück", () => {
    // 37 x "ä" = 74 Oktette, das nächste passt nicht mehr in die erste Zeile.
    const zeile = "ä".repeat(60);
    const gefaltet = faltIcsZeile(zeile);
    const teile = gefaltet.split("\r\n");
    expect(teile.length).toBeGreaterThan(1);
    expect(teile[0]).toBe("ä".repeat(37));
    expect(gefaltet.replace(/\r\n /g, "")).toBe(zeile);
  });

  it("schneidet auch Zeichen außerhalb der Grundebene (Emoji, 4 Oktette) nicht entzwei", () => {
    const zeile = "🐕".repeat(40);
    const gefaltet = faltIcsZeile(zeile);
    for (const teil of gefaltet.split("\r\n")) expect(oktette(teil)).toBeLessThanOrEqual(75);
    expect(gefaltet.replace(/\r\n /g, "")).toBe(zeile);
  });
});

describe("icsZeitpunkt", () => {
  it("schreibt UTC ohne Trennzeichen und ohne Millisekunden, mit Z", () => {
    expect(icsZeitpunkt(new Date("2026-10-05T16:30:00.123Z"))).toBe("20261005T163000Z");
  });
});

describe("erzeugeIcs", () => {
  const ics = erzeugeIcs(termin, JETZT);

  it("trennt Zeilen mit CRLF und endet mit CRLF", () => {
    expect(ics.endsWith("\r\n")).toBe(true);
    // Kein einzelnes LF ohne vorangehendes CR.
    expect(ics.replace(/\r\n/g, "")).not.toMatch(/[\r\n]/);
  });

  it("rahmt den Termin als VCALENDAR mit einem VEVENT", () => {
    const zeilen = entfaltet(ics);
    expect(zeilen[0]).toBe("BEGIN:VCALENDAR");
    expect(zeilen).toContain("VERSION:2.0");
    expect(zeilen).toContain("BEGIN:VEVENT");
    expect(zeilen).toContain("END:VEVENT");
    expect(zeilen[zeilen.length - 2]).toBe("END:VCALENDAR");
    expect(zeilen.filter((z) => z === "BEGIN:VEVENT")).toHaveLength(1);
  });

  it("nennt PRODID, UID, DTSTAMP, Start und Ende in UTC", () => {
    const zeilen = entfaltet(ics);
    expect(zeilen.some((z) => z.startsWith("PRODID:"))).toBe(true);
    expect(zeilen).toContain("UID:8b0c2c9e-1d3a-4f57-9a64-2f0d6a1b7c11@dogity");
    expect(zeilen).toContain("DTSTAMP:20261001T080000Z");
    expect(zeilen).toContain("DTSTART:20261005T163000Z");
    expect(zeilen).toContain("DTEND:20261005T180000Z");
  });

  it("setzt Titel, Ort und Beschreibung, Sonderzeichen maskiert", () => {
    const mitKomma = erzeugeIcs({ ...termin, ort: "Platz, hinter der Halle; Tor 2", titel: "Gruppe Ä: Gruppentraining" }, JETZT);
    const zeilen = entfaltet(mitKomma);
    expect(zeilen).toContain("SUMMARY:Gruppe Ä: Gruppentraining");
    expect(zeilen).toContain("LOCATION:Platz\\, hinter der Halle\\; Tor 2");
    expect(zeilen.some((z) => z.startsWith("DESCRIPTION:Gruppentraining mit Dogity"))).toBe(true);
  });

  it("maskiert ein Semikolon im Gruppennamen, damit das Feld nicht zerschnitten wird", () => {
    const ics = erzeugeIcs({ ...termin, titel: "Hunde; Freunde: Gruppentraining" }, JETZT);
    expect(entfaltet(ics)).toContain("SUMMARY:Hunde\\; Freunde: Gruppentraining");
  });

  it("lässt LOCATION weg, wenn es keinen besonderen Ort gibt", () => {
    const ohneOrt = erzeugeIcs({ ...termin, ort: null }, JETZT);
    expect(ohneOrt).not.toContain("LOCATION");
  });

  it("faltet lange Zeilen bei 75 Oktetten, auch mit Umlauten", () => {
    const lang = erzeugeIcs({ ...termin, beschreibung: "Übung für Fährte und Fußarbeit ".repeat(10) }, JETZT);
    for (const zeile of lang.split("\r\n")) expect(oktette(zeile)).toBeLessThanOrEqual(75);
    const beschreibung = entfaltet(lang).find((z) => z.startsWith("DESCRIPTION:"))!;
    expect(beschreibung).toContain("Übung für Fährte und Fußarbeit Übung");
  });

  it("enthält keine Personendaten", () => {
    expect(ics).not.toMatch(/ORGANIZER|ATTENDEE|@(?!dogity)/i);
  });

  it("gibt einem Termin ohne Dauer mindestens eine Minute", () => {
    const zeilen = entfaltet(erzeugeIcs({ ...termin, durationMinutes: 0 }, JETZT));
    expect(zeilen).toContain("DTEND:20261005T163100Z");
  });
});

describe("kannInKalender", () => {
  const jetzt = JETZT.getTime();
  it("erlaubt geplante, künftige Termine", () => {
    expect(kannInKalender({ status: 0, startsAt: "2026-10-05T16:30:00Z" }, jetzt)).toBe(true);
  });
  it("lehnt abgesagte Termine ab", () => {
    expect(kannInKalender({ status: 1, startsAt: "2026-10-05T16:30:00Z" }, jetzt)).toBe(false);
  });
  it("lehnt vergangene Termine ab", () => {
    expect(kannInKalender({ status: 0, startsAt: "2026-09-30T16:30:00Z" }, jetzt)).toBe(false);
  });
});

describe("icsDateiname", () => {
  it("trägt das Datum in UTC", () => {
    expect(icsDateiname("2026-10-05T16:30:00Z")).toBe("Gruppentraining-2026-10-05.ics");
  });
});

import { describe, expect, it } from "vitest";
import { icsDateiname } from "./kalender";
import { heuteIso } from "./pruefung";

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

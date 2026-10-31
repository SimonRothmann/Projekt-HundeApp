import { describe, expect, it } from "vitest";
import { datumKurz, datumMitWochentag } from "./pruefung";
import { heuteIso, plusTage, tageBis, tagAlsDatum } from "./kalendertag";

// Westlich von Greenwich ist UTC-Mitternacht noch der Vortag: `new Date("2026-10-03")` zeigte dort
// den 2. Oktober. Dieser Test legt eine solche Zeitzone selbst fest (ein Entwicklungsrechner in
// Mitteleuropa oder UTC würde den Fehler nie zeigen).
process.env.TZ = "America/Los_Angeles";

describe("Kalendertage westlich von UTC", () => {
  it("die Zeitzone dieses Tests liegt wirklich westlich (sonst beweist er nichts)", () => {
    expect(new Date("2026-10-03").getDate()).toBe(2);
  });

  it("tagAlsDatum zeigt den Tag, nicht den Vortag", () => {
    const datum = tagAlsDatum("2026-10-03");
    expect([datum.getFullYear(), datum.getMonth() + 1, datum.getDate()]).toEqual([2026, 10, 3]);
    expect(datum.toLocaleDateString("de-DE")).toBe("3.10.2026");
    expect(datum.toLocaleDateString("en-GB", { month: "long", year: "numeric" })).toBe("October 2026");
  });

  it("tagAlsDatum nimmt auch einen Zeitstempel mit Tagesanfang als den Tag", () => {
    expect(tagAlsDatum("2026-10-03T00:00:00").getDate()).toBe(3);
  });

  it("die vorhandenen Formate (datumKurz, datumMitWochentag) zeigen denselben Tag", () => {
    expect(datumKurz("2026-10-03")).toBe("03.10.2026");
    expect(datumMitWochentag("2026-10-03")).toBe("Samstag, 3. Oktober");
  });

  it("heute, morgen und Abstand rechnen mit dem Kalendertag des Geräts", () => {
    // 2026-10-04T02:00Z ist in Los Angeles noch der 3. Oktober, 19:00 Uhr.
    const jetzt = Date.parse("2026-10-04T02:00:00Z");
    expect(heuteIso(jetzt)).toBe("2026-10-03");
    expect(plusTage(heuteIso(jetzt), 1)).toBe("2026-10-04");
    expect(tageBis("2026-10-05", heuteIso(jetzt))).toBe(2);
  });
});

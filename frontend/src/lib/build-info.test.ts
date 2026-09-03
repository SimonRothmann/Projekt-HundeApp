import { describe, expect, it } from "vitest";
import { formatiereBuildZeit } from "./build-info";

describe("formatiereBuildZeit", () => {
  it("gibt deutsche Ortszeit aus, nicht UTC", () => {
    // 16:09 UTC im September ist in Berlin 18:09 (Sommerzeit, UTC+2).
    expect(formatiereBuildZeit(new Date("2026-09-03T16:09:00Z"))).toBe("3. September 2026 um 18:09 Uhr");
  });

  it("rechnet auch im Winter richtig um", () => {
    // Januar: UTC+1. Ein fest eingebauter Versatz wäre hier eine Stunde daneben.
    expect(formatiereBuildZeit(new Date("2026-01-15T16:09:00Z"))).toBe("15. Januar 2026 um 17:09 Uhr");
  });

  it("verschiebt den Tag, wenn die Ortszeit ihn verschiebt", () => {
    // 23:30 UTC ist in Berlin schon der Folgetag - genau der Fall, in dem
    // eine Anzeige ohne feste Zeitzone je nach Betrachter anders ausfiele.
    expect(formatiereBuildZeit(new Date("2026-09-03T23:30:00Z"))).toBe("4. September 2026 um 01:30 Uhr");
  });
});

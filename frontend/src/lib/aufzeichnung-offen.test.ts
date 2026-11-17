import { describe, expect, it } from "vitest";
import { aufzeichnungGeoeffnet, istAufzeichnungOffen } from "./aufzeichnung-offen";

describe("aufzeichnungGeoeffnet", () => {
  it("meldet offen, bis jede Öffnung zurückgegeben ist", () => {
    expect(istAufzeichnungOffen()).toBe(false);
    const zu1 = aufzeichnungGeoeffnet();
    const zu2 = aufzeichnungGeoeffnet(); // z. B. Strict Mode hängt kurz doppelt ein
    zu1();
    expect(istAufzeichnungOffen()).toBe(true);
    zu2();
    expect(istAufzeichnungOffen()).toBe(false);
  });
});

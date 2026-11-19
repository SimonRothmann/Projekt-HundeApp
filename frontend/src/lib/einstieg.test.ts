import { describe, expect, it } from "vitest";
import { einstiegsOrdnung } from "@/lib/einstieg";
import type { Regulation } from "@/lib/types";

const ordnung = (name: string): Regulation =>
  ({ id: name, name, seedKey: name, sourceUrl: null, lastSyncedAt: null, latestKnownVersionLabel: null, description: null }) as Regulation;

describe("einstiegsOrdnung", () => {
  it("Deutschland sieht nur BH", () => {
    expect(einstiegsOrdnung("BH", [ordnung("BH")])?.name).toBe("BH");
  });
  it("Österreich bekommt die ÖKV-BH, auch wenn BH davor steht", () => {
    const liste = [ordnung("BH"), ordnung("ÖKV BH/VT (Mensch-Hund-Team-Prüfung)")];
    expect(einstiegsOrdnung("BH", liste)?.name).toBe("ÖKV BH/VT (Mensch-Hund-Team-Prüfung)");
  });
  it("Schweiz: erste vorhandene der Vorzugsliste", () => {
    const liste = [ordnung("SKG Obedience Beginners"), ordnung("FCI-Obedience 1"), ordnung("SKG Agility Klasse 1")];
    expect(einstiegsOrdnung("OBEDIENCE", liste)?.name).toBe("SKG Obedience Beginners");
    expect(einstiegsOrdnung("AGILITY", liste)?.name).toBe("SKG Agility Klasse 1");
  });
  it("unbekannter Code oder keine Vorzugsordnung: erste der Liste", () => {
    expect(einstiegsOrdnung("IGP1", [ordnung("IGP 1"), ordnung("IGP 2")])?.name).toBe("IGP 1");
    expect(einstiegsOrdnung("THS", [ordnung("Etwas anderes")])?.name).toBe("Etwas anderes");
  });
  it("leere Liste", () => {
    expect(einstiegsOrdnung("BH", [])).toBeNull();
  });
});

import { describe, expect, it } from "vitest";
import { faerbeSegmente } from "./faehrten-bild";
import { deviationColor } from "./karte/spur-daten";
import { ART_FAEHRTE, ART_MANTRAILING, FAEHRTE_GRENZEN, MANTRAILING_GRENZEN, spurGrenzen } from "./mantrailing";
import { AMPEL_FARBEN, ampelLabel, ampelStufe } from "./spur-ampel";

// Eine Einstufung für alle Anzeigen: Auswertung (Wort), Kartenlinie (Farbe) und
// Fährtenbild (Linienstück) müssen für dieselbe Abweichung dieselbe Stufe nennen.
const FAELLE = [
  { art: { kind: ART_FAEHRTE }, grenzen: FAEHRTE_GRENZEN, werte: [0, 3, 3.1, 6, 6.1, 20] },
  { art: { kind: ART_MANTRAILING }, grenzen: MANTRAILING_GRENZEN, werte: [0, 10, 10.1, 25, 25.1, 40] },
];

describe("Ampel der Abweichung", () => {
  it("nimmt für die Fährte 3/6 m und fürs Mantrailing 10/25 m", () => {
    expect(spurGrenzen({ kind: ART_FAEHRTE })).toEqual({ gruenBis: 3, gelbBis: 6 });
    expect(spurGrenzen({ kind: ART_MANTRAILING })).toEqual({ gruenBis: 10, gelbBis: 25 });
    expect(spurGrenzen(undefined)).toEqual({ gruenBis: 3, gelbBis: 6 });
  });

  it.each(FAELLE)("stuft Karte, Wort und Bild für $art.kind gleich ein", ({ art, grenzen, werte }) => {
    for (const meter of werte) {
      const stufe = ampelStufe(meter, spurGrenzen(art));
      expect(deviationColor(meter, grenzen)).toBe(AMPEL_FARBEN[stufe]);
      const stueck = faerbeSegmente([meter, meter], grenzen)[0];
      expect(stueck.stufe).toBe(stufe);
    }
  });

  it("gehört die Grenze zur besseren Stufe", () => {
    expect(ampelStufe(3)).toBe("gruen");
    expect(ampelStufe(3.01)).toBe("gelb");
    expect(ampelStufe(6)).toBe("gelb");
    expect(ampelStufe(6.01)).toBe("rot");
    expect(ampelStufe(10, MANTRAILING_GRENZEN)).toBe("gruen");
    expect(ampelStufe(25, MANTRAILING_GRENZEN)).toBe("gelb");
    expect(ampelStufe(25.01, MANTRAILING_GRENZEN)).toBe("rot");
  });

  it("nennt je Art das passende Wort", () => {
    expect(ampelLabel(2, { kind: ART_FAEHRTE })).toBe("eng an der Fährte");
    expect(ampelLabel(8, { kind: ART_MANTRAILING })).toBe("nah an der Spur");
    expect(ampelLabel(8, { kind: ART_FAEHRTE })).toBe("deutlich abgekommen");
    expect(ampelLabel(8, { kind: ART_MANTRAILING })).not.toBe(ampelLabel(8, { kind: ART_FAEHRTE }));
    expect(ampelLabel(4)).toBe("mittlere Abweichung");
    expect(ampelLabel(15, { kind: ART_MANTRAILING })).toBe("mittlere Abweichung");
  });
});

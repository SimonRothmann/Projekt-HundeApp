import { describe, expect, it } from "vitest";
import {
  AKTUELLE_VERSION,
  formatiereVeroeffentlichung,
  VERSIONSHINWEISE,
} from "./versionshinweise";

/**
 * Die Liste ist von Hand gepflegt, und die Anzeige verlässt sich auf genau
 * eine Zusage: Der erste Eintrag ist der aktuelle. Ein neuer Eintrag unten
 * angehängt statt oben eingefügt wäre kein Tippfehler, den man beim Lesen
 * sieht - die Seite zeigte dann still eine ein Jahr alte Fassung als
 * "aktuell". Deshalb wacht ein Test darüber und nicht die Aufmerksamkeit.
 */
describe("VERSIONSHINWEISE", () => {
  it("steht neueste Fassung zuerst", () => {
    const daten = VERSIONSHINWEISE.map((h) => h.datum);
    expect(daten).toStrictEqual([...daten].sort().reverse());
  });

  it("zählt die Fassungen absteigend - und zwar als Zahl, nicht als Text", () => {
    // 0.10 kommt NACH 0.9, obwohl "0.10" < "0.9" ist, sobald man
    // Zeichenketten vergleicht. Genau diese Falle schnappt zu, wenn die
    // zweite Stelle zweistellig wird - also beim Übergang, den dieses
    // Projekt gerade hinter sich hat.
    const alsZahlen = VERSIONSHINWEISE.map((h) => h.version.split(".").map(Number));
    for (let i = 1; i < alsZahlen.length; i++) {
      const [aMajor, aMinor, aPatch = 0] = alsZahlen[i - 1];
      const [bMajor, bMinor, bPatch = 0] = alsZahlen[i];
      const vorher = aMajor * 1_000_000 + aMinor * 1_000 + aPatch;
      const nachher = bMajor * 1_000_000 + bMinor * 1_000 + bPatch;
      expect(vorher, `${VERSIONSHINWEISE[i - 1].version} muss über ${VERSIONSHINWEISE[i].version} stehen`)
        .toBeGreaterThan(nachher);
    }
  });

  it("nennt jede Fassungsnummer nur einmal", () => {
    const versionen = VERSIONSHINWEISE.map((h) => h.version);
    expect(new Set(versionen).size).toBe(versionen.length);
  });

  it("führt jede Fassung mit Datum, Titel und mindestens einer Änderung", () => {
    for (const hinweis of VERSIONSHINWEISE) {
      expect(hinweis.datum, hinweis.version).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(hinweis.titel.trim(), hinweis.version).not.toBe("");
      expect(hinweis.aenderungen.length, hinweis.version).toBeGreaterThan(0);
      for (const aenderung of hinweis.aenderungen) {
        expect(aenderung.text.trim(), hinweis.version).not.toBe("");
      }
    }
  });

  it("meldet als aktuelle Fassung die oberste", () => {
    expect(AKTUELLE_VERSION).toBe(VERSIONSHINWEISE[0].version);
  });
});

describe("formatiereVeroeffentlichung", () => {
  it("schreibt den Monat aus", () => {
    expect(formatiereVeroeffentlichung("2026-09-03")).toBe("3. September 2026");
  });

  it("folgt der Oberflächensprache", () => {
    expect(formatiereVeroeffentlichung("2026-09-03", "en")).toBe("3 September 2026");
  });

  it("bleibt am selben Tag - der Mittags-Anker ist kein Zierrat", () => {
    // Ohne ihn läse JavaScript "2026-01-01" als Mitternacht UTC und machte
    // in Berlin daraus den 1., in New York aber den 31. Dezember.
    expect(formatiereVeroeffentlichung("2026-01-01")).toBe("1. Januar 2026");
    expect(formatiereVeroeffentlichung("2026-12-31")).toBe("31. Dezember 2026");
  });
});

import { describe, expect, it } from "vitest";
import type { GpsPoint, GpsWalkRunArticle } from "@/lib/types";
import { uebersetze } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";
import {
  aufschluesselung,
  bestaetigteZahlen,
  ergebnisAenderungen,
  ergebnisseJeGegenstand,
  ergebnisUmschalten,
  gegenstaende,
  MATERIALIEN,
  materialUmschalten,
} from "./gegenstaende";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);

function marker(id: string | undefined, sekunde: number, rest: Partial<GpsPoint> = {}): GpsPoint {
  return {
    id,
    latitude: 50,
    longitude: 8,
    timestamp: new Date(Date.UTC(2026, 0, 1, 10, 0, sekunde)).toISOString(),
    accuracy: 5,
    pointType: 1,
    label: null,
    markerType: 0,
    ...rest,
  };
}

const angabe = (pointId: string, result: 0 | 1 | 2): GpsWalkRunArticle => ({ pointId, result });

describe("gegenstaende", () => {
  it("nimmt nur Marker der Art Gegenstand mit Id, in Legereihenfolge", () => {
    const punkte = [
      marker("c", 30),
      marker("pot", 20, { markerType: 1 }),
      marker("gps", 10, { pointType: 0 }),
      marker("a", 10, { label: "Handschuh" }),
      marker(undefined, 15),
      marker("b", 20, { material: "Holz" }),
    ];

    expect(gegenstaende(punkte)).toEqual([
      { nummer: 1, id: "a", label: "Handschuh", material: null },
      { nummer: 2, id: "b", label: null, material: "Holz" },
      { nummer: 3, id: "c", label: null, material: null },
    ]);
  });

  it("behält bei gleichem Zeitpunkt die Reihenfolge der Eingabe", () => {
    expect(gegenstaende([marker("x", 5), marker("y", 5), marker("z", 5)]).map((g) => g.id)).toEqual(["x", "y", "z"]);
  });

  it("hat ohne Gegenstände nichts zu zeigen", () => {
    expect(gegenstaende([])).toEqual([]);
    expect(gegenstaende([marker("pot", 1, { markerType: 2 })])).toEqual([]);
  });
});

describe("Material und Ergebnis umschalten", () => {
  it("wählt ein Material und wählt es beim erneuten Tippen ab", () => {
    expect(materialUmschalten(null, "Leder")).toBe("Leder");
    expect(materialUmschalten(undefined, "Leder")).toBe("Leder");
    expect(materialUmschalten("Holz", "Leder")).toBe("Leder");
    expect(materialUmschalten("Leder", "Leder")).toBeNull();
  });

  it("wählt ein Ergebnis und entfernt es beim erneuten Tippen", () => {
    expect(ergebnisUmschalten(null, 0)).toBe(0);
    expect(ergebnisUmschalten(0, 2)).toBe(2);
    expect(ergebnisUmschalten(1, 1)).toBeNull();
    // 0 ist eine Wahl (verwiesen), keine fehlende Angabe.
    expect(ergebnisUmschalten(0, 0)).toBeNull();
  });

  it("bietet die Auswahl des Servers an, jedes Material passt in die Spalte", () => {
    expect([...MATERIALIEN]).toEqual(["Leder", "Holz", "Stoff", "Teppich", "Kunststoff", "Sonstiges"]);
    expect(MATERIALIEN.every((m) => m.length <= TEXTLAENGE.gegenstandsMaterial)).toBe(true);
  });
});

describe("ergebnisAenderungen", () => {
  it("schickt nur, was vom Gespeicherten abweicht", () => {
    const gespeichert = [angabe("a", 0), angabe("b", 2)];

    expect(ergebnisAenderungen(gespeichert, { a: 0, b: 1, c: 2 })).toEqual([
      { pointId: "b", result: 1 },
      { pointId: "c", result: 2 },
    ]);
  });

  it("meldet ein Entfernen als null und ein Nicht-Anfassen gar nicht", () => {
    const gespeichert = [angabe("a", 0)];

    expect(ergebnisAenderungen(gespeichert, { a: null, b: null })).toEqual([{ pointId: "a", result: null }]);
    expect(ergebnisAenderungen(gespeichert, {})).toEqual([]);
    expect(ergebnisAenderungen(undefined, { a: null })).toEqual([]);
  });

  it("liest die gespeicherten Angaben als Wert je Gegenstand", () => {
    expect(ergebnisseJeGegenstand([angabe("a", 1)])).toEqual({ a: 1 });
    expect(ergebnisseJeGegenstand(undefined)).toEqual({});
  });
});

describe("bestaetigteZahlen", () => {
  it("zählt verwiesen und aufgenommen als gefunden, mit Aufschlüsselung", () => {
    const zahlen = bestaetigteZahlen({ articlesTotal: 3, articles: [angabe("a", 0), angabe("b", 1), angabe("c", 2)] });

    expect(zahlen).toEqual({ gefunden: 2, gesamt: 3, verwiesen: 1, aufgenommen: 1, ueberlaufen: 1, ohneAngabe: 0 });
    expect(aufschluesselung(zahlen!, t)).toBe("1 verwiesen, 1 aufgenommen, 1 überlaufen");
  });

  it("lässt Ergebnisse ohne Treffer weg und nennt Gegenstände ohne Angabe", () => {
    const zahlen = bestaetigteZahlen({ articlesTotal: 4, articles: [angabe("a", 0), angabe("b", 0)] });

    expect(zahlen).toMatchObject({ gefunden: 2, gesamt: 4, ohneAngabe: 2 });
    expect(aufschluesselung(zahlen!, t)).toBe("2 verwiesen, 2 ohne Angabe");
  });

  it("gibt ohne Angaben null: dann zeigt die Anzeige die GPS-Zahl wie bisher", () => {
    expect(bestaetigteZahlen({ articlesTotal: 3, articles: [] })).toBeNull();
    expect(bestaetigteZahlen({ articlesTotal: 3 })).toBeNull();
    // Ohne gelegte Gegenstände gibt es nichts zu bestätigen.
    expect(bestaetigteZahlen({ articlesTotal: 0, articles: [angabe("a", 0)] })).toBeNull();
    expect(bestaetigteZahlen({ articlesTotal: null, articles: [angabe("a", 0)] })).toBeNull();
  });
});

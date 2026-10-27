import { describe, expect, it } from "vitest";
import { MODULE, type Country } from "./types";
import { uebersetze } from "./i18n";
import {
  sichtbareModule,
  themeName,
  vorschauDarstellung,
  vorschauFunktionen,
  vorschauSportarten,
  zeigeLaenderwahl,
} from "./profil";

const de = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const en = (text: string, werte?: Record<string, string | number>) => uebersetze("en", text, werte);

const SPORTARTEN = [
  { id: "1", name: "BH" },
  { id: "2", name: "Fährte" },
  { id: "3", name: "Agility" },
  { id: "4", name: "Turnierhundsport" },
];

describe("vorschauSportarten", () => {
  it("sagt 'alle', solange nichts gewählt ist", () => {
    expect(vorschauSportarten([], SPORTARTEN, de)).toBe("alle");
    expect(vorschauSportarten([], null, de)).toBe("alle");
  });

  it("zählt bis zwei auf, in der Reihenfolge der Liste", () => {
    expect(vorschauSportarten(["2"], SPORTARTEN, de)).toBe("Fährte");
    expect(vorschauSportarten(["2", "1"], SPORTARTEN, de)).toBe("BH, Fährte");
  });

  it("kürzt längere Auswahlen mit der Restzahl", () => {
    expect(vorschauSportarten(["1", "2", "3", "4"], SPORTARTEN, de)).toBe("BH, Fährte +2");
  });

  it("nennt auf Englisch die englischen Namen der FCI-Sportarten", () => {
    const sportarten = [{ id: "1", name: "FCI-Fährtenprüfung (FPr)", nameEn: "Tracking (FCI-FPr)" }, { id: "2", name: "Agility" }];
    expect(vorschauSportarten(["1", "2"], sportarten, de, "en")).toBe("Tracking (FCI-FPr), Agility");
    expect(vorschauSportarten(["1", "2"], sportarten, de)).toBe("FCI-Fährtenprüfung (FPr), Agility");
  });

  it("zeigt nichts, solange die Liste noch lädt", () => {
    expect(vorschauSportarten(["1"], null, de)).toBe("");
  });

  it("nennt die Zahl, wenn die gewählten Sportarten nicht mehr existieren", () => {
    expect(vorschauSportarten(["x", "y"], SPORTARTEN, de)).toBe("2 gewählt");
  });
});

describe("vorschauFunktionen", () => {
  it("zählt nur die Module, die die Person sieht", () => {
    expect(sichtbareModule(false).some((m) => m.key === MODULE.gruppentraining)).toBe(false);
    expect(vorschauFunktionen([], false, de)).toBe("5 von 5 an");
    expect(vorschauFunktionen([], true, de)).toBe("6 von 6 an");
  });

  it("zieht abgewählte Module ab", () => {
    expect(vorschauFunktionen([MODULE.wetter], true, de)).toBe("5 von 6 an");
  });

  it("ignoriert abgewählte Module, die die Person gar nicht sieht", () => {
    expect(vorschauFunktionen([MODULE.gruppentraining], false, de)).toBe("5 von 5 an");
  });
});

describe("vorschauDarstellung", () => {
  it("fügt Farbschema, Schrift und Sprache zusammen", () => {
    expect(vorschauDarstellung("dark", "normal", "de", de)).toBe("Dunkel · Normal · Deutsch");
    expect(vorschauDarstellung("light", "sehr-gross", "en", en)).toBe("Light · Extra large · English");
  });

  it("nimmt dunkel an, solange das Schema nicht bekannt ist", () => {
    expect(themeName(undefined, de)).toBe("Dunkel");
  });
});

describe("zeigeLaenderwahl", () => {
  const land = (code: string, regulationCount: number): Country => ({ code, regulationCount });

  it("blendet die Auswahl aus, wenn nur ein Land Inhalt hat", () => {
    expect(zeigeLaenderwahl([land("DE", 40), land("AT", 0), land("CH", 0)], "DE")).toBe(false);
    expect(zeigeLaenderwahl([], "DE")).toBe(false);
  });

  it("zeigt sie ab zwei Ländern mit Inhalt", () => {
    expect(zeigeLaenderwahl([land("DE", 40), land("AT", 3), land("CH", 0)], "DE")).toBe(true);
  });

  it("bleibt sichtbar, wenn ein anderes Land gespeichert ist - sonst gäbe es keinen Rückweg", () => {
    expect(zeigeLaenderwahl([land("DE", 40), land("AT", 0)], "AT")).toBe(true);
  });
});

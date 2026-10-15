import { describe, expect, it } from "vitest";
import { uebersetze } from "./i18n";
import { istGuid, resetUserZiel, zuErledigenZeilen } from "./admin";

const de = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const en = (text: string, werte?: Record<string, string | number>) => uebersetze("en", text, werte);

const ID = "3f2b8c1e-5d4a-4e7b-9a10-0c1d2e3f4a5b";

describe("resetUserZiel", () => {
  it("leitet eine Guid auf die Nutzer-Seite weiter, unverändert", () => {
    expect(resetUserZiel(ID)).toBe(`/admin/nutzer?resetUser=${ID}`);
    expect(resetUserZiel(ID.toUpperCase())).toBe(`/admin/nutzer?resetUser=${ID.toUpperCase()}`);
  });

  it("leitet ohne Parameter nicht weiter", () => {
    expect(resetUserZiel(null)).toBeNull();
    expect(resetUserZiel("")).toBeNull();
  });

  it("verwirft alles, was keine Guid ist", () => {
    expect(resetUserZiel("abc")).toBeNull();
    expect(resetUserZiel(`${ID}&x=1`)).toBeNull();
    expect(resetUserZiel(`${ID}/../../x`)).toBeNull();
    expect(resetUserZiel("//evil.example")).toBeNull();
    expect(resetUserZiel(` ${ID}`)).toBeNull();
    expect(resetUserZiel(`${ID}\n`)).toBeNull();
  });
});

describe("istGuid", () => {
  it("erkennt nur Guids", () => {
    expect(istGuid(ID)).toBe(true);
    expect(istGuid("nope")).toBe(false);
    expect(istGuid(null)).toBe(false);
    expect(istGuid(undefined)).toBe(false);
  });
});

describe("zuErledigenZeilen", () => {
  it("zeigt nichts, wenn nichts offen ist", () => {
    expect(zuErledigenZeilen({ antraege: 0, verwaisteKonten: 0 }, de)).toEqual([]);
  });

  it("blendet nicht abrufbare Zahlen aus, ohne die andere zu verlieren", () => {
    expect(zuErledigenZeilen({ antraege: null, verwaisteKonten: null }, de)).toEqual([]);
    expect(zuErledigenZeilen({ antraege: null, verwaisteKonten: 2 }, de)).toEqual([
      { href: "/admin/system", titel: "Daten gelöschter Konten", vorschau: "2" },
    ]);
  });

  it("führt offene Anträge zuerst und verlinkt auf die zuständigen Seiten", () => {
    expect(zuErledigenZeilen({ antraege: 3, verwaisteKonten: 1 }, de)).toEqual([
      { href: "/admin/vereine", titel: "Vereinsanträge", vorschau: "3" },
      { href: "/admin/system", titel: "Daten gelöschter Konten", vorschau: "1" },
    ]);
  });

  it("übersetzt die Titel", () => {
    expect(zuErledigenZeilen({ antraege: 1, verwaisteKonten: 1 }, en).map((z) => z.titel)).not.toEqual([
      "Vereinsanträge",
      "Daten gelöschter Konten",
    ]);
  });
});

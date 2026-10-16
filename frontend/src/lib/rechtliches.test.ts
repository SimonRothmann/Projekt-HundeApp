import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { STAND, STAND_EN } from "./rechtliches";

/**
 * Wächter gegen das Auseinanderlaufen der deutschen und der englischen
 * Datenschutzerklärung. Rechtlich verbindlich ist die deutsche; die englische
 * ist eine Übersetzung und darf ihr nicht hinterherhinken.
 */

const DATENSCHUTZ = join(process.cwd(), "src", "app", "datenschutz");

function abschnitte(datei: string): number {
  const quelltext = readFileSync(join(DATENSCHUTZ, datei), "utf8");
  return (quelltext.match(/<Abschnitt\b/g) ?? []).length;
}

describe("Datenschutzerklärung: deutsche und englische Fassung", () => {
  it("hat in der englischen Fassung dasselbe Datum wie in der deutschen", () => {
    // Rot heißt: STAND wurde angehoben (die deutsche Fassung hat sich
    // geändert), die englische aber noch nicht nachgezogen. Erst
    // datenschutz-en.tsx anpassen, dann STAND_EN auf dasselbe Datum setzen.
    expect(STAND_EN).toBe(STAND);
  });

  it("hat in beiden Fassungen gleich viele Abschnitte", () => {
    const deutsch = abschnitte("page.tsx");
    expect(deutsch).toBeGreaterThan(0);
    expect(abschnitte("datenschutz-en.tsx")).toBe(deutsch);
  });

  it("nummeriert die Abschnitte in beiden Fassungen gleich", () => {
    const nummern = (datei: string) =>
      [...readFileSync(join(DATENSCHUTZ, datei), "utf8").matchAll(/<Abschnitt titel="(\d+)\./g)].map((m) => m[1]);
    expect(nummern("datenschutz-en.tsx")).toStrictEqual(nummern("page.tsx"));
  });
});

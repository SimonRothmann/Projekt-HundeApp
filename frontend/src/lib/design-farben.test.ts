import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Wächter über DESIGN.md "Farben": Jede Farbe im Code hat eine Rolle (primary, erfolg, warnung,
 * destructive ...), keine Tailwind-Rohfarbe wie `text-amber-700`. Bis 2026-11-18 standen Erfolg und
 * Warnung als Rohfarben in 31 Dateien, in acht Mischungen, teils unter 4,5:1 (emerald-600 als Text).
 * Eine neue Stelle scheitert hier, bis sie ein Token nutzt oder begründet in die Liste unten kommt.
 */

const QUELLE = join(process.cwd(), "src");

const PALETTE =
  "red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose|slate|gray|zinc|neutral|stone";
const ROHFARBE = new RegExp(
  `\\b(?:bg|text|border|ring|from|to|via|fill|stroke|outline|decoration|divide|shadow)-(?:${PALETTE})-\\d{2,3}\\b`,
  "g",
);

/** Begründete Ausnahmen: Datei → warum hier keine Rolle passt. */
const AUSNAHMEN: Record<string, string> = {
  "components/env-badge.tsx": "TEST-Kennzeichen der Testumgebung, kein Teil der Oberfläche; Rot mit weißer Schrift in beiden Modi",
};

function alleDateien(verzeichnis: string): string[] {
  return readdirSync(verzeichnis).flatMap((eintrag) => {
    const pfad = join(verzeichnis, eintrag);
    if (statSync(pfad).isDirectory()) return alleDateien(pfad);
    return /\.tsx?$/.test(eintrag) && !/\.test\.tsx?$/.test(eintrag) ? [pfad] : [];
  });
}

describe("Farben nach Rolle (DESIGN.md)", () => {
  it("nutzt keine Tailwind-Rohfarben außerhalb der Ausnahmen", () => {
    const funde = alleDateien(QUELLE).flatMap((pfad) => {
      const name = relative(QUELLE, pfad);
      if (name in AUSNAHMEN) return [];
      return [...readFileSync(pfad, "utf8").matchAll(ROHFARBE)].map((m) => `${name}: ${m[0]}`);
    });
    expect(funde).toEqual([]);
  });

  it("legt Flächen mit bg-surface an, nicht mit bg-muted/… (verschwindet im Dark Mode)", () => {
    // Hover-Zustände (hover:bg-muted/60) und Seitenhintergründe (<main>) sind keine Flächen.
    const funde = alleDateien(QUELLE).flatMap((pfad) =>
      readFileSync(pfad, "utf8")
        .split("\n")
        .filter((zeile) => !zeile.includes("<main") && /(?<![\w:/-])bg-muted\/\d+/.test(zeile))
        .map((zeile) => `${relative(QUELLE, pfad)}: ${zeile.trim()}`),
    );
    expect(funde).toEqual([]);
  });
});

import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Der Wächter über die Stellen, an denen Text als HTML in die Seite geht.
 *
 * React behandelt Text immer als Text - dort gibt es nichts zu bewachen.
 * Gefährlich sind die Ausnahmen: dangerouslySetInnerHTML und alles, was
 * Zeichenketten per innerHTML in die Seite schreibt. Über genau so eine
 * Stelle lief bis 2026-09-28 die Beschriftung eines Fährten-Markers als
 * Skript beim Betrachter: Die damalige Kartenbibliothek (Leaflet) schrieb
 * Tooltips per innerHTML, ein Label wie `<img src=x onerror=...>` wurde beim
 * Antippen ausgeführt - bei jeder Person, die die Fährte sieht, und damit mit
 * deren Anmeldung.
 *
 * Seit dem Umstieg auf MapLibre gibt es dafür keinen Pfad mehr: Die Karte
 * zeigt Text über Popup.setText, das Text als Text setzt. Deshalb ist
 * Popup.setHTML hier nirgends erlaubt - kein Escaping, das man vergessen
 * könnte.
 *
 * Die Abwägung "Anmeldung im localStorage statt im Cookie" in TODO.md steht
 * auf der Voraussetzung, dass es keine solchen Einschübe gibt. Dieser Test
 * macht aus der Voraussetzung eine Prüfung: Eine neue Stelle scheitert hier,
 * bis sie begründet in die Liste unten kommt.
 */

const QUELLE = join(process.cwd(), "src");

function alleDateien(verzeichnis: string): string[] {
  return readdirSync(verzeichnis).flatMap((eintrag) => {
    const pfad = join(verzeichnis, eintrag);
    if (statSync(pfad).isDirectory()) return alleDateien(pfad);
    return /\.tsx?$/.test(eintrag) && !/\.test\.tsx?$/.test(eintrag) ? [pfad] : [];
  });
}

/** Kommentare zählen nicht - sonst meldete die Erklärung eines Beispiels sich selbst. */
function ohneKommentare(inhalt: string): string {
  return inhalt.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
}

const dateien = alleDateien(QUELLE).map((pfad) => ({
  name: relative(QUELLE, pfad),
  inhalt: ohneKommentare(readFileSync(pfad, "utf8")),
}));

/**
 * dangerouslySetInnerHTML ist erlaubt, wo der Inhalt nur aus Konstanten
 * besteht oder JSON ist, dessen "<" maskiert wird (kein </script> möglich).
 */
const ERLAUBTES_INNER_HTML = [
  /__html:\s*JSON\.stringify\([^)]*\)\.replace\(\/<\/g,\s*"\\\\u003c"\)/,
];
const INNER_HTML_NUR_KONSTANTEN = new Set([
  // Schriftgrößen-Skript vor dem ersten Bild - nur Konstanten aus lib/schrift.
  "app/layout.tsx",
]);

describe("Stellen, an denen Text als HTML in die Seite geht", () => {
  it("findet die Karte überhaupt - sonst prüfte dieser Test nichts", () => {
    expect(dateien.some((d) => d.inhalt.includes(".setText("))).toBe(true);
  });

  it("setzt Text der Karte nie als HTML", () => {
    const verstoesse = dateien.filter((d) => d.inhalt.includes(".setHTML(")).map((d) => d.name);
    expect(verstoesse, `Popup.setHTML in:\n${verstoesse.join("\n")}`).toStrictEqual([]);
  });

  it("nutzt dangerouslySetInnerHTML nur für maskiertes JSON oder reine Konstanten", () => {
    const verstoesse = dateien.flatMap((d) => {
      const stellen = [...d.inhalt.matchAll(/dangerouslySetInnerHTML=\{\{([\s\S]*?)\}\}/g)];
      if (INNER_HTML_NUR_KONSTANTEN.has(d.name)) return [];
      return stellen
        .filter((s) => !ERLAUBTES_INNER_HTML.some((erlaubt) => erlaubt.test(s[1])))
        .map((s) => `${d.name}: ${s[1].trim().slice(0, 60)}…`);
    });
    expect(verstoesse, `Nicht freigegeben:\n${verstoesse.join("\n")}`).toStrictEqual([]);
  });

  it("schreibt nirgends direkt HTML ins DOM", () => {
    const muster = /\.(innerHTML|outerHTML)\s*=[^=]|insertAdjacentHTML\(|document\.write\(/g;
    const verstoesse = dateien.flatMap((d) => [...d.inhalt.matchAll(muster)].map((m) => `${d.name}: ${m[0]}`));
    expect(verstoesse).toStrictEqual([]);
  });
});

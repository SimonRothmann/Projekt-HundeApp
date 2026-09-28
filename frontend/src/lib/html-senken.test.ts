import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";
import { alsHtmlText } from "./html-text";

/**
 * Der Wächter über die Stellen, an denen Text als HTML in die Seite geht.
 *
 * React behandelt Text immer als Text - dort gibt es nichts zu bewachen.
 * Gefährlich sind die Ausnahmen: Leaflet (Tooltips, Popups, divIcons setzt es
 * per innerHTML) und dangerouslySetInnerHTML. Über genau so eine Stelle lief
 * bis 2026-09-28 die Beschriftung eines Fährten-Markers als Skript beim
 * Betrachter (siehe html-text.ts).
 *
 * Die Abwägung "Anmeldung im localStorage statt im Cookie" in TODO.md steht
 * auf der Voraussetzung, dass es keine solchen Einschübe gibt. Dieser Test
 * macht aus der Voraussetzung eine Prüfung: Eine neue Stelle scheitert hier,
 * bis sie über alsHtmlText läuft oder begründet in die Liste unten kommt.
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
 * Einschübe in ein divIcon, die nachweislich nicht von Nutzer:innen kommen.
 * Neue Einträge nur mit Begründung.
 */
const SICHERE_ICON_WERTE = new Set([
  // Farbe aus der festen Liste WALK_RUN_COLORS in track-map.tsx.
  "liveColor",
  // Geglättete Himmelsrichtung - eine Zahl aus der Geolocation.
  "smoothedHeadingRef.current ?? 0",
]);

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

describe("HTML aus Text", () => {
  it("maskiert alles, was HTML ausmacht", () => {
    expect(alsHtmlText(`<img src=x onerror="alert('x')">`)).toBe(
      "&lt;img src=x onerror=&quot;alert(&#39;x&#39;)&quot;&gt;",
    );
  });

  it("lässt ein & als Zeichen stehen, statt es zu verschlucken", () => {
    expect(alsHtmlText("Wiese & Wald")).toBe("Wiese &amp; Wald");
    // Zweimal angewandt entsteht sichtbares "&amp;" - deshalb genau einmal,
    // direkt an der Stelle, die HTML baut.
    expect(alsHtmlText("&lt;")).toBe("&amp;lt;");
  });

  it("lässt gewöhnlichen Text unverändert", () => {
    expect(alsHtmlText("Gegenstand 3: 12s")).toBe("Gegenstand 3: 12s");
  });
});

describe("Stellen, an denen Text als HTML in die Seite geht", () => {
  it("findet die Karte überhaupt - sonst prüfte dieser Test nichts", () => {
    expect(dateien.some((d) => d.inhalt.includes(".bindTooltip("))).toBe(true);
  });

  it("gibt Leaflet-Tooltips, -Popups und -Inhalte nur über alsHtmlText", () => {
    const muster = /\.(bindTooltip|bindPopup|setContent|setTooltipContent|setPopupContent)\(\s*([^\s][\s\S]{0,20})/g;
    const verstoesse = dateien.flatMap((d) =>
      [...d.inhalt.matchAll(muster)]
        .filter((m) => !m[2].startsWith("alsHtmlText("))
        .map((m) => `${d.name}: .${m[1]}(${m[2]}…`),
    );
    expect(verstoesse, `Ohne alsHtmlText:\n${verstoesse.join("\n")}`).toStrictEqual([]);
  });

  it("setzt in divIcons nur Werte ein, die nicht von Nutzer:innen stammen", () => {
    const verstoesse: string[] = [];
    for (const d of dateien) {
      for (const icon of d.inhalt.matchAll(/divIcon\(\{[\s\S]*?html:\s*([`"'])([\s\S]*?)\1/g)) {
        for (const einschub of icon[2].matchAll(/\$\{([^}]*)\}/g)) {
          const wert = einschub[1].trim();
          if (!wert.startsWith("alsHtmlText(") && !SICHERE_ICON_WERTE.has(wert)) {
            verstoesse.push(`${d.name}: \${${wert}}`);
          }
        }
      }
      // html: ohne Literal (etwa eine Variable) lässt sich hier nicht prüfen.
      for (const m of d.inhalt.matchAll(/divIcon\(\{[\s\S]*?html:\s*([^\s`"'])/g)) {
        verstoesse.push(`${d.name}: html: ${m[1]}… ist kein Literal`);
      }
    }
    expect(verstoesse, `Ungeprüfte Einschübe:\n${verstoesse.join("\n")}`).toStrictEqual([]);
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

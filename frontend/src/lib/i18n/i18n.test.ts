import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { EN } from "./en";
import { uebersetze } from "./index";

/**
 * Der Wächter über die Vollständigkeit.
 *
 * Weil der Schlüssel der deutsche Satz selbst ist, kann keine Typprüfung
 * merken, dass eine Übersetzung fehlt - `t("Neuer Satz")` ist immer gültiger
 * Code und liefert im Zweifel eben Deutsch. Genau das macht den Rückfall
 * gutmütig und das Vergessen leicht.
 *
 * Deshalb liest dieser Test den Quelltext und vergleicht jeden Aufruf mit der
 * englischen Liste. Er ist damit die einzige Stelle, an der "die App ist
 * vollständig übersetzt" überhaupt eine überprüfbare Aussage ist.
 */

const QUELLE = join(process.cwd(), "src");

function alleDateien(verzeichnis: string): string[] {
  return readdirSync(verzeichnis).flatMap((eintrag) => {
    const pfad = join(verzeichnis, eintrag);
    if (statSync(pfad).isDirectory()) return alleDateien(pfad);
    return /\.tsx?$/.test(eintrag) && !/\.test\.tsx?$/.test(eintrag) ? [pfad] : [];
  });
}

/**
 * Entfernt Kommentare, damit ein Beispiel in einer Erklärung nicht als
 * echter Aufruf zählt - genau das ist beim Schreiben dieses Tests passiert.
 */
function ohneKommentare(inhalt: string): string {
  return inhalt.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
}

/**
 * Findet t("...") und uebersetzbar("..."), auch mehrzeilig geschrieben.
 *
 * Der Marker muss mit: Wo ein Satz als Datenfeld steht und später über
 * t(variable) gerendert wird, ist er im Quelltext sonst unsichtbar.
 */
function sammleSchluessel(): Map<string, string[]> {
  const gefunden = new Map<string, string[]>();
  const muster = /\b(?:t|uebersetzbar)\(\s*"((?:[^"\\]|\\.)*)"/g;

  for (const datei of alleDateien(QUELLE)) {
    const inhalt = ohneKommentare(readFileSync(datei, "utf8"));
    for (const treffer of inhalt.matchAll(muster)) {
      const schluessel = treffer[1].replace(/\\"/g, '"').replace(/\\n/g, "\n");
      gefunden.set(schluessel, [...(gefunden.get(schluessel) ?? []), datei]);
    }
  }
  return gefunden;
}

describe("Englische Übersetzung", () => {
  const schluessel = sammleSchluessel();

  it("findet überhaupt Aufrufe - sonst prüfte dieser Test nichts", () => {
    expect(schluessel.size).toBeGreaterThan(50);
  });

  it("übersetzt jeden verwendeten Satz", () => {
    const fehlend = [...schluessel.keys()].filter((s) => !(s in EN)).sort();
    expect(fehlend, `Ohne englische Fassung:\n${fehlend.join("\n")}`).toStrictEqual([]);
  });

  it("führt keine Sätze, die es im Code nicht mehr gibt", () => {
    // Verwaiste Einträge sind kein Fehler, der etwas kaputt macht - aber sie
    // sammeln sich an und lassen die Liste größer aussehen als die Arbeit ist.
    const verwaist = Object.keys(EN).filter((s) => !schluessel.has(s)).sort();
    expect(verwaist, `Nicht mehr verwendet:\n${verwaist.join("\n")}`).toStrictEqual([]);
  });

  it("lässt keine englische Fassung leer", () => {
    const leer = Object.entries(EN).filter(([, wert]) => wert.trim() === "").map(([s]) => s);
    expect(leer).toStrictEqual([]);
  });

  it("behält die Platzhalter der deutschen Fassung bei", () => {
    // Ein verlorenes {name} fällt sonst erst auf, wenn jemand "Hallo ,"
    // auf dem Bildschirm sieht.
    const platzhalter = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort();
    const abweichend = Object.entries(EN)
      .filter(([de, en]) => platzhalter(de).join() !== platzhalter(en).join())
      .map(([de]) => de);
    expect(abweichend).toStrictEqual([]);
  });
});

describe("uebersetze", () => {
  it("gibt auf Deutsch den Schlüssel selbst zurück", () => {
    expect(uebersetze("de", "Meine Hunde")).toBe("Meine Hunde");
  });

  it("fällt bei fehlender Übersetzung auf Deutsch zurück statt auf nichts", () => {
    expect(uebersetze("en", "Diesen Satz gibt es nicht")).toBe("Diesen Satz gibt es nicht");
  });

  it("setzt Platzhalter in beiden Sprachen ein", () => {
    expect(uebersetze("de", "{n} Hunde", { n: 3 })).toBe("3 Hunde");
  });

  it("lässt unbekannte Platzhalter stehen, statt sie zu leeren", () => {
    expect(uebersetze("de", "Hallo {name}", { andere: "x" })).toBe("Hallo {name}");
  });
});

import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Der Wächter über die eingecheckten Kartendateien (public/karte/).
 *
 * Sie sind Kopien bzw. Erzeugnisse, die sich nicht von selbst mit
 * node_modules mitbewegen: Wer maplibre-gl aktualisiert oder eine Farbe in
 * scripts/karten-stile.mjs ändert, vergisst leicht den zweiten Schritt - und
 * der Fehler zeigt sich erst im Browser als leere Karte. Ein veralteter
 * Worker passt zudem nicht mehr zur Kartenbibliothek, ohne dass etwas meldet.
 */

const WURZEL = process.cwd();
const KARTE = join(WURZEL, "public", "karte");
const MAPLIBRE = join(WURZEL, "node_modules", "maplibre-gl");
const WORKER_DATEIEN = ["maplibre-gl-worker.mjs", "maplibre-gl-shared.mjs"];

const version = (JSON.parse(readFileSync(join(MAPLIBRE, "package.json"), "utf8")) as { version: string }).version;

describe("Worker-Dateien von MapLibre", () => {
  it("liegen für die installierte Version vor und sind byte-gleich mit node_modules", () => {
    const ordner = join(KARTE, `maplibre-${version}`);
    expect(existsSync(ordner), `${ordner} fehlt - "node scripts/maplibre-worker.mjs" ausführen`).toBe(true);
    for (const datei of WORKER_DATEIEN) {
      const kopie = readFileSync(join(ordner, datei));
      const original = readFileSync(join(MAPLIBRE, "dist", datei));
      expect(kopie.equals(original), `${datei} weicht von node_modules ab - "node scripts/maplibre-worker.mjs" ausführen`).toBe(true);
    }
  });

  it("gibt es nur für eine Version, nicht für alte daneben", () => {
    const ordner = readdirSync(KARTE).filter((name) => name.startsWith("maplibre-"));
    expect(ordner).toStrictEqual([`maplibre-${version}`]);
  });
});

describe("Kartenstile", () => {
  it("sind nicht veraltet gegenüber scripts/karten-stile.mjs", () => {
    // Endet das Skript mit Fehler, wirft execFileSync - samt Meldung des Skripts.
    expect(() =>
      execFileSync(process.execPath, ["scripts/karten-stile.mjs", "--pruefen"], { cwd: WURZEL, stdio: "pipe" }),
    ).not.toThrow();
  });

  it.each(["stil-hell.json", "stil-dunkel.json"])("%s holt Kartendaten nur von der OpenStreetMap Foundation", (name) => {
    const stil = JSON.parse(readFileSync(join(KARTE, name), "utf8")) as Record<string, unknown>;
    // Die Lizenz-Adressen in metadata sind keine Abrufe.
    delete stil.metadata;
    const adressen = JSON.stringify(stil).match(/https?:\/\/[^"\s\\]+/g) ?? [];
    const hosts = new Set(adressen.map((adresse) => new URL(adresse).host));
    // Genau ein Anbieter - vor allem kein tiles.versatiles.org, das Dogity
    // nicht als Empfänger von IP-Adressen führt (Datenschutzerklärung).
    expect([...hosts]).toStrictEqual(["vector.openstreetmap.org"]);
  });

  it.each(["stil-hell.json", "stil-dunkel.json"])("%s verweist für Schriften und Symbole auf die eigene Auslieferung", (name) => {
    const stil = JSON.parse(readFileSync(join(KARTE, name), "utf8")) as { glyphs: string; sprite: { url: string }[] };
    expect(stil.glyphs).toBe("/karte/schriften/{fontstack}/{range}.pbf");
    expect(stil.sprite).toStrictEqual([{ id: "base", url: "/karte/symbole/base" }]);
    expect(existsSync(join(KARTE, "symbole", "base.json"))).toBe(true);
  });
});

// Kopiert die Worker-Dateien von MapLibre nach public/karte/maplibre-<Version>/.
//
//   node scripts/maplibre-worker.mjs
//
// Warum: MapLibre leitet die Adresse seines Web Workers aus import.meta.url
// ab. Unter dem Bundler ist das keine http-Adresse, der Worker startet nie,
// und die Karte bleibt leer - ohne dass ein Fehler ausgelöst wird (siehe
// docs/FAEHRTE_AUFZEICHNUNG.md, "Umstieg auf MapLibre GL"). Die Karte setzt
// deshalb setWorkerUrl auf diese Dateien, von der eigenen Adresse und als
// Modul-Worker, also ohne Blob (worker-src bleibt 'self').
//
// Die Version steht im Ordnernamen: Der Service Worker liefert den Pfad
// unveränderlich aus dem Cache. Die Dateien bleiben eingecheckt, kein
// prebuild-Hook - nach einem Update von maplibre-gl dieses Skript ausführen;
// src/lib/karte/karten-dateien.test.ts scheitert, solange das fehlt.

import { copyFileSync, mkdirSync, readFileSync, readdirSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const WURZEL = join(dirname(fileURLToPath(import.meta.url)), "..");
const QUELLE = join(WURZEL, "node_modules", "maplibre-gl");
const ZIEL = join(WURZEL, "public", "karte");
const DATEIEN = ["maplibre-gl-worker.mjs", "maplibre-gl-shared.mjs"];

const { version } = JSON.parse(readFileSync(join(QUELLE, "package.json"), "utf8"));
const ordner = `maplibre-${version}`;

// Alte Versionen entfernen, sonst sammelt sich je Update ein Ordner an.
for (const eintrag of readdirSync(ZIEL)) {
  if (eintrag.startsWith("maplibre-") && eintrag !== ordner) {
    rmSync(join(ZIEL, eintrag), { recursive: true, force: true });
  }
}

mkdirSync(join(ZIEL, ordner), { recursive: true });
for (const datei of DATEIEN) copyFileSync(join(QUELLE, "dist", datei), join(ZIEL, ordner, datei));
console.log(`Kopiert nach public/karte/${ordner}/: ${DATEIEN.join(", ")}`);

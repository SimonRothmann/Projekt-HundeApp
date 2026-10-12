// Erzeugt die beiden Kartenstile (hell und dunkel) nach public/karte/.
//
//   node scripts/karten-stile.mjs          schreibt die Dateien neu
//   node scripts/karten-stile.mjs --pruefen  scheitert, wenn sie veraltet sind
//
// Warum vorab erzeugt und eingecheckt statt im Browser gebaut: Der Baukasten
// (@versatiles/style) wäre sonst ein weiteres Stück Code, das jedes Handy beim
// ersten Kartenaufruf laden müsste - für ein Ergebnis, das sich nur ändert,
// wenn hier jemand eine Farbe anfasst. So ist er nur eine Entwicklungs-
// abhängigkeit, und die App lädt eine fertige JSON-Datei.
//
// Die Farben stammen nicht aus den fertigen VersaTiles-Paletten: Deren helle
// Fassung (und die von Home Assistant, die darauf aufbaut) zeichnet Acker,
// Wiese und Feldweg in fast demselben Hellbeige - in der Stadt ruhig, auf dem
// Übungsgelände aber genau das, was man sehen will, verschwunden. Hier sind
// Acker, Wiese und Wald klar getrennt und Feldwege sichtbar; der Dunkelstil
// ist auf den Hintergrund der App abgestimmt (globals.css, .dark).

import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { osm } from "@versatiles/style";

const ZIEL = join(dirname(fileURLToPath(import.meta.url)), "..", "public", "karte");

// Die Vektorkacheln der OpenStreetMap Foundation (Schema "Shortbread"). Über
// das TileJSON statt fester Kachel-Adressen: Namensnennung, Zoombereich und
// Adressen kommen dann vom Anbieter selbst. Dasselbe Schema liefert auch
// tiles.versatiles.org - fällt die OSMF aus, genügt ein Wechsel dieser Zeile.
const TILEJSON = "https://vector.openstreetmap.org/shortbread_v1/tilejson.json";

// Schriften und Symbole liefert Dogity selbst aus (public/karte/): kein
// weiterer Dienst, der IP-Adressen sieht, und offline aus dem Cache verfügbar.
// Relativ - die Karte macht daraus zur Laufzeit absolute Adressen, weil
// MapLibre relative Pfade für Symbole ablehnt.
const URLS = {
  // Leer, sonst setzt der Baukasten tiles.versatiles.org davor.
  base: "",
  osm: TILEJSON,
  glyphsPattern: "/karte/schriften/{fontstack}/{range}.pbf",
  sprite: [{ id: "base", url: "/karte/symbole/base" }],
};

const HELL = {
  background: "#f3f1ec",
  land: "#f3f1ec",
  water: "#a8d1ea",
  labelWater: "#4f7fa3",
  natureWood: "#b3d39a",
  natureGrass: "#d5e8bf",
  naturePark: "#c6e0ae",
  natureLeisure: "#d9ebc6",
  natureAgriculture: "#efe9cb",
  natureWetland: "#d8e6d6",
  natureSand: "#f1e8d6",
  natureRock: "#ece6dc",
  glacier: "#faf8f4",
  areaResidential: "#ede9e1",
  areaCommercial: "#f2e3dc",
  areaIndustrial: "#ebe6d6",
  areaWaste: "#e9e2d4",
  areaBurial: "#e3e8d8",
  siteParking: "#e9e5dc",
  siteSports: "#c6e0ae66",
  building: "#e2dbcc",
  buildingBg: "#cdc2ad",
  roadStreet: "#ffffff",
  roadStreetBg: "#c9bda6",
  roadTrunk: "#ffe7be",
  roadTrunkBg: "#e3c793",
  roadMotorway: "#f6d49a",
  roadMotorwayBg: "#dbb06a",
  transitRail: "#cfc6b6",
  transitSubway: "#d6cebf",
  transitCycle: "#8d9cb8",
  transitFoot: "#9c8566",
  boundary: "#c9b28e",
  boundaryDisputed: "#ddcfb6",
  label: "#3f3b33",
  labelHalo: "#f8f6f1cc",
  labelSymbol: "#857d6b",
  labelPoi: "#857d6b99",
  labelShield: "#fbf8f3",
  labelHousenumber: "#857d6b80",
};

// Vollständig ausgeschrieben: Ein Hellwert, der hier fehlte, schiene auf der
// dunklen Karte als greller Fleck durch.
const DUNKEL = {
  background: "#181922",
  land: "#181922",
  water: "#1e3456",
  labelWater: "#8fb3e0",
  natureWood: "#1b3a29",
  natureGrass: "#1e3328",
  naturePark: "#22392c",
  natureLeisure: "#1e3328",
  natureAgriculture: "#2b2c22",
  natureWetland: "#1f2a2a",
  natureSand: "#26262c",
  natureRock: "#26262c",
  glacier: "#2a2b33",
  areaResidential: "#1c1d27",
  areaCommercial: "#221f2c",
  areaIndustrial: "#202029",
  areaWaste: "#1c1d27",
  areaBurial: "#1c2420",
  siteParking: "#1f2029",
  siteSports: "#22392c66",
  building: "#252735",
  buildingBg: "#30334a",
  roadStreet: "#42465e",
  roadStreetBg: "#121319",
  roadTrunk: "#4a4e66",
  roadTrunkBg: "#17181f",
  roadMotorway: "#545872",
  roadMotorwayBg: "#1a1b23",
  transitRail: "#34374a",
  transitSubway: "#34374a",
  transitCycle: "#7f8fb3",
  transitFoot: "#9799b3",
  boundary: "#45495f",
  boundaryDisputed: "#45495f",
  label: "#e6e7ef",
  labelHalo: "#0f1015cc",
  labelSymbol: "#9a9db8",
  labelPoi: "#9a9db899",
  labelShield: "#181922",
  labelHousenumber: "#9a9db880",
};

/** Die eine Quelle auf das TileJSON zeigen lassen - alles Weitere liefert der Anbieter. */
function aufTileJson(stil) {
  const quellen = Object.values(stil.sources);
  if (quellen.length !== 1) {
    throw new Error(`Erwartet genau eine Kartenquelle, gefunden ${quellen.length} - was liefert @versatiles/style?`);
  }
  for (const feld of ["tiles", "attribution", "bounds", "minzoom", "maxzoom", "scheme"]) delete quellen[0][feld];
  quellen[0].url = TILEJSON;
  return stil;
}

function erzeugen(theme, colors) {
  // Flache Karte: Der Globus samt Himmel ist für ein Übungsgelände von ein
  // paar hundert Metern nur Ballast.
  const stil = aufTileJson(osm({ theme, colors, urls: URLS, projection: "mercator", sky: false }));
  delete stil.sky;
  return JSON.stringify(stil);
}

const dateien = {
  "stil-hell.json": erzeugen("colorful", HELL),
  "stil-dunkel.json": erzeugen("colorful-dark", DUNKEL),
};

if (process.argv.includes("--pruefen")) {
  const veraltet = Object.entries(dateien).filter(([name, inhalt]) => {
    try {
      return readFileSync(join(ZIEL, name), "utf8") !== inhalt;
    } catch {
      return true;
    }
  });
  if (veraltet.length > 0) {
    console.error(`Veraltet: ${veraltet.map(([n]) => n).join(", ")} - "node scripts/karten-stile.mjs" ausführen.`);
    process.exit(1);
  }
} else {
  for (const [name, inhalt] of Object.entries(dateien)) writeFileSync(join(ZIEL, name), inhalt);
  console.log(`Geschrieben: ${Object.keys(dateien).join(", ")}`);
}

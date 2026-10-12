import type { StyleSpecification } from "maplibre-gl";
import { KARTEN_EBENEN, type KartenEbene } from "@/lib/karten-ebenen";

/**
 * Welcher Kartenstil gerade gilt.
 *
 * "hell" und "dunkel" sind die beiden Vektorstile aus public/karte/ (erzeugt
 * von scripts/karten-stile.mjs), "luftbild" ist ein Rasterstil, der hier im
 * Code steht. Das Luftbild hat nur eine Fassung: Es ist ohnehin dunkel genug,
 * ein "dunkles Luftbild" gäbe es nicht.
 */
export type KartenStil = "hell" | "dunkel" | "luftbild";

export function stilWaehlen(ebene: KartenEbene, dunkel: boolean): KartenStil {
  if (ebene === "luftbild") return "luftbild";
  return dunkel ? "dunkel" : "hell";
}

/**
 * Schriften der eigenen Auslieferung (public/karte/schriften/). Auch der
 * Luftbild-Stil bekommt sie: Er zeichnet selbst keinen Text, aber ein Stil
 * ohne glyphs wäre bei jeder späteren Beschriftung ein stiller Fehler.
 */
const GLYPHEN_PFAD = "/karte/schriften/{fontstack}/{range}.pbf";

/**
 * Macht einen ursprungsrelativen Pfad absolut.
 *
 * Bewusst Zeichenkette davorsetzen und KEIN new URL(...): Das würde die
 * geschweiften Klammern von {fontstack} und {range} prozentcodieren, und die
 * Platzhalter fänden sich nicht mehr. MapLibre lehnt relative Symbol-Adressen
 * ab, deshalb muss das hier passieren und nicht im JSON.
 */
function absolut(adresse: string, ursprung: string): string {
  return adresse.startsWith("/") && !adresse.startsWith("//") ? `${ursprung}${adresse}` : adresse;
}

/** Eine Kopie des Stils, deren Schriften und Symbole absolute Adressen haben. */
export function absolutMachen(stil: StyleSpecification, ursprung: string): StyleSpecification {
  const kopie: StyleSpecification = { ...stil };
  if (typeof stil.glyphs === "string") kopie.glyphs = absolut(stil.glyphs, ursprung);
  if (typeof stil.sprite === "string") {
    kopie.sprite = absolut(stil.sprite, ursprung);
  } else if (Array.isArray(stil.sprite)) {
    kopie.sprite = stil.sprite.map((eintrag) => ({ ...eintrag, url: absolut(eintrag.url, ursprung) }));
  }
  return kopie;
}

/** Der Luftbild-Stil: eine Rasterquelle, eine Rasterebene. */
export function luftbildStil(ursprung: string): StyleSpecification {
  const raster = KARTEN_EBENEN.luftbild.raster;
  if (!raster) throw new Error("Die Ebene Luftbild braucht eine Rasterquelle.");
  return {
    version: 8,
    glyphs: absolut(GLYPHEN_PFAD, ursprung),
    sources: {
      luftbild: {
        type: "raster",
        tiles: [raster.url],
        tileSize: 256,
        maxzoom: raster.maxZoom,
        attribution: raster.attribution,
      },
    },
    layers: [{ id: "luftbild", type: "raster", source: "luftbild" }],
  };
}

// Vom Text, nicht vom Objekt: MapLibre verändert, was man ihm übergibt (es
// ergänzt Quellen, ersetzt Ausdrücke). Ein geteiltes Objekt wäre beim zweiten
// Stilwechsel nicht mehr das, was in der Datei steht - deshalb wird je
// Verwendung neu geparst.
const texte = new Map<string, string>();
// Laufende Anfragen teilen: Mehrere Karten einer Seite (Vollbild und
// Historie) fragen sonst im selben Moment dieselbe Datei an.
const laufend = new Map<string, Promise<string>>();

async function stilText(name: "hell" | "dunkel"): Promise<string> {
  const vorhanden = texte.get(name);
  if (vorhanden !== undefined) return vorhanden;

  let anfrage = laufend.get(name);
  if (!anfrage) {
    anfrage = fetch(`/karte/stil-${name}.json`)
      .then((antwort) => {
        if (!antwort.ok) throw new Error(`Kartenstil ${name} nicht ladbar (${antwort.status}).`);
        return antwort.text();
      })
      .then((text) => {
        texte.set(name, text);
        return text;
      })
      // Egal wie sie endet: Die Anfrage ist vorbei. Ein Fehler bleibt so nicht
      // im Speicher hängen, der nächste Versuch fragt neu an.
      .finally(() => laufend.delete(name));
    laufend.set(name, anfrage);
  }
  return anfrage;
}

/** Lädt den gewählten Stil, bereit für map.setStyle bzw. new Map({ style }). */
export async function stilLaden(stil: KartenStil, ursprung: string): Promise<StyleSpecification> {
  if (stil === "luftbild") return luftbildStil(ursprung);
  const geparst = JSON.parse(await stilText(stil)) as StyleSpecification;
  return absolutMachen(geparst, ursprung);
}

/** Nur für Tests: leert den Speicher der geladenen Stile. */
export function stilSpeicherLeeren(): void {
  texte.clear();
  laufend.clear();
}

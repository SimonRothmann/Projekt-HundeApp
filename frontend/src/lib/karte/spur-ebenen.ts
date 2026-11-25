import type { ExpressionSpecification, GeoJSONSource, LayerSpecification, Map as KartenObjekt } from "maplibre-gl";
import {
  MARKER_FORMEN,
  MARKER_RAND_FARBE,
  TRACK_CASING_COLOR,
  TRACK_LINE_COLOR,
  leer,
  type Linie,
  type MarkerArt,
  type Merkmale,
  type Punkt,
} from "@/lib/karte/spur-daten";

/**
 * Die eigenen Quellen und Ebenen der Fährtenkarte, über dem Grundstil.
 *
 * Die Reihenfolge ist die Zeichenreihenfolge: Die Legung liegt unten, der
 * Ablauf als Linie darin, Halte, Marker und Start/Ende als Punkte darüber, die
 * eigene Position zuoberst.
 */

export const SPUR_QUELLEN = [
  "legung-linie",
  "legung-enden",
  "marker",
  "winkel",
  "ablauf-bewertet",
  "ablauf-unbewertet",
  "ablauf-live",
  "ablauf-luecke",
  "ablauf-luecke-live",
  "halte",
  "position",
] as const;
export type SpurQuelle = (typeof SPUR_QUELLEN)[number];

/**
 * Ebenen, die beim Antippen einen Text zeigen. Die Legung selbst gehört nicht
 * dazu - sie hatte auch vorher keinen Tooltip, nur Start und Ende.
 */
export const TIPPBARE_EBENEN = [
  "ablauf-bewertet",
  "ablauf-unbewertet",
  "ablauf-live",
  "ablauf-luecke",
  "ablauf-luecke-live",
  "halte",
  "marker",
  "winkel",
  "start",
  "ende",
] as const;

/**
 * Rechnet ein Strichmuster aus Bildpunkten ins Maß von MapLibre um:
 * line-dasharray zählt in Linienbreiten, nicht in Pixeln. 6/6 px bei 4 px
 * Breite sind also [1.5, 1.5].
 */
export function strichmuster(strich: number, luecke: number, breite: number): [number, number] {
  return [strich / breite, luecke / breite];
}

/** Kantenlänge eines Markerbilds in Bildpunkten (Pixelverhältnis 2: das Bild hat die doppelte Zahl an Pixeln). */
const MARKER_BILD_GROESSE = 24;
const MARKER_BILD_FAKTOR = 2;
/** Name eines Markerbilds in der Karte: Präfix + Form. */
export const MARKER_BILD_PRAEFIX = "marker-";

const ABLAUF_BREITE = 4;
/** Winkelnummer als Plakette rechts oberhalb des Knicks, damit ein Marker genau am Winkel sichtbar bleibt. */
const WINKEL_VERSATZ: [number, number] = [10, -10];
// Die Farbe steht je Merkmal in den Daten (spur-daten.ts), nicht in der Ebene.
const FARBE: ExpressionSpecification = ["get", "farbe"];

export function spurEbenen(): LayerSpecification[] {
  return [
    // Deutlich breiter als der Ablauf (4), der darüber gezeichnet wird: Die
    // Legung erscheint als farbiger Korridor, der Ablauf als Linie darin.
    // 6 px waren zu wenig - dort, wo der Hund sauber sucht, verschwand die
    // Legung unter dem Ablauf. 9 px waren auf Test gut sichtbar, aber
    // breiter als nötig; 7 px lassen beidseits des Ablaufs noch einen
    // klaren Saum.
    {
      id: "legung-fassung",
      type: "line",
      source: "legung-linie",
      layout: { "line-join": "round", "line-cap": "round" },
      paint: { "line-color": TRACK_CASING_COLOR, "line-width": 10, "line-opacity": 0.5 },
    },
    {
      id: "legung",
      type: "line",
      source: "legung-linie",
      layout: { "line-join": "round", "line-cap": "round" },
      paint: { "line-color": TRACK_LINE_COLOR, "line-width": 7, "line-opacity": 0.9 },
    },
    {
      id: "ablauf-bewertet",
      type: "line",
      source: "ablauf-bewertet",
      layout: { "line-join": "round", "line-cap": "round" },
      paint: { "line-color": FARBE, "line-width": ABLAUF_BREITE },
    },
    {
      id: "ablauf-unbewertet",
      type: "line",
      source: "ablauf-unbewertet",
      paint: {
        "line-color": FARBE,
        "line-width": ABLAUF_BREITE,
        "line-dasharray": strichmuster(6, 6, ABLAUF_BREITE),
      },
    },
    // Der laufende Versuch dichter gestrichelt als die gespeicherten, damit
    // die frische Linie sich klar abhebt.
    {
      id: "ablauf-live",
      type: "line",
      source: "ablauf-live",
      paint: {
        "line-color": FARBE,
        "line-width": ABLAUF_BREITE,
        "line-dasharray": strichmuster(3, 4, ABLAUF_BREITE),
      },
    },
    // Stücke ohne Aufzeichnung: grau gestrichelt statt Ampel- oder Versuchsfarbe (Lückenregel: lib/ablauf-luecken.ts).
    ...["ablauf-luecke", "ablauf-luecke-live"].map(
      (id): LayerSpecification => ({
        id,
        type: "line",
        source: id,
        paint: {
          "line-color": FARBE,
          "line-width": ABLAUF_BREITE,
          "line-dasharray": strichmuster(2, 5, ABLAUF_BREITE),
        },
      }),
    ),
    {
      id: "halte",
      type: "circle",
      source: "halte",
      paint: {
        "circle-radius": 9,
        "circle-opacity": 0,
        "circle-stroke-color": FARBE,
        "circle-stroke-width": 3,
      },
    },
    // Die Nummer jedes Winkels der Legung, klein auf der Linie. Die Schrift liegt als Glyphen
    // neben dem Kartenstil (public/karte/schriften), auch beim Luftbild.
    {
      id: "winkel",
      type: "circle",
      source: "winkel",
      paint: {
        "circle-radius": 8,
        "circle-color": FARBE,
        "circle-opacity": 0.95,
        "circle-stroke-color": "#ffffff",
        "circle-stroke-width": 2,
        "circle-translate": WINKEL_VERSATZ,
      },
    },
    {
      id: "winkel-nummer",
      type: "symbol",
      source: "winkel",
      layout: {
        "text-field": ["get", "nummer"],
        "text-font": ["noto_sans_bold"],
        "text-size": 11,
        "text-allow-overlap": true,
        "text-ignore-placement": true,
      },
      paint: { "text-color": "#ffffff", "text-translate": WINKEL_VERSATZ },
    },
    // Die Marker als Bilder mit eigener Form je Art (markerBilder): Die Form trägt die
    // Unterscheidung, die Farbe hilft nur. Die Bilder legt ebenenAnlegen an.
    // Über den Winkelnummern: Ein Fähnchen steht oft genau am Winkel und verschwand sonst darunter.
    {
      id: "marker",
      type: "symbol",
      source: "marker",
      layout: {
        "icon-image": ["concat", MARKER_BILD_PRAEFIX, ["get", "form"]],
        "icon-allow-overlap": true,
        "icon-ignore-placement": true,
      },
    },
    // Start gefüllt, Ende hohl - dieselbe Farbe wie die Linie, statt Grün und
    // Rot: beides sind Ampeltöne und meinten hier gerade NICHT gut und
    // schlecht, sondern Anfang und Ende der Legung.
    {
      id: "start",
      type: "circle",
      source: "legung-enden",
      filter: ["==", ["get", "rolle"], "start"],
      paint: {
        "circle-radius": 8,
        "circle-color": TRACK_LINE_COLOR,
        "circle-stroke-color": "#ffffff",
        "circle-stroke-width": 3,
      },
    },
    {
      id: "ende",
      type: "circle",
      source: "legung-enden",
      filter: ["==", ["get", "rolle"], "ende"],
      paint: {
        "circle-radius": 8,
        "circle-color": "#ffffff",
        "circle-stroke-color": TRACK_LINE_COLOR,
        "circle-stroke-width": 4,
      },
    },
    {
      id: "position",
      type: "circle",
      source: "position",
      paint: {
        "circle-radius": 6,
        "circle-color": FARBE,
        "circle-stroke-color": FARBE,
        "circle-stroke-width": 3,
      },
    },
  ];
}

/**
 * Zeichnet eine Markerform mit dunklem Rand und hellem Saum, damit sie auf Luftbild und heller wie
 * dunkler Karte steht. `groesse` ist die Kantenlänge des Bildes, die Form liegt mit Rand darin.
 */
export function zeichneMarkerForm(ctx: CanvasRenderingContext2D, art: MarkerArt, groesse: number): void {
  ctx.beginPath();
  if (art.umriss === "kreis") {
    ctx.arc(groesse / 2, groesse / 2, 0.3 * groesse, 0, Math.PI * 2);
  } else {
    art.umriss.forEach(([x, y], i) => (i === 0 ? ctx.moveTo(x * groesse, y * groesse) : ctx.lineTo(x * groesse, y * groesse)));
    ctx.closePath();
  }
  ctx.lineJoin = "round";
  // Der Strich liegt je zur Hälfte innen und außen; die Füllung deckt die innere Hälfte,
  // sichtbar bleiben außen 2 px dunkler Rand und darum 1,5 px weißer Saum.
  ctx.strokeStyle = "#ffffff";
  ctx.lineWidth = 7;
  ctx.stroke();
  ctx.strokeStyle = MARKER_RAND_FARBE;
  ctx.lineWidth = 4;
  ctx.stroke();
  ctx.fillStyle = art.farbe;
  ctx.fill();
}

function markerBild(art: MarkerArt): ImageData | null {
  const leinwand = document.createElement("canvas");
  leinwand.width = leinwand.height = MARKER_BILD_GROESSE * MARKER_BILD_FAKTOR;
  const ctx = leinwand.getContext("2d");
  if (!ctx) return null;
  ctx.scale(MARKER_BILD_FAKTOR, MARKER_BILD_FAKTOR);
  zeichneMarkerForm(ctx, art, MARKER_BILD_GROESSE);
  return ctx.getImageData(0, 0, leinwand.width, leinwand.height);
}

// Die GeoJSON-Form, die setData annimmt - ohne @types/geojson als eigene
// Abhängigkeit anzufassen.
type GeoJsonDaten = Exclude<Parameters<GeoJSONSource["setData"]>[0], string>;

/** Alle Quellen mit ihren aktuellen Daten. */
export type SpurDatensatz = Record<SpurQuelle, Merkmale<Linie> | Merkmale<Punkt>>;

export function leererDatensatz(): SpurDatensatz {
  return Object.fromEntries(SPUR_QUELLEN.map((id) => [id, leer()])) as SpurDatensatz;
}

/**
 * Legt die eigenen Quellen und Ebenen über dem Grundstil an - und zwar nur,
 * was fehlt. Nach jedem Stilwechsel ("style.load") sind sie weg, denn setStyle
 * ersetzt den ganzen Stil; so lässt sich dieselbe Funktion jedes Mal aufrufen.
 * Gefüllt wird mit dem AKTUELLEN Stand, nicht mit dem vom Erzeugen der Karte.
 */
export function ebenenAnlegen(map: KartenObjekt, daten: SpurDatensatz): void {
  for (const id of SPUR_QUELLEN) {
    if (!map.getSource(id)) map.addSource(id, { type: "geojson", data: daten[id] as GeoJsonDaten });
  }
  // Bilder gehen mit dem Stil verloren wie die Ebenen - und werden hier ebenso nur ergänzt.
  for (const art of MARKER_FORMEN) {
    const id = MARKER_BILD_PRAEFIX + art.form;
    if (map.hasImage(id)) continue;
    const bild = markerBild(art);
    if (bild) map.addImage(id, bild, { pixelRatio: MARKER_BILD_FAKTOR });
  }
  for (const ebene of spurEbenen()) {
    if (!map.getLayer(ebene.id)) map.addLayer(ebene);
  }
}

/** Schreibt neue Daten in die betroffenen Quellen; was es (noch) nicht gibt, überspringt es. */
export function datenAnwenden(map: KartenObjekt | null, teil: Partial<SpurDatensatz>): void {
  if (!map) return;
  for (const id of SPUR_QUELLEN) {
    const daten = teil[id];
    if (daten) (map.getSource(id) as GeoJSONSource | undefined)?.setData(daten as GeoJsonDaten);
  }
}

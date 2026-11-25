import type { Map as KartenObjekt } from "maplibre-gl";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  MARKER_BILD_PRAEFIX,
  SPUR_QUELLEN,
  TIPPBARE_EBENEN,
  datenAnwenden,
  ebenenAnlegen,
  leererDatensatz,
  spurEbenen,
  strichmuster,
} from "./spur-ebenen";
import { MARKER_FORMEN, TRACK_CASING_COLOR, TRACK_LINE_COLOR, leer } from "./spur-daten";

describe("strichmuster", () => {
  it("rechnet Bildpunkte in Linienbreiten um", () => {
    // 6/6 px bei 4 px Breite (unbewerteter Ablauf), 3/4 px (laufender).
    expect(strichmuster(6, 6, 4)).toStrictEqual([1.5, 1.5]);
    expect(strichmuster(3, 4, 4)).toStrictEqual([0.75, 1]);
  });
});

describe("spurEbenen", () => {
  const ebenen = spurEbenen();
  const ids = ebenen.map((e) => e.id);

  it("zeichnet in der vorgesehenen Reihenfolge", () => {
    expect(ids).toStrictEqual([
      "legung-fassung",
      "legung",
      "ablauf-bewertet",
      "ablauf-unbewertet",
      "ablauf-live",
      "ablauf-luecke",
      "ablauf-luecke-live",
      "halte",
      "winkel",
      "winkel-nummer",
      // Marker über den Winkelnummern: ein Fähnchen genau am Winkel blieb sonst darunter verborgen.
      "marker",
      "start",
      "ende",
      "position",
    ]);
  });

  it("hat nur Ebenen mit bekannter Quelle, und jede Quelle wird gebraucht", () => {
    const quellen = ebenen.map((e) => ("source" in e ? e.source : undefined));
    expect(quellen.every((q) => SPUR_QUELLEN.includes(q as (typeof SPUR_QUELLEN)[number]))).toBe(true);
    expect(new Set(quellen)).toStrictEqual(new Set(SPUR_QUELLEN));
  });

  it("macht genau Ebenen antippbar, die es gibt", () => {
    for (const id of TIPPBARE_EBENEN) expect(ids).toContain(id);
    // Die Legung selbst und die eigene Position haben keinen Text.
    expect(TIPPBARE_EBENEN).not.toContain("legung");
    expect(TIPPBARE_EBENEN).not.toContain("position");
  });

  it("zeichnet die Legung 7 px in Magenta mit 10 px dunkler Fassung darunter", () => {
    const fassung = ebenen.find((e) => e.id === "legung-fassung");
    const kern = ebenen.find((e) => e.id === "legung");
    expect(fassung).toMatchObject({ paint: { "line-color": TRACK_CASING_COLOR, "line-width": 10, "line-opacity": 0.5 } });
    expect(kern).toMatchObject({ paint: { "line-color": TRACK_LINE_COLOR, "line-width": 7, "line-opacity": 0.9 } });
  });

  it("strichelt unbewertete und laufende Abläufe wie bisher in Pixeln gedacht", () => {
    const unbewertet = ebenen.find((e) => e.id === "ablauf-unbewertet");
    const live = ebenen.find((e) => e.id === "ablauf-live");
    const bewertet = ebenen.find((e) => e.id === "ablauf-bewertet");
    expect(unbewertet).toMatchObject({ paint: { "line-width": 4, "line-dasharray": [1.5, 1.5] } });
    expect(live).toMatchObject({ paint: { "line-width": 4, "line-dasharray": [0.75, 1] } });
    expect(bewertet).toMatchObject({ paint: { "line-width": 4 } });
    expect((bewertet as { paint: object }).paint).not.toHaveProperty("line-dasharray");
  });

  it("liest die Farbe der Abläufe, Halte und Marker aus den Daten", () => {
    for (const id of ["ablauf-bewertet", "ablauf-unbewertet", "ablauf-live"]) {
      expect(ebenen.find((e) => e.id === id)).toMatchObject({ paint: { "line-color": ["get", "farbe"] } });
    }
    expect(ebenen.find((e) => e.id === "halte")).toMatchObject({
      paint: { "circle-radius": 9, "circle-opacity": 0, "circle-stroke-width": 3 },
    });
  });

  it("zeichnet Marker als Bilder je Form, die sich überlagern dürfen", () => {
    expect(ebenen.find((e) => e.id === "marker")).toMatchObject({
      type: "symbol",
      layout: {
        "icon-image": ["concat", MARKER_BILD_PRAEFIX, ["get", "form"]],
        "icon-allow-overlap": true,
        "icon-ignore-placement": true,
      },
    });
  });

  it("zeichnet Start gefüllt und Ende hohl", () => {
    expect(ebenen.find((e) => e.id === "start")).toMatchObject({
      filter: ["==", ["get", "rolle"], "start"],
      paint: { "circle-color": TRACK_LINE_COLOR, "circle-stroke-color": "#ffffff", "circle-stroke-width": 3 },
    });
    expect(ebenen.find((e) => e.id === "ende")).toMatchObject({
      filter: ["==", ["get", "rolle"], "ende"],
      paint: { "circle-color": "#ffffff", "circle-stroke-color": TRACK_LINE_COLOR, "circle-stroke-width": 4 },
    });
  });
});

/** Ein Dokument, dessen Leinwand alles annimmt: ebenenAnlegen zeichnet die Markerbilder darauf. */
function falscheLeinwand() {
  const ctx = new Proxy({ getImageData: (_x: number, _y: number, breite: number, hoehe: number) => ({ width: breite, height: hoehe }) } as Record<string, unknown>, {
    get: (ziel, name: string) => ziel[name] ?? (() => undefined),
    set: () => true,
  });
  vi.stubGlobal("document", { createElement: () => ({ width: 0, height: 0, getContext: () => ctx }) });
}

afterEach(() => vi.unstubAllGlobals());

/** Eine Karte, die nur kann, was ebenenAnlegen und datenAnwenden brauchen. */
function falscheKarte() {
  falscheLeinwand();
  const bilder = new Set<string>();
  const quellen = new Map<string, { setData: ReturnType<typeof vi.fn> }>();
  const ebenen = new Set<string>();
  const karte = {
    getSource: (id: string) => quellen.get(id),
    addSource: vi.fn((id: string) => quellen.set(id, { setData: vi.fn() })),
    getLayer: (id: string) => (ebenen.has(id) ? { id } : undefined),
    addLayer: vi.fn((ebene: { id: string }) => ebenen.add(ebene.id)),
    hasImage: (id: string) => bilder.has(id),
    addImage: vi.fn((id: string) => bilder.add(id)),
  };
  return { karte: karte as unknown as KartenObjekt, rohling: karte, quellen, ebenen, bilder };
}

describe("ebenenAnlegen", () => {
  it("legt alle Quellen und Ebenen an", () => {
    const { karte, rohling, quellen, ebenen } = falscheKarte();
    ebenenAnlegen(karte, leererDatensatz());
    expect([...quellen.keys()]).toStrictEqual([...SPUR_QUELLEN]);
    expect(ebenen.size).toBe(spurEbenen().length);
    expect(rohling.addLayer).toHaveBeenCalledTimes(spurEbenen().length);
  });

  it("registriert für jede Markerform ein Bild mit Pixelverhältnis 2", () => {
    const { karte, rohling, bilder } = falscheKarte();
    ebenenAnlegen(karte, leererDatensatz());
    expect([...bilder]).toStrictEqual(MARKER_FORMEN.map((a) => MARKER_BILD_PRAEFIX + a.form));
    expect(rohling.addImage).toHaveBeenCalledWith(expect.any(String), expect.anything(), { pixelRatio: 2 });
  });

  it("legt nichts doppelt an, wenn es schon da ist", () => {
    const { karte, rohling } = falscheKarte();
    ebenenAnlegen(karte, leererDatensatz());
    ebenenAnlegen(karte, leererDatensatz());
    expect(rohling.addImage).toHaveBeenCalledTimes(MARKER_FORMEN.length);
    expect(rohling.addSource).toHaveBeenCalledTimes(SPUR_QUELLEN.length);
    expect(rohling.addLayer).toHaveBeenCalledTimes(spurEbenen().length);
  });

  it("füllt die Quellen mit dem Stand, der ihm übergeben wird", () => {
    const { karte, rohling } = falscheKarte();
    const daten = leererDatensatz();
    daten.halte = { type: "FeatureCollection", features: [{ type: "Feature", properties: { text: "x" }, geometry: { type: "Point", coordinates: [8, 50] } }] };
    ebenenAnlegen(karte, daten);
    expect(rohling.addSource).toHaveBeenCalledWith("halte", { type: "geojson", data: daten.halte });
  });
});

describe("datenAnwenden", () => {
  it("schreibt nur in die genannten Quellen", () => {
    const { karte, quellen } = falscheKarte();
    ebenenAnlegen(karte, leererDatensatz());
    const neu = leer<never>();
    datenAnwenden(karte, { marker: neu });
    expect(quellen.get("marker")?.setData).toHaveBeenCalledWith(neu);
    expect(quellen.get("halte")?.setData).not.toHaveBeenCalled();
  });

  it("überspringt Quellen, die es noch nicht gibt, und eine fehlende Karte", () => {
    const { karte } = falscheKarte();
    expect(() => datenAnwenden(karte, { marker: leer<never>() })).not.toThrow();
    expect(() => datenAnwenden(null, { marker: leer<never>() })).not.toThrow();
  });
});

import type { StyleSpecification } from "maplibre-gl";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { absolutMachen, luftbildStil, stilLaden, stilSpeicherLeeren, stilWaehlen } from "./stil";

const URSPRUNG = "https://dogity.example";

const STIL: StyleSpecification = {
  version: 8,
  glyphs: "/karte/schriften/{fontstack}/{range}.pbf",
  sprite: [{ id: "base", url: "/karte/symbole/base" }],
  sources: {},
  layers: [],
};

describe("stilWaehlen", () => {
  it("nimmt für die Straße hell oder dunkel nach dem Modus", () => {
    expect(stilWaehlen("strasse", false)).toBe("hell");
    expect(stilWaehlen("strasse", true)).toBe("dunkel");
  });

  it("kennt vom Luftbild nur eine Fassung", () => {
    expect(stilWaehlen("luftbild", false)).toBe("luftbild");
    expect(stilWaehlen("luftbild", true)).toBe("luftbild");
  });
});

describe("absolutMachen", () => {
  it("setzt den Ursprung vor Schriften und Symbole", () => {
    const ergebnis = absolutMachen(STIL, URSPRUNG);
    expect(ergebnis.glyphs).toBe(`${URSPRUNG}/karte/schriften/{fontstack}/{range}.pbf`);
    expect(ergebnis.sprite).toStrictEqual([{ id: "base", url: `${URSPRUNG}/karte/symbole/base` }]);
  });

  it("lässt die Platzhalter stehen, statt sie zu codieren", () => {
    // new URL(...) machte aus {fontstack} ein %7Bfontstack%7D.
    const glyphs = absolutMachen(STIL, URSPRUNG).glyphs as string;
    expect(glyphs).toContain("{fontstack}");
    expect(glyphs).toContain("{range}");
    expect(glyphs).not.toContain("%7B");
  });

  it("versteht ein einzelnes Symbol als Zeichenkette", () => {
    const ergebnis = absolutMachen({ ...STIL, sprite: "/karte/symbole/base" }, URSPRUNG);
    expect(ergebnis.sprite).toBe(`${URSPRUNG}/karte/symbole/base`);
  });

  it("lässt bereits absolute Adressen in Ruhe", () => {
    const ergebnis = absolutMachen(
      { ...STIL, glyphs: "https://fonts.example/{fontstack}/{range}.pbf", sprite: "https://sprites.example/s" },
      URSPRUNG,
    );
    expect(ergebnis.glyphs).toBe("https://fonts.example/{fontstack}/{range}.pbf");
    expect(ergebnis.sprite).toBe("https://sprites.example/s");
  });

  it("verändert das übergebene Objekt nicht", () => {
    const vorher = JSON.stringify(STIL);
    absolutMachen(STIL, URSPRUNG);
    expect(JSON.stringify(STIL)).toBe(vorher);
  });
});

describe("luftbildStil", () => {
  const stil = luftbildStil(URSPRUNG);

  it("besteht aus einer Rasterquelle und einer Rasterebene", () => {
    expect(stil.version).toBe(8);
    expect(Object.keys(stil.sources)).toStrictEqual(["luftbild"]);
    expect(stil.layers).toHaveLength(1);
    expect(stil.layers[0]).toMatchObject({ type: "raster", source: "luftbild" });
  });

  it("holt die Kacheln von Esri World Imagery, mit Namensnennung", () => {
    const quelle = stil.sources.luftbild;
    expect(quelle).toMatchObject({
      type: "raster",
      tileSize: 256,
      maxzoom: 19,
      attribution: "&copy; Esri, Maxar, Earthstar Geographics",
    });
    const kacheln = (quelle as { tiles: string[] }).tiles;
    expect(kacheln).toStrictEqual([
      "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
    ]);
  });

  it("verweist auf die eigenen Schriften, absolut", () => {
    expect(stil.glyphs).toBe(`${URSPRUNG}/karte/schriften/{fontstack}/{range}.pbf`);
  });
});

describe("stilLaden", () => {
  const abruf = vi.fn();

  function antwort(inhalt: unknown, ok = true) {
    return { ok, status: ok ? 200 : 500, text: async () => JSON.stringify(inhalt) };
  }

  beforeEach(() => {
    stilSpeicherLeeren();
    abruf.mockReset();
    vi.stubGlobal("fetch", abruf);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("holt hell und dunkel aus ihren Dateien und macht die Adressen absolut", async () => {
    abruf.mockResolvedValue(antwort(STIL));
    const stil = await stilLaden("dunkel", URSPRUNG);
    expect(abruf).toHaveBeenCalledWith("/karte/stil-dunkel.json");
    expect(stil.glyphs).toBe(`${URSPRUNG}/karte/schriften/{fontstack}/{range}.pbf`);
  });

  it("holt für das Luftbild nichts", async () => {
    const stil = await stilLaden("luftbild", URSPRUNG);
    expect(abruf).not.toHaveBeenCalled();
    expect(Object.keys(stil.sources)).toStrictEqual(["luftbild"]);
  });

  it("merkt sich den Text, gibt aber jedes Mal ein frisches Objekt", async () => {
    abruf.mockResolvedValue(antwort(STIL));
    const erstes = await stilLaden("hell", URSPRUNG);
    // MapLibre verändert, was man ihm übergibt.
    erstes.layers.push({ id: "fremd", type: "background" });

    const zweites = await stilLaden("hell", URSPRUNG);
    expect(abruf).toHaveBeenCalledTimes(1);
    expect(zweites).not.toBe(erstes);
    expect(zweites.layers).toStrictEqual([]);
  });

  it("teilt eine laufende Anfrage, statt sie doppelt zu stellen", async () => {
    abruf.mockResolvedValue(antwort(STIL));
    await Promise.all([stilLaden("hell", URSPRUNG), stilLaden("hell", URSPRUNG)]);
    expect(abruf).toHaveBeenCalledTimes(1);
  });

  it("merkt sich einen Fehlschlag nicht", async () => {
    abruf.mockResolvedValueOnce(antwort({}, false));
    await expect(stilLaden("hell", URSPRUNG)).rejects.toThrow();

    abruf.mockResolvedValueOnce(antwort(STIL));
    await expect(stilLaden("hell", URSPRUNG)).resolves.toMatchObject({ version: 8 });
    expect(abruf).toHaveBeenCalledTimes(2);
  });

  it("merkt sich auch einen Netzfehler nicht", async () => {
    abruf.mockRejectedValueOnce(new TypeError("offline"));
    await expect(stilLaden("dunkel", URSPRUNG)).rejects.toThrow("offline");

    abruf.mockResolvedValueOnce(antwort(STIL));
    await expect(stilLaden("dunkel", URSPRUNG)).resolves.toMatchObject({ version: 8 });
  });
});

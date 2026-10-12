import { describe, expect, it } from "vitest";
import { uebersetze } from "@/lib/i18n";
import { AMBER_MAX_M, GREEN_MAX_M } from "@/components/tracking/walk-run-evaluation";
import type { GpsWalkPoint, GpsWalkRun, GpsWalkStop } from "@/lib/types";
import {
  DEVIATION_COLORS,
  MARKER_COLORS,
  STOP_COLORS,
  WALK_RUN_COLORS,
  ablaufDaten,
  kartenGrenzen,
  legungDaten,
  leer,
  liveAblaufDaten,
  type KartenPunkt,
} from "./spur-daten";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);

function punkt(latitude: number, longitude: number, rest: Partial<KartenPunkt> = {}): KartenPunkt {
  return { latitude, longitude, ...rest };
}

function laufPunkt(latitude: number, longitude: number, deviationMeters: number | null): GpsWalkPoint {
  return { latitude, longitude, timestamp: "2026-10-12T10:00:00Z", accuracy: 4, deviationMeters };
}

function lauf(points: GpsWalkPoint[], stops: GpsWalkStop[] = []): GpsWalkRun {
  return {
    id: "r",
    trackId: "t",
    createdAt: "2026-10-12T10:00:00Z",
    lengthMeters: null,
    comment: null,
    points,
    avgDeviationMeters: null,
    maxDeviationMeters: null,
    onTrackPercent: null,
    articlesFound: null,
    articlesTotal: null,
    evaluatedAt: null,
    stops,
  };
}

describe("legungDaten", () => {
  const legung = [punkt(50, 8), punkt(50.001, 8.001), punkt(50.002, 8.002)];

  it("schreibt Koordinaten als [lon, lat]", () => {
    const d = legungDaten(legung, false, t);
    expect(d.linie.features[0].geometry.coordinates).toStrictEqual([
      [8, 50],
      [8.001, 50.001],
      [8.002, 50.002],
    ]);
    expect(d.enden.features[0].geometry.coordinates).toStrictEqual([8, 50]);
  });

  it("lässt manuelle Marker aus der Linie und aus Start/Ende heraus", () => {
    const mitMarker = [
      punkt(50, 8),
      punkt(50.0005, 8.0005, { pointType: 1, label: "Weit weg" }),
      punkt(50.001, 8.001),
    ];
    const d = legungDaten(mitMarker, false, t);
    expect(d.linie.features[0].geometry.coordinates).toStrictEqual([
      [8, 50],
      [8.001, 50.001],
    ]);
    expect(d.enden.features.map((f) => f.properties.rolle)).toStrictEqual(["start", "ende"]);
    expect(d.enden.features[1].geometry.coordinates).toStrictEqual([8.001, 50.001]);
    expect(d.marker.features).toHaveLength(1);
    expect(d.marker.features[0].geometry.coordinates).toStrictEqual([8.0005, 50.0005]);
  });

  it("hat im Live-Modus einen Start, aber kein Ende", () => {
    const d = legungDaten(legung, true, t);
    expect(d.enden.features.map((f) => f.properties.rolle)).toStrictEqual(["start"]);
  });

  it("beschriftet Start und Ende mit den bisherigen Tooltip-Texten", () => {
    const d = legungDaten(legung, false, t);
    expect(d.enden.features.map((f) => f.properties.text)).toStrictEqual(["Start (gelegt)", "Ende (gelegt)"]);
  });

  it("zeichnet mit einem Punkt keine Linie, aber den Start", () => {
    const d = legungDaten([punkt(50, 8)], true, t);
    expect(d.linie.features).toStrictEqual([]);
    expect(d.enden.features).toHaveLength(1);
  });

  it("ist ohne Punkte leer", () => {
    const d = legungDaten([], false, t);
    expect(d.linie.features).toStrictEqual([]);
    expect(d.enden.features).toStrictEqual([]);
    expect(d.marker.features).toStrictEqual([]);
  });

  it("färbt und beschriftet Marker nach ihrer Art", () => {
    const d = legungDaten(
      [0, 1, 2, 3, 9].map((markerType, i) => punkt(50 + i / 1000, 8, { pointType: 1, markerType })),
      false,
      t,
    );
    expect(d.marker.features.map((f) => f.properties.farbe)).toStrictEqual([
      MARKER_COLORS[0],
      MARKER_COLORS[1],
      MARKER_COLORS[2],
      MARKER_COLORS[3],
      // Unbekannte Art: wie "Marker".
      MARKER_COLORS[3],
    ]);
    expect(d.marker.features.map((f) => f.properties.text)).toStrictEqual([
      "Gegenstand",
      "Leckerlipot",
      "Verleitung",
      "Marker",
      "Marker",
    ]);
  });

  it("nimmt ohne markerType die Art Gegenstand", () => {
    const d = legungDaten([punkt(50, 8, { pointType: 1 })], false, t);
    expect(d.marker.features[0].properties).toStrictEqual({ farbe: MARKER_COLORS[0], text: "Gegenstand" });
  });

  it("gibt das Label von Nutzer:innen unverändert als Text weiter", () => {
    // Weder maskiert noch entfernt: Angezeigt wird es über Popup.setText.
    const label = `<img src=x onerror="alert('x')"> & Wald`;
    const d = legungDaten([punkt(50, 8, { pointType: 1, markerType: 0, label })], false, t);
    expect(d.marker.features[0].properties.text).toBe(label);
  });

  it("fällt bei leerem Label auf die Beschriftung der Art zurück", () => {
    const d = legungDaten([punkt(50, 8, { pointType: 1, markerType: 2, label: "" })], false, t);
    expect(d.marker.features[0].properties.text).toBe("Verleitung");
  });
});

describe("ablaufDaten", () => {
  // Zwei Punkte mit Abweichung links und rechts, damit beide Endpunkte zählen.
  function segmentFarbe(a: number | null, b: number | null): string {
    const d = ablaufDaten([lauf([laufPunkt(50, 8, a), laufPunkt(50.0001, 8, b)])], t);
    return d.bewertet.features[0].properties.farbe as string;
  }

  it("färbt Segmente an den Schwellen der Auswertung", () => {
    expect(segmentFarbe(0, GREEN_MAX_M)).toBe(DEVIATION_COLORS.green);
    expect(segmentFarbe(0, GREEN_MAX_M + 0.01)).toBe(DEVIATION_COLORS.amber);
    expect(segmentFarbe(0, AMBER_MAX_M)).toBe(DEVIATION_COLORS.amber);
    expect(segmentFarbe(0, AMBER_MAX_M + 0.01)).toBe(DEVIATION_COLORS.red);
  });

  it("nimmt je Segment die Farbe des schlechteren Endpunkts", () => {
    expect(segmentFarbe(7, 1)).toBe(DEVIATION_COLORS.red);
    expect(segmentFarbe(1, 7)).toBe(DEVIATION_COLORS.red);
    expect(segmentFarbe(1, 4)).toBe(DEVIATION_COLORS.amber);
  });

  it("zählt eine fehlende Abweichung innerhalb eines bewerteten Ablaufs als 0", () => {
    expect(segmentFarbe(null, 2)).toBe(DEVIATION_COLORS.green);
  });

  it("macht aus einem bewerteten Ablauf ein Merkmal je Segment, mit [lon, lat]", () => {
    const d = ablaufDaten(
      [lauf([laufPunkt(50, 8, 1), laufPunkt(50.001, 8.001, 2), laufPunkt(50.002, 8.002, 8)])],
      t,
    );
    expect(d.bewertet.features).toHaveLength(2);
    expect(d.bewertet.features[1].geometry.coordinates).toStrictEqual([
      [8.001, 50.001],
      [8.002, 50.002],
    ]);
    expect(d.unbewertet.features).toStrictEqual([]);
  });

  it("nennt Nummer und gerundete Abweichung im Text", () => {
    const d = ablaufDaten(
      [lauf([laufPunkt(50, 8, 1)]), lauf([laufPunkt(50, 8, 1.2), laufPunkt(50.001, 8, 4.6)])],
      t,
    );
    expect(d.bewertet.features[0].properties.text).toBe("Ablauf 2: 5 m Abweichung");
  });

  it("zeichnet einen unbewerteten Ablauf als ganze Linie in der Zyklusfarbe", () => {
    const punkte = [laufPunkt(50, 8, null), laufPunkt(50.001, 8, null), laufPunkt(50.002, 8, null)];
    const d = ablaufDaten([lauf(punkte)], t);
    expect(d.bewertet.features).toStrictEqual([]);
    expect(d.unbewertet.features).toHaveLength(1);
    expect(d.unbewertet.features[0].geometry.coordinates).toHaveLength(3);
    expect(d.unbewertet.features[0].properties).toStrictEqual({
      farbe: WALK_RUN_COLORS[0],
      text: "Ablauf-Versuch 1",
    });
  });

  it("hängt die Farbe an die Position in der ganzen Liste und wiederholt sie im Zyklus", () => {
    const unbewertet = () => lauf([laufPunkt(50, 8, null), laufPunkt(50.001, 8, null)]);
    const bewertet = () => lauf([laufPunkt(50, 8, 1), laufPunkt(50.001, 8, 1)]);
    // Der bewertete Ablauf davor ändert nichts an den Farben der übrigen.
    const d = ablaufDaten([bewertet(), unbewertet(), unbewertet(), unbewertet(), unbewertet(), unbewertet()], t);
    expect(d.unbewertet.features.map((f) => f.properties.farbe)).toStrictEqual([
      WALK_RUN_COLORS[1],
      WALK_RUN_COLORS[2],
      WALK_RUN_COLORS[3],
      WALK_RUN_COLORS[0],
      WALK_RUN_COLORS[1],
    ]);
  });

  it("überspringt Abläufe ohne Punkte, behält aber die Nummerierung", () => {
    const d = ablaufDaten([lauf([]), lauf([laufPunkt(50, 8, null), laufPunkt(50.001, 8, null)])], t);
    expect(d.unbewertet.features).toHaveLength(1);
    expect(d.unbewertet.features[0].properties.text).toBe("Ablauf-Versuch 2");
  });

  it("zeichnet Halte als Punkte mit Farbe und Text je Art", () => {
    const stops: GpsWalkStop[] = [
      { latitude: 50.0001, longitude: 8.0001, durationSeconds: 12, kind: 0, markerLabel: null },
      { latitude: 50.0002, longitude: 8.0002, durationSeconds: 7, kind: 1, markerLabel: "Handschuh" },
      { latitude: 50.0003, longitude: 8.0003, durationSeconds: 3, kind: 2, markerLabel: null },
    ];
    const d = ablaufDaten([lauf([laufPunkt(50, 8, null), laufPunkt(50.001, 8, null)], stops)], t);
    expect(d.halte.features.map((f) => f.properties)).toStrictEqual([
      { farbe: STOP_COLORS[0], text: "Unerklärte Stockung: 12s" },
      { farbe: STOP_COLORS[1], text: "Verweisen: 7s (Handschuh)" },
      { farbe: STOP_COLORS[2], text: "Halt (erklärt): 3s" },
    ]);
    expect(d.halte.features[0].geometry.coordinates).toStrictEqual([8.0001, 50.0001]);
  });

  it("gibt das Label hinter einem Halt unverändert als Text weiter", () => {
    const stops: GpsWalkStop[] = [
      { latitude: 50, longitude: 8, durationSeconds: 5, kind: 1, markerLabel: "<b>fett</b>" },
    ];
    const d = ablaufDaten([lauf([laufPunkt(50, 8, null)], stops)], t);
    expect(d.halte.features[0].properties.text).toBe("Verweisen: 5s (<b>fett</b>)");
  });

  it("zeichnet Halte auch bei einem Ablauf mit nur einem Punkt", () => {
    const stops: GpsWalkStop[] = [{ latitude: 50, longitude: 8, durationSeconds: 5, kind: 2, markerLabel: null }];
    const d = ablaufDaten([lauf([laufPunkt(50, 8, null)], stops)], t);
    expect(d.unbewertet.features).toStrictEqual([]);
    expect(d.halte.features).toHaveLength(1);
  });
});

describe("liveAblaufDaten", () => {
  const punkte = [punkt(50, 8), punkt(50.001, 8.001)];

  it("nimmt die nächste Farbe im Zyklus nach den gespeicherten Versuchen", () => {
    expect(liveAblaufDaten(punkte, 0, t).farbe).toBe(WALK_RUN_COLORS[0]);
    expect(liveAblaufDaten(punkte, 2, t).farbe).toBe(WALK_RUN_COLORS[2]);
    expect(liveAblaufDaten(punkte, WALK_RUN_COLORS.length, t).farbe).toBe(WALK_RUN_COLORS[0]);
  });

  it("zeichnet Linie und Position in dieser Farbe, beschriftet als laufenden Versuch", () => {
    const d = liveAblaufDaten(punkte, 1, t);
    expect(d.linie.features[0].properties).toStrictEqual({ farbe: WALK_RUN_COLORS[1], text: "Ablauf-Versuch 2 (läuft)" });
    expect(d.linie.features[0].geometry.coordinates).toStrictEqual([
      [8, 50],
      [8.001, 50.001],
    ]);
    expect(d.position.features).toHaveLength(1);
    expect(d.position.features[0].properties.farbe).toBe(WALK_RUN_COLORS[1]);
    expect(d.letzter).toStrictEqual([8.001, 50.001]);
  });

  it("zeigt mit einem Punkt die Position, aber noch keine Linie", () => {
    const d = liveAblaufDaten([punkt(50, 8)], 0, t);
    expect(d.linie.features).toStrictEqual([]);
    expect(d.position.features).toHaveLength(1);
  });

  it("ist ohne Punkte leer", () => {
    for (const leerer of [undefined, []]) {
      const d = liveAblaufDaten(leerer, 0, t);
      expect(d.linie.features).toStrictEqual([]);
      expect(d.position.features).toStrictEqual([]);
      expect(d.letzter).toBeNull();
    }
  });
});

describe("kartenGrenzen", () => {
  it("ist ohne Punkte null", () => {
    expect(kartenGrenzen([], [])).toBeNull();
  });

  it("umfasst Legung samt Markern und alle Abläufe, als [lon, lat]", () => {
    const grenzen = kartenGrenzen(
      [punkt(50, 8), punkt(50.002, 8.001), punkt(49.999, 8.0005, { pointType: 1 })],
      [lauf([laufPunkt(50.001, 7.999, null), laufPunkt(50.0005, 8.004, null)])],
    );
    expect(grenzen).toStrictEqual([
      [7.999, 49.999],
      [8.004, 50.002],
    ]);
  });

  it("zieht bei Bedarf den laufenden Ablauf mit ein", () => {
    expect(kartenGrenzen([punkt(50, 8)], [], [punkt(50.01, 8.02)])).toStrictEqual([
      [8, 50],
      [8.02, 50.01],
    ]);
  });

  it("ergibt bei einem einzelnen Punkt ein Rechteck der Größe null", () => {
    expect(kartenGrenzen([punkt(50, 8)], [])).toStrictEqual([
      [8, 50],
      [8, 50],
    ]);
  });
});

describe("leer", () => {
  it("liefert jedes Mal eine neue, leere Sammlung", () => {
    const a = leer();
    const b = leer();
    expect(a).toStrictEqual({ type: "FeatureCollection", features: [] });
    expect(a).not.toBe(b);
    expect(a.features).not.toBe(b.features);
  });
});

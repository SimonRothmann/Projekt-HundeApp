import type { GpsWalkRun } from "@/lib/types";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { AMBER_MAX_M, GREEN_MAX_M } from "@/components/tracking/walk-run-evaluation";

/**
 * Die Fährte als GeoJSON für die Karte.
 *
 * Alles, was entscheidet, WAS gezeichnet wird und in welcher Farbe - nicht
 * WIE: Die Karte (track-map.tsx) legt nur Quellen und Ebenen an und füllt sie
 * hiermit. Koordinaten stehen in GeoJSON-Reihenfolge [lon, lat], nicht wie bei
 * Leaflet [lat, lon].
 */

// Kompatibel zu sowohl GpsPoint (pointType/label gesetzt) als auch
// GpsWalkPoint (kennt beide Felder nicht - zählt dann automatisch als
// "automatischer Punkt", siehe pointType !== 1 unten).
export type KartenPunkt = {
  latitude: number;
  longitude: number;
  pointType?: number;
  label?: string | null;
  markerType?: number;
};

/** Die Übersetzungsfunktion aus useT() - hier nur als Form, ohne React. */
export type TextUebersetzer = (text: string, werte?: Record<string, string | number>) => string;

export type Position = [number, number];
export type Eigenschaften = Record<string, string | number>;
export type Merkmal<G> = { type: "Feature"; properties: Eigenschaften; geometry: G };
export type Linie = { type: "LineString"; coordinates: Position[] };
export type Punkt = { type: "Point"; coordinates: Position };
export type Merkmale<G> = { type: "FeatureCollection"; features: Merkmal<G>[] };

// Eigene Farbe pro Ablauf-Versuch, damit mehrere Wiederholungen auf der
// Karte unterscheidbar bleiben (zyklisch wiederverwendet, falls mehr
// Versuche als Farben vorhanden sind). Gilt für den gerade laufenden und für
// noch nicht ausgewertete Abläufe.
//
// Nur kühle Blau- und Türkistöne: Violett lag zu nah am Magenta der Legung,
// Rot mitten in der Ampel der Auswertung - beides hätte genau die
// Verwechslung zurückgebracht, die die Farbordnung verhindern soll.
export const WALK_RUN_COLORS = ["#3b82f6", "#14b8a6", "#6366f1", "#06b6d4"];

// Fester Farbwert statt einer Theme-CSS-Variable: MapLibre parst Farben
// selbst und kennt kein var(...) - die Linie bliebe unsichtbar.
//
// Bewusst KEIN Ampelton mehr: die Legung war bis hierher im selben Grün
// gezeichnet wie ein sauber gelaufener Ablauf (DEVIATION_COLORS.green). Auf
// der Karte lagen dann zwei gleich grüne Linien übereinander, und was Legung
// und was Ablauf war, ließ sich nicht mehr auseinanderhalten. Die Legung ist
// auch keine Bewertung, sondern die Bezugslinie, an der gemessen wird - sie
// gehört deshalb gar nicht in die Ampelskala.
//
// Magenta mit dunkler Fassung. Der erste Versuch - dunkler Kern, helle
// Fassung - war auf Test kaum zu sehen: auf dem Luftbild und auf der dunklen
// Karte verschwand der dunkle Kern im Untergrund, übrig blieb ein dünner
// heller Doppelrand. Die Last muss der Kern tragen, nicht die Fassung.
// Magenta kommt in keinem Untergrund vor (weder Wiese, Acker noch Wald), ist
// in keiner Ampel und in keiner Ablauf-Farbe - und ist die übliche Farbe für
// die vorgegebene Route (Garmin, Luftfahrt-Navigation). Die dunkle Fassung
// trägt nur auf hellen Straßenkarten etwas bei.
export const TRACK_LINE_COLOR = "#e11dde";
export const TRACK_CASING_COLOR = "#1e1b2e";

// Ampelfarben für die Abweichung der Ablauf-Linie. Die Schwellen sind die der
// Auswertung (walk-run-evaluation.tsx, identisch zum GpsTrackEvaluator im
// Backend) - dieselben Zahlen, damit Karte, Auswertung und Fährtenbild nie
// verschiedene Farben für denselben Ablauf zeigen.
export const DEVIATION_COLORS = { green: "#16a34a", amber: "#d97706", red: "#dc2626" } as const;

export function deviationColor(meters: number | null | undefined): string | null {
  if (meters == null) return null;
  if (meters <= GREEN_MAX_M) return DEVIATION_COLORS.green;
  if (meters <= AMBER_MAX_M) return DEVIATION_COLORS.amber;
  return DEVIATION_COLORS.red;
}

// Beschriftung manueller Marker nach fachlicher Bedeutung (GpsMarkerType).
export const MARKER_LABELS = [
  uebersetzbar("Gegenstand"),
  uebersetzbar("Leckerlipot"),
  uebersetzbar("Verleitung"),
  uebersetzbar("Marker"),
] as const;
export const MARKER_COLORS = ["orange", "#a855f7", "#0ea5e9", "#94a3b8"] as const;

// Stockungen: unerklärt = Warnsignal (rot), Verweisen am Gegenstand = gut
// (grün), erklärt/neutral (grau).
export const STOP_COLORS = ["#dc2626", "#16a34a", "#94a3b8"] as const;
export const STOP_LABELS = [
  uebersetzbar("Unerklärte Stockung"),
  uebersetzbar("Verweisen"),
  uebersetzbar("Halt (erklärt)"),
] as const;

export function leer<G>(): Merkmale<G> {
  return { type: "FeatureCollection", features: [] };
}

function position(p: { latitude: number; longitude: number }): Position {
  return [p.longitude, p.latitude];
}

function punkt(p: { latitude: number; longitude: number }, properties: Eigenschaften): Merkmal<Punkt> {
  return { type: "Feature", properties, geometry: { type: "Point", coordinates: position(p) } };
}

function linie(koordinaten: Position[], properties: Eigenschaften): Merkmal<Linie> {
  return { type: "Feature", properties, geometry: { type: "LineString", coordinates: koordinaten } };
}

/** Eine Linie braucht mindestens zwei Punkte - bei einem bleibt nichts zu zeichnen. */
function linienMerkmale(koordinaten: Position[], properties: Eigenschaften): Merkmal<Linie>[] {
  return koordinaten.length >= 2 ? [linie(koordinaten, properties)] : [];
}

export type LegungDaten = {
  linie: Merkmale<Linie>;
  /** Start und (nur wenn nicht live) Ende, unterschieden über `rolle`. */
  enden: Merkmale<Punkt>;
  marker: Merkmale<Punkt>;
};

/**
 * Die gelegte Fährte: Linie, Start/Ende und die manuellen Marker.
 *
 * Linie/Start/Ende beziehen sich nur auf die automatischen GPS-Punkte -
 * manuell gesetzte Marker für gelegte Gegenstände (Schussstelle, Apportel
 * etc.) gehören nicht zur eigentlichen Laufstrecke und würden die
 * Linienführung verzerren.
 */
export function legungDaten(points: KartenPunkt[], live: boolean, t: TextUebersetzer): LegungDaten {
  const automatisch = points.filter((p) => p.pointType !== 1);
  const manuell = points.filter((p) => p.pointType === 1);

  const enden: Merkmal<Punkt>[] = [];
  if (automatisch.length > 0) {
    enden.push(punkt(automatisch[0], { rolle: "start", text: t("Start (gelegt)") }));
    // Das Ende gibt es erst, wenn die Legung abgeschlossen ist - live wandert
    // es mit jedem Punkt weiter und wäre nur ein zweiter Punkt unter der Position.
    if (!live) {
      enden.push(punkt(automatisch[automatisch.length - 1], { rolle: "ende", text: t("Ende (gelegt)") }));
    }
  }

  return {
    linie: { type: "FeatureCollection", features: linienMerkmale(automatisch.map(position), {}) },
    enden: { type: "FeatureCollection", features: enden },
    marker: {
      type: "FeatureCollection",
      features: manuell.map((p) => {
        const art = p.markerType ?? 0;
        return punkt(p, {
          farbe: MARKER_COLORS[art] ?? MARKER_COLORS[3],
          // Das Label stammt von Nutzer:innen - es geht als Text weiter und
          // wird als Text angezeigt (setText), nie als HTML.
          text: p.label || t(MARKER_LABELS[art] ?? MARKER_LABELS[3]),
        });
      }),
    },
  };
}

export type AblaufDaten = {
  /** Je Segment ein Merkmal, Farbe = die des schlechteren Endpunkts. */
  bewertet: Merkmale<Linie>;
  /** Ganze Linie je Ablauf, in der Zyklusfarbe (wird gestrichelt gezeichnet). */
  unbewertet: Merkmale<Linie>;
  halte: Merkmale<Punkt>;
};

/** Die abgelaufenen Versuche mit ihren Haltepunkten. */
export function ablaufDaten(walkRuns: GpsWalkRun[], t: TextUebersetzer): AblaufDaten {
  const bewertet: Merkmal<Linie>[] = [];
  const unbewertet: Merkmal<Linie>[] = [];
  const halte: Merkmal<Punkt>[] = [];

  walkRuns.forEach((run, index) => {
    if (run.points.length === 0) return;
    const ausweichfarbe = WALK_RUN_COLORS[index % WALK_RUN_COLORS.length];
    const koordinaten = run.points.map(position);

    if (run.points.some((p) => p.deviationMeters != null)) {
      // Je Segment die Farbe des schlechteren der beiden Endpunkte - so ist
      // auf einen Blick sichtbar, WO das Team abgekommen ist.
      for (let i = 0; i < koordinaten.length - 1; i++) {
        const schlechter = Math.max(run.points[i].deviationMeters ?? 0, run.points[i + 1].deviationMeters ?? 0);
        bewertet.push(
          linie([koordinaten[i], koordinaten[i + 1]], {
            farbe: deviationColor(schlechter) ?? ausweichfarbe,
            text: t("Ablauf {nr}: {meter} m Abweichung", { nr: index + 1, meter: Math.round(schlechter) }),
          }),
        );
      }
    } else {
      unbewertet.push(
        ...linienMerkmale(koordinaten, { farbe: ausweichfarbe, text: t("Ablauf-Versuch {nr}", { nr: index + 1 }) }),
      );
    }

    // Erkannte Halte als Ringe: zeigen die Stellen, an denen der Hund
    // gesucht/verwiesen hat - auch dort, wo die Position gar nicht abweicht.
    (run.stops ?? []).forEach((stop) => {
      halte.push(
        punkt(stop, {
          farbe: STOP_COLORS[stop.kind] ?? STOP_COLORS[2],
          // markerLabel kopiert der Server aus dem Label des nächsten
          // Markers - also ebenso Text von Nutzer:innen.
          text: `${t(STOP_LABELS[stop.kind] ?? uebersetzbar("Halt"))}: ${stop.durationSeconds}s${stop.markerLabel ? ` (${stop.markerLabel})` : ""}`,
        }),
      );
    });
  });

  return {
    bewertet: { type: "FeatureCollection", features: bewertet },
    unbewertet: { type: "FeatureCollection", features: unbewertet },
    halte: { type: "FeatureCollection", features: halte },
  };
}

export type LiveAblaufDaten = {
  linie: Merkmale<Linie>;
  /** Die eigene Position als Punkt; leer, solange es keinen Punkt gibt. */
  position: Merkmale<Punkt>;
  /** Farbe des laufenden Versuchs - auch für den Richtungspfeil. */
  farbe: string;
  /** Letzter Punkt als [lon, lat]. */
  letzter: Position | null;
};

/**
 * Der gerade laufende Ablauf-Versuch: nächste Farbe im Zyklus, damit sich die
 * frische Linie klar von den gespeicherten Versuchen abhebt (sie wird breiter
 * und dichter gestrichelt gezeichnet).
 */
export function liveAblaufDaten(
  liveWalkRunPoints: KartenPunkt[] | undefined,
  anzahlAblaeufe: number,
  t: TextUebersetzer,
): LiveAblaufDaten {
  const farbe = WALK_RUN_COLORS[anzahlAblaeufe % WALK_RUN_COLORS.length];
  const koordinaten = (liveWalkRunPoints ?? []).map(position);
  const letzter = koordinaten.length > 0 ? koordinaten[koordinaten.length - 1] : null;

  return {
    linie: {
      type: "FeatureCollection",
      features: linienMerkmale(koordinaten, {
        farbe,
        text: t("Ablauf-Versuch {nr} (läuft)", { nr: anzahlAblaeufe + 1 }),
      }),
    },
    position: {
      type: "FeatureCollection",
      features: letzter ? [{ type: "Feature", properties: { farbe }, geometry: { type: "Point", coordinates: letzter } }] : [],
    },
    farbe,
    letzter,
  };
}

/** Südwest- und Nordost-Ecke als [lon, lat] - die Form, die fitBounds annimmt. */
export type Grenzen = [Position, Position];

/**
 * Das Rechteck um alle Punkte: Legung samt Markern und alle Abläufe.
 * null, wenn es gar keinen Punkt gibt.
 */
export function kartenGrenzen(
  points: KartenPunkt[],
  walkRuns: GpsWalkRun[],
  liveWalkRunPoints?: KartenPunkt[],
): Grenzen | null {
  const alle = [...points, ...walkRuns.flatMap((r) => r.points), ...(liveWalkRunPoints ?? [])];
  if (alle.length === 0) return null;

  let west = Infinity;
  let sued = Infinity;
  let ost = -Infinity;
  let nord = -Infinity;
  for (const p of alle) {
    west = Math.min(west, p.longitude);
    ost = Math.max(ost, p.longitude);
    sued = Math.min(sued, p.latitude);
    nord = Math.max(nord, p.latitude);
  }
  return [
    [west, sued],
    [ost, nord],
  ];
}

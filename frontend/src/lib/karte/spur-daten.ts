import type { GpsWalkRun } from "@/lib/types";
import { automatischePunkte, markerPunkte } from "@/lib/geo";
import { istLuecke, lueckeDauerText, lueckeSekunden, stueckeAnLuecken } from "@/lib/ablauf-luecken";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { FAEHRTE_GRENZEN, type SpurGrenzen } from "@/lib/mantrailing";
import { ampelFarben, ampelStufe } from "@/lib/spur-ampel";
import { winkelBeschriftung, winkelZustand, zustandBeschriftung } from "@/lib/winkel-schenkel";

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
// "automatischer Punkt", siehe automatischePunkte in lib/geo.ts).
export type KartenPunkt = {
  latitude: number;
  longitude: number;
  pointType?: number;
  label?: string | null;
  markerType?: number;
  /** Nur Ablaufpunkte: für die Lückenregel der laufenden Aufzeichnung. */
  timestamp?: string;
};

/** Die Übersetzungsfunktion aus useT() - hier nur als Form, ohne React. */
export type TextUebersetzer = (text: string, werte?: Record<string, string | number>) => string;

export type Position = [number, number];
export type Eigenschaften = Record<string, string | number>;
export type Merkmal<G> = { type: "Feature"; properties: Eigenschaften; geometry: G };
export type Linie = { type: "LineString"; coordinates: Position[] };
export type Punkt = { type: "Point"; coordinates: Position };
export type Flaeche = { type: "Polygon"; coordinates: Position[][] };
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

// Ampelfarben für die Abweichung der Ablauf-Linie: die Farbtabelle und die
// Einstufung der Auswertung (lib/spur-ampel.ts), damit Karte, Auswertung und
// Fährtenbild nie verschiedene Farben für denselben Ablauf zeigen.
// Eine Funktion statt Konstante: die Palette hängt am Modus "Farbsichere Darstellung".
export function deviationColors(): { green: string; amber: string; red: string } {
  const farben = ampelFarben();
  return { green: farben.gruen, amber: farben.gelb, red: farben.rot };
}

export function deviationColor(meters: number | null | undefined, grenzen: SpurGrenzen = FAEHRTE_GRENZEN): string | null {
  return meters == null ? null : ampelFarben()[ampelStufe(meters, grenzen)];
}

// Beschriftung manueller Marker nach fachlicher Bedeutung (GpsMarkerType).
export const MARKER_LABELS = [
  uebersetzbar("Gegenstand"),
  uebersetzbar("Leckerlipot"),
  uebersetzbar("Verleitung"),
  uebersetzbar("Marker"),
  uebersetzbar("Wundbett"),
  uebersetzbar("Verweiserpunkt"),
] as const;

/**
 * Form UND Farbe je Markerart - die EINE Tabelle für Karte (spur-ebenen.ts) und
 * Legende (track-map.tsx). Die Form trägt die Unterscheidung, die Farbe hilft nur:
 * Bei Rot-Grün-Schwäche werden Gelb, Orange und Grün zu Gelbtönen, Magenta der
 * Legung zu Blau-Lavendel. Index = markerType (GpsMarkerType), Rückfall = Marker (Index 3).
 *
 * `umriss` in Einheitskoordinaten (0 bis 1, Mitte 0,5), Spitze des Dreiecks oben;
 * "kreis" als Kreis um die Mitte (Radius 0,3). Größen so gewählt, dass die Formen
 * optisch gleich groß wirken (das Dreieck braucht mehr Kantenlänge als das Quadrat).
 */
export type MarkerForm = "quadrat" | "kreis" | "dreieck" | "raute" | "sechseck" | "stern" | "kreuz";
export type MarkerUmriss = "kreis" | readonly (readonly [number, number])[];
export type MarkerArt = { form: MarkerForm; farbe: string; umriss: MarkerUmriss };

export const MARKER_FORMEN: readonly MarkerArt[] = [
  { form: "quadrat", farbe: "#facc15", umriss: [[0.2, 0.2], [0.8, 0.2], [0.8, 0.8], [0.2, 0.8]] },
  { form: "kreis", farbe: "#ffffff", umriss: "kreis" },
  { form: "dreieck", farbe: "#f97316", umriss: [[0.5, 0.16], [0.86, 0.84], [0.14, 0.84]] },
  { form: "raute", farbe: "#94a3b8", umriss: [[0.5, 0.15], [0.85, 0.5], [0.5, 0.85], [0.15, 0.5]] },
  // Schweißfährte: Wundbett (Sechseck) und Verweiserpunkt (Stern) - Formen, die keine der anderen Arten hat.
  { form: "sechseck", farbe: "#f43f5e", umriss: [[0.3, 0.14], [0.7, 0.14], [0.9, 0.5], [0.7, 0.86], [0.3, 0.86], [0.1, 0.5]] },
  { form: "stern", farbe: "#38bdf8", umriss: [[0.5, 0.08], [0.62, 0.38], [0.92, 0.5], [0.62, 0.62], [0.5, 0.92], [0.38, 0.62], [0.08, 0.5], [0.38, 0.38]] },
];

/**
 * Das Zeichen eines Rückrufs (Schweißfährte, Paket SF2): ein Kreuz, eine Form, die kein Marker hat. Nicht in
 * MARKER_FORMEN, denn dort ist der Index der markerType.
 */
export const RUECKRUF_FORM: MarkerArt = {
  form: "kreuz",
  farbe: "#ef4444",
  umriss: [[0.38, 0.1], [0.62, 0.1], [0.62, 0.38], [0.9, 0.38], [0.9, 0.62], [0.62, 0.62], [0.62, 0.9], [0.38, 0.9], [0.38, 0.62], [0.1, 0.62], [0.1, 0.38], [0.38, 0.38]],
};

/** Dunkler Rand der Marker (und Rand der Legendenzeichen). */
export const MARKER_RAND_FARBE = "#1c1917";

/** Die Art zu einem markerType; Unbekanntes ist ein Marker. */
export function markerArt(markerType: number | undefined): MarkerArt {
  return MARKER_FORMEN[markerType ?? 0] ?? MARKER_FORMEN[3];
}

// Stockungen: unerklärt = Warnsignal (rot), Verweisen am Gegenstand = gut
// (grün), erklärt/neutral (grau) - rot und grün aus der Ampel, also im Modus
// "Farbsichere Darstellung" Rot und Blau.
export function stopColors(): readonly [string, string, string, string] {
  const farben = ampelFarben();
  return [farben.rot, farben.gruen, "#94a3b8", "#94a3b8"];
}
export const STOP_LABELS = [
  uebersetzbar("Unerklärte Stockung"),
  uebersetzbar("Verweisen"),
  uebersetzbar("Halt (erklärt)"),
  uebersetzbar("Halt am Winkel"),
] as const;

/** Lückenstücke (keine Aufzeichnung) im Ablauf: grau gestrichelt, nicht in der Ampel- oder Versuchsfarbe. */
export const LUECKE_FARBE = "#94a3b8";

// Winkel, die der Ablauf nicht erreicht hat (oder ohne Ablauf-Zustand): nicht Teil der Ampel.
export const WINKEL_NEUTRAL_FARBE = "#64748b";

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
  const automatisch = automatischePunkte(points);
  const manuell = markerPunkte(points);

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
          form: markerArt(art).form,
          // Das Label stammt von Nutzer:innen - es geht als Text weiter und
          // wird als Text angezeigt (setText), nie als HTML.
          text: p.label || t(MARKER_LABELS[art] ?? MARKER_LABELS[3]),
        });
      }),
    },
  };
}

/**
 * Die Winkel der gelegten Fährte als nummerierte Punkte auf der Linie. Die Lage steht je Ablauf
 * am Server (alle Abläufe teilen dieselbe Legung); die Farbe richtet sich nach dem Zustand im
 * zuletzt gezeigten Ablauf - sauber grün, überschossen nach der Ampel, nicht erreicht neutral.
 * Ohne ausgewerteten Ablauf gibt es keine Winkel zu zeigen.
 */
export function winkelDaten(walkRuns: GpsWalkRun[], t: TextUebersetzer, grenzen: SpurGrenzen = FAEHRTE_GRENZEN, haken = false): Merkmale<Punkt> {
  const lauf = [...walkRuns].reverse().find((r) => (r.corners ?? []).length > 0);
  const features = (lauf?.corners ?? []).map((corner) => {
    const zustand = winkelZustand(corner);
    const farbe =
      zustand === "sauber"
        ? ampelFarben().gruen
        : zustand === "ueberschossen"
          ? (deviationColor(corner.overshootMeters ?? Infinity, grenzen) ?? ampelFarben().rot)
          : WINKEL_NEUTRAL_FARBE;
    return punkt(corner, {
      farbe,
      nummer: String(corner.number),
      text: `${winkelBeschriftung(corner, t, haken)} · ${zustandBeschriftung(corner, t)}`,
    });
  });
  return { type: "FeatureCollection", features };
}

export type AblaufDaten = {
  /** Je Segment ein Merkmal, Farbe = die des schlechteren Endpunkts. */
  bewertet: Merkmale<Linie>;
  /** Ganze Linie je Ablauf, in der Zyklusfarbe (wird gestrichelt gezeichnet). */
  unbewertet: Merkmale<Linie>;
  /** Stücke ohne Aufzeichnung (vom Server als afterGap getragen). */
  luecken: Merkmale<Linie>;
  halte: Merkmale<Punkt>;
};

function lueckenText(dauerSekunden: number, t: TextUebersetzer): string {
  return t("Keine Aufzeichnung ({dauer})", { dauer: lueckeDauerText(dauerSekunden, (n) => t("{n} Min.", { n })) });
}

/** Die abgelaufenen Versuche mit ihren Haltepunkten; die Farben folgen den Grenzen der Art (Fährte oder Mantrailing). */
export function ablaufDaten(walkRuns: GpsWalkRun[], t: TextUebersetzer, grenzen: SpurGrenzen = FAEHRTE_GRENZEN): AblaufDaten {
  const stopFarben = stopColors();
  const bewertet: Merkmal<Linie>[] = [];
  const unbewertet: Merkmal<Linie>[] = [];
  const luecken: Merkmal<Linie>[] = [];
  const halte: Merkmal<Punkt>[] = [];

  walkRuns.forEach((run, index) => {
    if (run.points.length === 0) return;
    const ausweichfarbe = WALK_RUN_COLORS[index % WALK_RUN_COLORS.length];
    const koordinaten = run.points.map(position);
    // Nur das Feld des Servers: auf den vereinfachten Punkten ließe sich keine Lücke zuverlässig erkennen.
    const lueckeVor = (i: number) => run.points[i].afterGap === true;
    const lueckenMerkmal = (i: number) =>
      linie([koordinaten[i - 1], koordinaten[i]], {
        farbe: LUECKE_FARBE,
        text: lueckenText(lueckeSekunden(run.points[i - 1], run.points[i]), t),
      });

    if (run.points.some((p) => p.deviationMeters != null)) {
      // Je Segment die Farbe des schlechteren der beiden Endpunkte - so ist
      // auf einen Blick sichtbar, WO das Team abgekommen ist.
      for (let i = 0; i < koordinaten.length - 1; i++) {
        if (lueckeVor(i + 1)) {
          luecken.push(lueckenMerkmal(i + 1));
          continue;
        }
        const schlechter = Math.max(run.points[i].deviationMeters ?? 0, run.points[i + 1].deviationMeters ?? 0);
        bewertet.push(
          linie([koordinaten[i], koordinaten[i + 1]], {
            farbe: deviationColor(schlechter, grenzen) ?? ausweichfarbe,
            text: t("Ablauf {nr}: {meter} m Abweichung", { nr: index + 1, meter: Math.round(schlechter) }),
          }),
        );
      }
    } else {
      // Index des ersten Punkts des Stücks in run.points (ein Lückenstück beginnt beim letzten Punkt davor).
      let naechster = 0;
      for (const stueck of stueckeAnLuecken(run.points, lueckeVor)) {
        if (stueck.luecke) {
          luecken.push(lueckenMerkmal(naechster));
          continue;
        }
        unbewertet.push(
          ...linienMerkmale(stueck.punkte.map(position), { farbe: ausweichfarbe, text: t("Ablauf-Versuch {nr}", { nr: index + 1 }) }),
        );
        naechster += stueck.punkte.length;
      }
    }

    // Erkannte Halte als Ringe: zeigen die Stellen, an denen der Hund
    // gesucht/verwiesen hat - auch dort, wo die Position gar nicht abweicht.
    (run.stops ?? []).forEach((stop) => {
      halte.push(
        punkt(stop, {
          farbe: stopFarben[stop.kind] ?? stopFarben[2],
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
    luecken: { type: "FeatureCollection", features: luecken },
    halte: { type: "FeatureCollection", features: halte },
  };
}

export type LiveAblaufDaten = {
  linie: Merkmale<Linie>;
  /** Lückenstücke nach der Regel auf den rohen Punkten (lib/ablauf-luecken.ts). */
  luecken: Merkmale<Linie>;
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
  const roh = liveWalkRunPoints ?? [];
  const koordinaten = roh.map(position);
  const mitZeit = (p: KartenPunkt) => ({ latitude: p.latitude, longitude: p.longitude, timestamp: p.timestamp ?? "" });
  const stuecke = stueckeAnLuecken(roh, (i) => roh[i - 1].timestamp != null && roh[i].timestamp != null && istLuecke(mitZeit(roh[i - 1]), mitZeit(roh[i])));
  const live = stuecke
    .filter((s) => !s.luecke)
    .flatMap((s) => linienMerkmale(s.punkte.map(position), { farbe, text: t("Ablauf-Versuch {nr} (läuft)", { nr: anzahlAblaeufe + 1 }) }));
  const luecken = stuecke
    .filter((s) => s.luecke)
    .map((s) => linie(s.punkte.map(position), { farbe: LUECKE_FARBE, text: lueckenText(lueckeSekunden(mitZeit(s.punkte[0]), mitZeit(s.punkte[1])), t) }));
  const letzter = koordinaten.length > 0 ? koordinaten[koordinaten.length - 1] : null;

  return {
    linie: { type: "FeatureCollection", features: live },
    luecken: { type: "FeatureCollection", features: luecken },
    position: {
      type: "FeatureCollection",
      features: letzter ? [{ type: "Feature", properties: { farbe }, geometry: { type: "Point", coordinates: letzter } }] : [],
    },
    farbe,
    letzter,
  };
}

/** Die Stellen, an denen ein Rückruf fiel (vom Server berechnet, nur Schweißfährte). */
export function rueckrufDaten(walkRuns: GpsWalkRun[], t: TextUebersetzer): Merkmale<Punkt> {
  const features = walkRuns.flatMap((run, i) =>
    (run.recallPoints ?? []).map((p, k) => punkt(p, { form: RUECKRUF_FORM.form, text: t("Rückruf {n} (Ablauf {nr})", { n: k + 1, nr: i + 1 }) })),
  );
  return { type: "FeatureCollection", features };
}

/** Das Quadrat um den Anschuss beim verdeckten Arbeiten; leer ohne Fläche. */
export function flaecheDaten(ring: Position[] | null): Merkmale<Flaeche> {
  return {
    type: "FeatureCollection",
    features: ring ? [{ type: "Feature", properties: { text: "30 × 30 m" }, geometry: { type: "Polygon", coordinates: [ring] } }] : [],
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

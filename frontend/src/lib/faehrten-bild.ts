import { estimateLengthMeters } from "@/lib/geo";
import type { GpsPoint, GpsTrack, GpsWalkRun } from "@/lib/types";
import { formatDelta, formatTemperature } from "@/lib/weather";
import { AMBER_MAX_M, ampelLabel, GREEN_MAX_M } from "@/components/tracking/walk-run-evaluation";

/**
 * Reine Geometrie und Kennzahlen für das Fährtenbild ("Als Bild teilen").
 *
 * Das Bild zeigt die FORM der Fährte, nicht ihren Ort: Alles wird in lokale
 * Meter um den Mittelpunkt umgerechnet, bevor gezeichnet wird. Damit kommen
 * Koordinaten gar nicht erst in die Nähe des Bildes - weder sichtbar noch als
 * Metadaten. Gezeichnet wird in lib/faehrten-bild-zeichnen.ts.
 */

const ERDRADIUS_M = 6371000;

/**
 * Kleinste Ausdehnung, auf die das Bild eingepasst wird. Eine gerade Fährte
 * (oder ein einzelner Punkt) hat in einer Richtung die Ausdehnung 0 - ohne
 * Untergrenze wäre der Maßstab unendlich.
 */
const MIN_AUSDEHNUNG_M = 10;

export type Koordinate = { latitude: number; longitude: number };

/** Lokale Meter um den Mittelpunkt: x nach Osten, y nach Norden. */
export type MeterPunkt = { x: number; y: number };

/** Mitte des umschließenden Rechtecks - null ohne Punkte. */
export function mittelpunkt(punkte: Koordinate[]): Koordinate | null {
  if (punkte.length === 0) return null;
  let minLat = Infinity;
  let maxLat = -Infinity;
  let minLon = Infinity;
  let maxLon = -Infinity;
  for (const p of punkte) {
    minLat = Math.min(minLat, p.latitude);
    maxLat = Math.max(maxLat, p.latitude);
    minLon = Math.min(minLon, p.longitude);
    maxLon = Math.max(maxLon, p.longitude);
  }
  return { latitude: (minLat + maxLat) / 2, longitude: (minLon + maxLon) / 2 };
}

/**
 * Breite/Länge in lokale Meter um `mitte` (gleichabständig, "equirectangular").
 * Auf Fährtenmaßstab (Hunderte Meter) ist der Fehler gegenüber einer
 * genaueren Projektion weit unter der GPS-Genauigkeit von 3 bis 8 m.
 */
export function inMeter(punkte: Koordinate[], mitte: Koordinate): MeterPunkt[] {
  const rad = (grad: number) => (grad * Math.PI) / 180;
  const laengenfaktor = Math.cos(rad(mitte.latitude));
  return punkte.map((p) => ({
    x: rad(p.longitude - mitte.longitude) * ERDRADIUS_M * laengenfaktor,
    y: rad(p.latitude - mitte.latitude) * ERDRADIUS_M,
  }));
}

export type Flaeche = { x: number; y: number; breite: number; hoehe: number };

export type Einpassung = {
  /** Pixel je Meter - in beiden Richtungen gleich, das Seitenverhältnis bleibt. */
  masstab: number;
  /** Meter-Punkt in Pixel der Zeichenfläche; Norden oben. */
  abbilden: (p: MeterPunkt) => { x: number; y: number };
};

/**
 * Passt die Punkte mittig in die Fläche ein, abzüglich `rand` ringsum.
 * Norden bleibt oben (y wird gespiegelt, weil die Zeichenfläche nach unten zählt).
 */
export function passeEin(punkte: MeterPunkt[], flaeche: Flaeche, rand: number): Einpassung {
  let minX = Infinity;
  let maxX = -Infinity;
  let minY = Infinity;
  let maxY = -Infinity;
  for (const p of punkte) {
    minX = Math.min(minX, p.x);
    maxX = Math.max(maxX, p.x);
    minY = Math.min(minY, p.y);
    maxY = Math.max(maxY, p.y);
  }
  if (punkte.length === 0) {
    minX = maxX = minY = maxY = 0;
  }

  const breiteM = Math.max(maxX - minX, MIN_AUSDEHNUNG_M);
  const hoeheM = Math.max(maxY - minY, MIN_AUSDEHNUNG_M);
  const innenBreite = Math.max(flaeche.breite - 2 * rand, 1);
  const innenHoehe = Math.max(flaeche.hoehe - 2 * rand, 1);
  const masstab = Math.min(innenBreite / breiteM, innenHoehe / hoeheM);

  const mitteX = (minX + maxX) / 2;
  const mitteY = (minY + maxY) / 2;
  const zentrumX = flaeche.x + flaeche.breite / 2;
  const zentrumY = flaeche.y + flaeche.hoehe / 2;

  return {
    masstab,
    abbilden: (p) => ({ x: zentrumX + (p.x - mitteX) * masstab, y: zentrumY - (p.y - mitteY) * masstab }),
  };
}

/** Runde Längen in Metern: 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000 ... */
function rundeLaengen(): number[] {
  const laengen: number[] = [];
  for (let zehner = 1; zehner <= 100000; zehner *= 10) {
    laengen.push(zehner, zehner * 2, zehner * 5);
  }
  return laengen;
}

const RUNDE_LAENGEN = rundeLaengen();

/**
 * Der Maßstabsbalken: die größte runde Länge, die noch in `maxBreitePx` passt.
 * null, wenn der Maßstab unbrauchbar ist.
 */
export function massstabsbalken(masstab: number, maxBreitePx: number): { meter: number; breitePx: number } | null {
  if (!Number.isFinite(masstab) || masstab <= 0 || maxBreitePx <= 0) return null;
  let gewaehlt: number | null = null;
  for (const meter of RUNDE_LAENGEN) {
    if (meter * masstab <= maxBreitePx) gewaehlt = meter;
  }
  return gewaehlt === null ? null : { meter: gewaehlt, breitePx: gewaehlt * masstab };
}

/** "50 m" bzw. "1 km" - Einheiten sind in beiden Sprachen gleich. */
export function formatiereMassstab(meter: number): string {
  return meter >= 1000 ? `${meter / 1000} km` : `${meter} m`;
}

export type Ampel = "gruen" | "gelb" | "rot";

/** Dieselben Schwellen wie die Auswertung im Tagebuch und die Kartenlinie. */
export function ampelStufe(abweichungMeter: number): Ampel {
  if (abweichungMeter <= GREEN_MAX_M) return "gruen";
  if (abweichungMeter <= AMBER_MAX_M) return "gelb";
  return "rot";
}

/** Dieselben Farben wie die Ablauflinie auf der Karte (track-map.tsx). */
export const AMPEL_FARBEN: Record<Ampel, string> = { gruen: "#16a34a", gelb: "#d97706", rot: "#dc2626" };

export type Linienstueck = {
  /** null: für dieses Stück gibt es keine Abweichung. */
  stufe: Ampel | null;
  /** Indizes der Punkte, beide einschließlich. */
  von: number;
  bis: number;
};

/**
 * Teilt den Ablauf in Stücke gleicher Farbe.
 *
 * Wie auf der Karte zählt je Teilstück die schlechtere der beiden
 * Abweichungen an seinen Enden - ein Ausreißer soll nicht weggemittelt
 * werden. Aufeinanderfolgende Stücke gleicher Farbe werden zu einer Linie
 * zusammengefasst; das gibt saubere Ecken statt einer Kette einzelner Striche.
 */
export function faerbeSegmente(abweichungen: (number | null | undefined)[]): Linienstueck[] {
  const stuecke: Linienstueck[] = [];
  for (let i = 0; i < abweichungen.length - 1; i++) {
    const a = abweichungen[i];
    const b = abweichungen[i + 1];
    const stufe = a == null && b == null ? null : ampelStufe(Math.max(a ?? 0, b ?? 0));
    const letztes = stuecke[stuecke.length - 1];
    if (letztes && letztes.stufe === stufe) letztes.bis = i + 1;
    else stuecke.push({ stufe, von: i, bis: i + 1 });
  }
  return stuecke;
}

/** Der neueste ausgewertete Ablauf - lose Prüfung, ältere Stände kennen die Felder nicht. */
export function neuesterAusgewerteterAblauf(ablaeufe: GpsWalkRun[]): GpsWalkRun | null {
  let neuester: GpsWalkRun | null = null;
  for (const ablauf of ablaeufe) {
    if (ablauf.evaluatedAt == null || ablauf.avgDeviationMeters == null) continue;
    if (neuester === null || ablauf.createdAt > neuester.createdAt) neuester = ablauf;
  }
  return neuester;
}

function automatischePunkte(track: GpsTrack): GpsPoint[] {
  return track.points.filter((p) => p.pointType !== 1);
}

/**
 * Liegezeit in Minuten: eine am Track hinterlegte, sonst die Zeit vom Ende
 * des Legens bis zum Start des Ablaufs. Neu aufgezeichnete Fährten tragen
 * keine eigene Liegezeit, die Punkte liefern sie aber mit.
 */
export function liegezeitMinuten(track: GpsTrack, ablauf: GpsWalkRun | null): number | null {
  if (track.ageMinutes != null) return track.ageMinutes;
  const gelegt = automatischePunkte(track);
  const letzter = gelegt[gelegt.length - 1];
  const erster = ablauf?.points[0];
  if (!letzter || !erster) return null;
  const minuten = Math.round((new Date(erster.timestamp).getTime() - new Date(letzter.timestamp).getTime()) / 60000);
  return Number.isFinite(minuten) && minuten >= 0 ? minuten : null;
}

export type FaehrtenKennzahlen = {
  /** Ganze Prozent - null ohne ausgewerteten Ablauf. */
  aufFaehrtePercent: number | null;
  /** Ganze Meter. */
  mittlereAbweichungM: number | null;
  ampel: Ampel | null;
  gefunden: number;
  gesamt: number;
  liegezeitMin: number | null;
  laengeM: number | null;
  /** Nur, was am Track schon gespeichert ist - das Bild lädt nichts nach. */
  legenC: number | null;
  suchenC: number | null;
  deltaC: number | null;
};

export function faehrtenKennzahlen(track: GpsTrack, ablauf: GpsWalkRun | null): FaehrtenKennzahlen {
  const avg = ablauf?.avgDeviationMeters ?? null;
  const laenge = track.lengthMeters != null ? track.lengthMeters : estimateLengthMeters(automatischePunkte(track));
  return {
    aufFaehrtePercent: ablauf?.onTrackPercent != null ? Math.round(ablauf.onTrackPercent) : null,
    mittlereAbweichungM: avg != null ? Math.round(avg) : null,
    ampel: avg != null ? ampelStufe(avg) : null,
    gefunden: ablauf?.articlesFound ?? 0,
    gesamt: ablauf?.articlesTotal ?? 0,
    liegezeitMin: liegezeitMinuten(track, ablauf ?? track.walkRuns[track.walkRuns.length - 1] ?? null),
    laengeM: laenge > 0 ? Math.round(laenge) : null,
    legenC: track.laidTemperatureC ?? null,
    suchenC: track.searchTemperatureC ?? null,
    deltaC: track.temperatureDeltaC ?? null,
  };
}

export type FaehrtenGeometrie = {
  /** Die gelegte Fährte (nur automatische Punkte), in Legereihenfolge. */
  gelegt: MeterPunkt[];
  marker: { p: MeterPunkt; gegenstand: boolean }[];
  /** Der ausgewertete Ablauf samt Abweichung je Punkt - null ohne Ablauf. */
  ablauf: { punkte: MeterPunkt[]; abweichungen: (number | null)[] } | null;
};

/**
 * Alles, was gezeichnet wird, in lokalen Metern. null, wenn es nichts zu
 * zeichnen gibt (weniger als zwei Punkte sind keine Linie).
 */
export function faehrtenGeometrie(track: GpsTrack, ablauf: GpsWalkRun | null): FaehrtenGeometrie | null {
  const gelegt = automatischePunkte(track);
  if (gelegt.length < 2) return null;

  const marker = track.points.filter((p) => p.pointType === 1);
  const ablaufPunkte = ablauf?.points ?? [];
  const mitte = mittelpunkt([...gelegt, ...marker, ...ablaufPunkte]);
  if (!mitte) return null;

  return {
    gelegt: inMeter(gelegt, mitte),
    marker: inMeter(marker, mitte).map((p, i) => ({ p, gegenstand: marker[i].markerType === 0 })),
    ablauf:
      ablauf && ablaufPunkte.length >= 2
        ? { punkte: inMeter(ablaufPunkte, mitte), abweichungen: ablaufPunkte.map((p) => p.deviationMeters ?? null) }
        : null,
  };
}

/** Tag des Legens (lokale Zeit) - ohne Uhrzeit, das Bild nennt keine. */
export function legedatum(track: GpsTrack): Date | null {
  const erster = automatischePunkte(track)[0] ?? track.points[0];
  if (!erster) return null;
  const datum = new Date(erster.timestamp);
  return Number.isNaN(datum.getTime()) ? null : datum;
}

/** Was das Bild als Text trägt - fertig übersetzt, gezeichnet wird in faehrten-bild-zeichnen.ts. */
export type BildTexte = {
  /** "Rex · Fährte" bzw. "Fährte". */
  titel: string;
  datum: string | null;
  /** Die große Zeile, z. B. "87 % auf der Fährte". */
  kopfzahl: string;
  /** Ampelwort unter der Kopfzahl - nur mit ausgewertetem Ablauf. */
  ampelWort: string | null;
  zeilen: string[];
  legende: { start: string; gegenstand: string; gelegt: string; abgelaufen: string };
  marke: string;
};

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/**
 * Stellt die Texte des Bildes zusammen. Ohne ausgewerteten Ablauf bleibt die
 * gelegte Fährte mit Länge und Liegezeit; die Länge steht dann als große
 * Zeile, damit sie nicht zweimal auftaucht.
 *
 * Zahlen sind ganze Zahlen: Der GPS-Fehler liegt bei 3 bis 8 m, Nachkommastellen
 * täuschten eine Genauigkeit vor, die es nicht gibt.
 */
export function baueBildTexte(
  t: Uebersetzer,
  eingabe: {
    track: GpsTrack;
    ablauf: GpsWalkRun | null;
    /** null: ohne Hundenamen. */
    hundeName: string | null;
    /** Für das Datum: "de" oder "en". */
    sprache: string;
  },
): BildTexte {
  const { track, ablauf, hundeName, sprache } = eingabe;
  const kz = faehrtenKennzahlen(track, ablauf);
  const tag = legedatum(track);

  let kopfzahl: string;
  let laengeInZeilen = true;
  if (kz.aufFaehrtePercent != null) {
    kopfzahl = t("{prozent} % auf der Fährte", { prozent: kz.aufFaehrtePercent });
  } else if (kz.mittlereAbweichungM != null) {
    kopfzahl = t("Ø {meter} m Abweichung", { meter: kz.mittlereAbweichungM });
  } else if (kz.laengeM != null) {
    kopfzahl = t("Länge {meter} m", { meter: kz.laengeM });
    laengeInZeilen = false;
  } else {
    kopfzahl = t("Gelegte Fährte");
  }

  const zeilen: string[] = [];
  if (kz.gesamt > 0) zeilen.push(t("{gefunden}/{gesamt} Gegenstände", { gefunden: kz.gefunden, gesamt: kz.gesamt }));
  if (kz.liegezeitMin != null) zeilen.push(t("Liegezeit {minuten} min", { minuten: kz.liegezeitMin }));
  if (laengeInZeilen && kz.laengeM != null) zeilen.push(t("Länge {meter} m", { meter: kz.laengeM }));

  const temperaturen: string[] = [];
  const legen = formatTemperature(kz.legenC, sprache === "en" ? "en" : "de");
  const suchen = formatTemperature(kz.suchenC, sprache === "en" ? "en" : "de");
  if (legen) temperaturen.push(t("Legen {temperatur}", { temperatur: legen }));
  if (suchen) temperaturen.push(t("Suchen {temperatur}", { temperatur: suchen }));
  if (temperaturen.length > 0) {
    const delta = legen && suchen ? formatDelta(kz.deltaC, sprache === "en" ? "en" : "de") : null;
    zeilen.push(temperaturen.join(" · ") + (delta ? ` (${delta})` : ""));
  }

  return {
    titel: hundeName ? t("{hund} · Fährte", { hund: hundeName }) : t("Fährte"),
    datum: tag
      ? tag.toLocaleDateString(sprache === "de" ? "de-DE" : "en-GB", { day: "numeric", month: "long", year: "numeric" })
      : null,
    kopfzahl,
    ampelWort: ablauf?.avgDeviationMeters != null ? t(ampelLabel(ablauf.avgDeviationMeters)) : null,
    zeilen,
    legende: { start: t("Start"), gegenstand: t("Gegenstand"), gelegt: t("Gelegt"), abgelaufen: t("Abgelaufen") },
    marke: "Dogity",
  };
}

/** "2026-09-30" aus dem Tag des Legens, für den Dateinamen. */
export function legedatumFuerDatei(track: GpsTrack): string {
  const tag = legedatum(track);
  if (!tag) return "bild";
  const zweistellig = (n: number) => String(n).padStart(2, "0");
  return `${tag.getFullYear()}-${zweistellig(tag.getMonth() + 1)}-${zweistellig(tag.getDate())}`;
}

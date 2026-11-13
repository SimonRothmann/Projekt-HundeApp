import type { GpsWalkRun, GpsWalkRunCorner, GpsWalkRunLeg } from "@/lib/types";

/**
 * Winkel und Schenkel eines Fährten-Ablaufs für die Anzeige (Paket F2): die Zeilen in
 * Laufreihenfolge und die Zeit als Text. Was ein Winkel ist und wann er "überschossen" gilt,
 * entscheidet der Server (GpsTrackEvaluator) - hier wird nur geordnet und benannt.
 *
 * Lose Prüfung (?? []): aus einem älteren Read-Cache oder von einem älteren Backend-Stand fehlen
 * die Felder ganz.
 */

/** Die Übersetzungsfunktion aus useT() - hier nur als Form, ohne React. */
type TextUebersetzer = (text: string, werte?: Record<string, string | number>) => string;

export type WinkelZustand = "sauber" | "ueberschossen" | "nicht-erreicht";

export type SchenkelZeile = { art: "schenkel"; leg: GpsWalkRunLeg };
export type WinkelZeile = { art: "winkel"; corner: GpsWalkRunCorner; zustand: WinkelZustand };
export type AbschnittZeile = SchenkelZeile | WinkelZeile;

export function winkelZustand(corner: GpsWalkRunCorner): WinkelZustand {
  if (!corner.reached) return "nicht-erreicht";
  return corner.overshot ? "ueberschossen" : "sauber";
}

/** Ob der Bereich "Winkel und Schenkel" etwas zu zeigen hat: einen Winkel oder mehr als einen Schenkel. */
export function hatWinkelUndSchenkel(run: Pick<GpsWalkRun, "legs" | "corners">): boolean {
  return (run.corners ?? []).length > 0 || (run.legs ?? []).length > 1;
}

/** Schenkel 1, Winkel 1, Schenkel 2, Winkel 2 ... in Laufrichtung. */
export function abschnittsZeilen(run: Pick<GpsWalkRun, "legs" | "corners">): AbschnittZeile[] {
  const legs = [...(run.legs ?? [])].sort((a, b) => a.number - b.number);
  const corners = [...(run.corners ?? [])].sort((a, b) => a.number - b.number);
  const zeilen: AbschnittZeile[] = [];
  const anzahl = Math.max(legs.length, corners.length);
  for (let i = 0; i < anzahl; i++) {
    if (legs[i]) zeilen.push({ art: "schenkel", leg: legs[i] });
    if (corners[i]) zeilen.push({ art: "winkel", corner: corners[i], zustand: winkelZustand(corners[i]) });
  }
  return zeilen;
}

/** Sekunden als "2:10" (Minuten:Sekunden); die Einheit setzt die Anzeige dazu. */
export function dauerText(sekunden: number): string {
  const ganz = Math.max(0, Math.round(sekunden));
  return `${Math.floor(ganz / 60)}:${String(ganz % 60).padStart(2, "0")}`;
}

/** "Winkel 1 · rechts · spitz" - die Beschriftung eines Winkels in Liste und Karte. */
export function winkelBeschriftung(corner: GpsWalkRunCorner, t: TextUebersetzer): string {
  const richtung = corner.direction === 0 ? t("links") : t("rechts");
  return [t("Winkel {nr}", { nr: corner.number }), richtung, ...(corner.sharp ? [t("spitz")] : [])].join(" · ");
}

/** Wie der Ablauf mit dem Winkel umging: sauber, überschossen (mit Meterzahl) oder nicht erreicht. */
export function zustandBeschriftung(corner: GpsWalkRunCorner, t: TextUebersetzer): string {
  const zustand = winkelZustand(corner);
  if (zustand === "sauber") return t("sauber");
  if (zustand === "nicht-erreicht") return t("nicht erreicht");
  return corner.overshootMeters != null
    ? t("überschossen {meter} m", { meter: Math.round(corner.overshootMeters) })
    : t("überschossen");
}

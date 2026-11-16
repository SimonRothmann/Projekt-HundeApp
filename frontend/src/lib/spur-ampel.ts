import { farbsicherAktiv } from "@/lib/farbsicher";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { FAEHRTE_GRENZEN, istMantrailing, spurGrenzen, type SpurGrenzen } from "@/lib/mantrailing";

/**
 * Die Ampel der Abweichung eines Ablaufs - die EINE Einstufung für Auswertung,
 * Kartenlinie, Fährtenbild und Verlauf. Die Grenzen je Art (Fährte 3/6 m,
 * Mantrailing 10/25 m) stehen in lib/mantrailing.ts (`spurGrenzen`); alles
 * andere bildet nur die Stufe auf Klasse, Wort oder Farbe ab.
 */

export type Ampel = "gruen" | "gelb" | "rot";

/** Die Stufe einer Abweichung in Metern - ohne Grenzen die der Fährte. Die Grenze selbst gehört zur besseren Stufe. */
export function ampelStufe(abweichungMeter: number, grenzen: SpurGrenzen = FAEHRTE_GRENZEN): Ampel {
  if (abweichungMeter <= grenzen.gruenBis) return "gruen";
  if (abweichungMeter <= grenzen.gelbBis) return "gelb";
  return "rot";
}

/** Die Farben der Ampel für Canvas und Karte (Hex; Tailwind-Klassen stehen bei den Komponenten). */
export const AMPEL_FARBEN_NORMAL: Record<Ampel, string> = { gruen: "#16a34a", gelb: "#d97706", rot: "#dc2626" };

/** Farbsichere Palette (Paul Tol, für Rot-Grün-Schwäche ausgelegt): Blau, Gelb, Rot - siehe lib/farbsicher.ts. */
export const AMPEL_FARBEN_SICHER: Record<Ampel, string> = { gruen: "#0077BB", gelb: "#DDAA33", rot: "#CC3311" };

/**
 * Die Farben der Ampel für Canvas und Karte - je nach Modus "Farbsichere Darstellung" (Attribut am
 * Wurzelelement, zur Laufzeit abgelesen). Ohne Dokument (Server, Tests) die normale Palette.
 */
export function ampelFarben(): Record<Ampel, string> {
  return farbsicherAktiv() ? AMPEL_FARBEN_SICHER : AMPEL_FARBEN_NORMAL;
}

/** Das Wort zur Ampel einer Abweichung (Schlüssel für t()): was die Stufe bei dieser Art Spur heißt. */
export function ampelLabel(abweichungMeter: number, art?: { kind?: number | null } | null): string {
  const stufe = ampelStufe(abweichungMeter, spurGrenzen(art));
  if (stufe === "gruen") return istMantrailing(art) ? uebersetzbar("nah an der Spur") : uebersetzbar("eng an der Fährte");
  return stufe === "gelb" ? uebersetzbar("mittlere Abweichung") : uebersetzbar("deutlich abgekommen");
}

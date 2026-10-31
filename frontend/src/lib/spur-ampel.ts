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
export const AMPEL_FARBEN: Record<Ampel, string> = { gruen: "#16a34a", gelb: "#d97706", rot: "#dc2626" };

/** Das Wort zur Ampel einer Abweichung (Schlüssel für t()): was die Stufe bei dieser Art Spur heißt. */
export function ampelLabel(abweichungMeter: number, art?: { kind?: number | null } | null): string {
  const stufe = ampelStufe(abweichungMeter, spurGrenzen(art));
  if (stufe === "gruen") return istMantrailing(art) ? uebersetzbar("nah an der Spur") : uebersetzbar("eng an der Fährte");
  return stufe === "gelb" ? uebersetzbar("mittlere Abweichung") : uebersetzbar("deutlich abgekommen");
}

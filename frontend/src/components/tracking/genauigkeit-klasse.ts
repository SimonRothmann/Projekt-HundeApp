import type { GenauigkeitsStufe } from "@/lib/gps-profil";

/**
 * Farbe der Genauigkeitsanzeige beim Aufzeichnen. Draußen im Sonnenlicht
 * gelesen: 700er-Töne im Hellen (>= 5:1 auf Weiß), halbfett, damit "±6 m" in
 * der Leiste nicht untergeht.
 */
export const GENAUIGKEIT_KLASSE: Record<GenauigkeitsStufe, string> = {
  gut: "font-semibold text-erfolg-text",
  mittel: "font-semibold text-warnung-text",
  schlecht: "font-semibold text-destructive",
};

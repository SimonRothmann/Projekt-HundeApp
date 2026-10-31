import type { GenauigkeitsStufe } from "@/lib/gps-profil";

/**
 * Farbe der Genauigkeitsanzeige beim Aufzeichnen. Draußen im Sonnenlicht
 * gelesen: 700er-Töne im Hellen (>= 5:1 auf Weiß), halbfett, damit "±6 m" in
 * der Leiste nicht untergeht.
 */
export const GENAUIGKEIT_KLASSE: Record<GenauigkeitsStufe, string> = {
  gut: "font-semibold text-green-700 dark:text-green-400",
  mittel: "font-semibold text-amber-700 dark:text-amber-400",
  schlecht: "font-semibold text-destructive",
};

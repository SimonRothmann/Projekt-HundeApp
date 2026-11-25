import type { Sprache } from "@/lib/i18n/sprachen";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { zahlText } from "@/lib/ortsformat";

// WMO-Wettercodes (siehe Open-Meteo weather_code) auf kurze deutsche Texte
// (der Aufrufer übersetzt sie mit t()) und ein Symbol abgebildet. Bewusst gruppiert statt jeden Code einzeln:
// für ein Trainingstagebuch reicht "Regen" statt "mäßiger gefrierender
// Sprühregen".
export function weatherLabel(code: number | null | undefined): string | null {
  if (code == null) return null;
  if (code === 0) return uebersetzbar("klar");
  if (code <= 3) return uebersetzbar("bewölkt");
  if (code <= 48) return uebersetzbar("Nebel");
  if (code <= 57) return uebersetzbar("Nieselregen");
  if (code <= 67) return uebersetzbar("Regen");
  if (code <= 77) return uebersetzbar("Schnee");
  if (code <= 82) return uebersetzbar("Schauer");
  if (code <= 86) return uebersetzbar("Schneeschauer");
  return uebersetzbar("Gewitter");
}

export function weatherIcon(code: number | null | undefined): string {
  if (code == null) return "🌡️";
  if (code === 0) return "☀️";
  if (code <= 3) return "⛅";
  if (code <= 48) return "🌫️";
  if (code <= 57) return "🌦️";
  if (code <= 67) return "🌧️";
  if (code <= 77) return "❄️";
  if (code <= 82) return "🌦️";
  if (code <= 86) return "🌨️";
  return "⛈️";
}

/** "12,4 °C" bzw. "12.4 °C" - eine Nachkommastelle, Trennzeichen nach Sprache. */
export function formatTemperature(celsius: number | null | undefined, sprache: Sprache = "de"): string | null {
  if (celsius == null) return null;
  return `${zahlText(celsius, sprache)} °C`;
}

/**
 * Temperaturänderung als vorzeichenbehafteter Text ("+3,2 K"). Bei der Fährte
 * die eigentlich interessante Größe: die Änderung zwischen Legen und Suchen
 * bestimmt maßgeblich, wie sich die Geruchsspur hält.
 */
export function formatDelta(delta: number | null | undefined, sprache: Sprache = "de"): string | null {
  if (delta == null) return null;
  const sign = delta > 0 ? "+" : "";
  return `${sign}${zahlText(delta, sprache)} K`;
}

/**
 * Wetter über die Stehzeit (Schweißfährte) bzw. Liegezeit (Trail) als eine Zeile,
 * z. B. "Während der Stehzeit: 4,2 mm Regen · 3 bis 9 °C". Null, solange nichts da ist.
 */
export function stehzeitWetterText(
  t: (text: string, werte?: Record<string, string | number>) => string,
  sprache: Sprache,
  trail: boolean,
  regenMm: number | null | undefined,
  minC: number | null | undefined,
  maxC: number | null | undefined,
): string | null {
  if (regenMm == null || minC == null || maxC == null) return null;
  // "+ 0" macht aus -0 (gerundet aus -0,3) eine 0.
  const von = zahlText(Math.round(minC) + 0, sprache, 0);
  const bis = zahlText(Math.round(maxC) + 0, sprache, 0);
  const regen = regenMm > 0 ? t("{n} mm Regen", { n: zahlText(regenMm, sprache) }) : t("kein Regen");
  const temperatur = von === bis ? `${von} °C` : t("{von} bis {bis} °C", { von, bis });
  const wetter = `${regen} · ${temperatur}`;
  return trail ? t("Während der Liegezeit: {wetter}", { wetter }) : t("Während der Stehzeit: {wetter}", { wetter });
}

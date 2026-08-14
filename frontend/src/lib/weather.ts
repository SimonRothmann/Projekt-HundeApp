// WMO-Wettercodes (siehe Open-Meteo weather_code) auf kurze deutsche Texte
// und ein Symbol abgebildet. Bewusst gruppiert statt jeden Code einzeln:
// für ein Trainingstagebuch reicht "Regen" statt "mäßiger gefrierender
// Sprühregen".
export function weatherLabel(code: number | null | undefined): string | null {
  if (code == null) return null;
  if (code === 0) return "klar";
  if (code <= 3) return "bewölkt";
  if (code <= 48) return "Nebel";
  if (code <= 57) return "Nieselregen";
  if (code <= 67) return "Regen";
  if (code <= 77) return "Schnee";
  if (code <= 82) return "Schauer";
  if (code <= 86) return "Schneeschauer";
  return "Gewitter";
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

/** "12,4 °C" - deutsche Schreibweise mit einer Nachkommastelle. */
export function formatTemperature(celsius: number | null | undefined): string | null {
  if (celsius == null) return null;
  return `${celsius.toFixed(1).replace(".", ",")} °C`;
}

/**
 * Temperaturänderung als vorzeichenbehafteter Text ("+3,2 K"). Bei der Fährte
 * die eigentlich interessante Größe: die Änderung zwischen Legen und Suchen
 * bestimmt maßgeblich, wie sich die Geruchsspur hält.
 */
export function formatDelta(delta: number | null | undefined): string | null {
  if (delta == null) return null;
  const sign = delta > 0 ? "+" : "";
  return `${sign}${delta.toFixed(1).replace(".", ",")} K`;
}

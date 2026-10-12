import type { TextUebersetzer } from "@/lib/karte/spur-daten";

/**
 * Die Beschriftungen, die MapLibre selbst anzeigt (Zoomknöpfe, Hinweis bei
 * Zwei-Finger-Bedienung, Namensnennung). Schlüssel sind die von MapLibre
 * (defaultLocale in maplibre-gl.d.ts); fehlt einer, bleibt dessen englischer
 * Standard.
 *
 * Wird beim Erzeugen der Karte einmal gelesen - ein Sprachwechsel gilt für
 * Karten, die danach geöffnet werden.
 */
export function kartenTexte(t: TextUebersetzer): Record<string, string> {
  return {
    "AttributionControl.ToggleAttribution": t("Namensnennung ein- oder ausblenden"),
    "CooperativeGesturesHandler.WindowsHelpText": t("Mit Strg + Scrollen zoomst du die Karte"),
    "CooperativeGesturesHandler.MacHelpText": t("Mit ⌘ + Scrollen zoomst du die Karte"),
    "CooperativeGesturesHandler.MobileHelpText": t("Mit zwei Fingern verschiebst du die Karte"),
    "Map.Title": t("Karte"),
    "NavigationControl.ZoomIn": t("Hineinzoomen"),
    "NavigationControl.ZoomOut": t("Herauszoomen"),
    "Popup.Close": t("Hinweis schließen"),
  };
}

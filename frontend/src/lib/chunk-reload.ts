// Erkennung & Behandlung von Chunk-Load-Fehlern.
//
// Nach einem neuen Deployment ändern sich die inhaltsgehashten JS-Chunk-Namen.
// Ein Client, der die App VOR dem Deploy geladen hat, fordert beim
// Client-seitigen Navigieren (z.B. auf eine Hundeseite) noch den alten
// Chunk-Namen an - der auf dem Server nicht mehr existiert (404) -> die
// dynamische Import-Promise wird abgelehnt ("ChunkLoadError"). Ohne Behandlung
// bleibt die Seite unbenutzbar, bis der Nutzer manuell neu lädt (der Neuladen
// holt frisches HTML mit den NEUEN Chunk-Namen). Diese Helfer machen daraus
// eine automatische, einmalige Wiederherstellung.

export function isChunkLoadError(err: unknown): boolean {
  if (err == null) return false;
  const name = typeof err === "object" && "name" in err ? String((err as { name?: unknown }).name ?? "") : "";
  const msg = err instanceof Error ? err.message : String(err);
  return (
    name === "ChunkLoadError" ||
    /loading chunk [^ ]+ failed/i.test(msg) ||
    /failed to fetch dynamically imported module/i.test(msg) ||
    /error loading dynamically imported module/i.test(msg) ||
    /importing a module script failed/i.test(msg)
  );
}

const RELOAD_KEY = "dogity-chunk-reload-at";
// Innerhalb dieses Fensters wird NICHT erneut automatisch neu geladen - so
// entsteht bei einem dauerhaft kaputten Zustand keine Reload-Schleife; ein
// einmaliger Stale-Chunk nach Deploy wird dagegen sauber abgefangen.
const RELOAD_WINDOW_MS = 20_000;

/**
 * Lädt die Seite EINMAL neu, sofern nicht gerade eben schon deswegen neu
 * geladen wurde. Gibt true zurück, wenn ein Reload ausgelöst wurde.
 */
export function reloadOnceForChunkError(): boolean {
  if (typeof window === "undefined") return false;
  try {
    const last = Number(window.sessionStorage.getItem(RELOAD_KEY) ?? "0");
    if (Date.now() - last < RELOAD_WINDOW_MS) return false;
    window.sessionStorage.setItem(RELOAD_KEY, String(Date.now()));
  } catch {
    // sessionStorage nicht verfügbar: trotzdem einmalig neu laden.
  }
  window.location.reload();
  return true;
}

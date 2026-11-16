/**
 * Ein kurzes Vibrieren als Bestätigung einer Hauptaktion (Training gespeichert, Marker gesetzt).
 * Nur, wo der Browser `navigator.vibrate` kennt (Android; iOS Safari nicht - dort passiert nichts),
 * und nicht bei "Bewegung reduzieren".
 */
export function kurzeHaptik(ms = 10): void {
  if (typeof navigator === "undefined" || typeof navigator.vibrate !== "function") return;
  if (typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) return;
  navigator.vibrate(ms);
}

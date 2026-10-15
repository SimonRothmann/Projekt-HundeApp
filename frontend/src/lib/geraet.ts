/**
 * Ob das Gerät mit dem Finger bedient wird (Handy, Tablet) statt mit Maus
 * oder Trackpad.
 *
 * Für Hinweise, die nur auf dem Handy Sinn ergeben ("Als App installieren",
 * "Mitteilungen aufs Handy"): Am Rechner nutzt niemand Dogity auf dem
 * Hundeplatz, und der Browser bietet das Installieren dort selbst an.
 * Bewusst nur (pointer: coarse) und nicht die Fensterbreite - ein schmales
 * Fenster am Rechner ist kein Handy.
 */
export function istTouchGeraet(): boolean {
  try {
    return window.matchMedia("(pointer: coarse)").matches;
  } catch {
    // Kein window (Server) oder kein matchMedia: lieber kein Hinweis als ein falscher.
    return false;
  }
}

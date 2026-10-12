/**
 * Kann dieses Gerät eine MapLibre-Karte zeichnen?
 *
 * MapLibre 6 braucht WebGL2. Ohne greift new Map() erst beim Erzeugen ins
 * Leere - die Probe vorher erlaubt, statt einer leeren Fläche einen ruhigen
 * Hinweis zu zeigen. Der Kontext der Probe wird gleich wieder freigegeben:
 * Browser erlauben nur wenige WebGL-Kontexte je Seite, und auf der Hundeseite
 * liegen mehrere Karten.
 */
export function webgl2Verfuegbar(): boolean {
  try {
    const leinwand = document.createElement("canvas");
    const kontext = leinwand.getContext("webgl2");
    if (!kontext) return false;
    kontext.getExtension("WEBGL_lose_context")?.loseContext();
    return true;
  } catch {
    return false;
  }
}

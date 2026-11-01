/**
 * Je Gerät gemerkt, ob ein Hinweis weggeklickt wurde (Push-Karte,
 * Installations-Hinweis). Der Schlüssel unterscheidet die Hinweise. Jeder
 * Zugriff ist gesichert: in privaten Fenstern und bei gesperrten Website-Daten
 * wirft localStorage - dann erscheint der Hinweis eben beim nächsten Besuch
 * wieder, statt dass die App abstürzt.
 */
export function warWeggeklickt(schluessel: string): boolean {
  try {
    return window.localStorage.getItem(schluessel) !== null;
  } catch {
    return false;
  }
}

export function merkeWeggeklickt(schluessel: string): void {
  try {
    window.localStorage.setItem(schluessel, "1");
  } catch {
    // Ohne Speicher kommt der Hinweis beim nächsten Besuch wieder - harmlos.
  }
}

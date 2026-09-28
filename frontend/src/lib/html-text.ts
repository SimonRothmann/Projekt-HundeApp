/**
 * Macht aus beliebigem Text ein HTML-Stück, das genau diesen Text zeigt.
 *
 * Leaflet schreibt Zeichenketten in Tooltips, Popups und divIcons per
 * innerHTML in die Seite - anders als React, das Text immer als Text
 * behandelt. Die Beschriftung eines Fährten-Markers kommt aber von Nutzer:innen
 * und war bis 2026-09-28 dort ungefiltert gelandet: Ein Label wie
 * `<img src=x onerror=...>` lief beim Antippen des Markers als Skript - bei
 * jeder Person, die die Fährte sieht (Mitbesitzer:innen, Trainer:innen), und
 * damit mit deren Anmeldung.
 *
 * Codiert wird bei der AUSGABE, nicht beim Speichern: Der Server bewahrt den
 * Text unverändert (ein "&" in "Wiese & Wald" bleibt ein "&"), und jede
 * Stelle, die HTML baut, ist selbst dafür zuständig. Welche Stellen das sind,
 * prüft html-senken.test.ts.
 */
export function alsHtmlText(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

/**
 * Die Prüfung hinter der Rückfrage mit Eingabe (z. B. "Namen des Hundes
 * eintippen"). Bewusst eigene Datei, damit sie ohne Oberfläche testbar ist.
 *
 * Leerzeichen am Rand zählen nicht: Handy-Tastaturen hängen beim Vorschlag
 * gern eines an, und der Name selbst wird davon nicht anders. Sonst muss es
 * genau passen - auch in der Groß-/Kleinschreibung.
 */
export function eingabePasst(eingabe: string, muss: string): boolean {
  return eingabe.trim() === muss.trim();
}

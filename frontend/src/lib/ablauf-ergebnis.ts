/**
 * Reine Entscheidungen des Ergebnis-Fensters "Fährte abgelaufen" - getrennt von
 * der Komponente, damit sie ohne Oberfläche prüfbar sind.
 */

/**
 * Was beim Schließen mit dem Kommentar zu tun ist: null, wenn nichts (die
 * Eingabe entspricht dem, was schon gespeichert ist), sonst der neue Wert -
 * leer heißt "kein Kommentar" und wird als null gespeichert, wie überall bei
 * den Ablauf-Kommentaren.
 *
 * Verglichen wird ohne Leerraum an den Rändern: Ein versehentliches Leerzeichen
 * ist keine Änderung und kostet keine Anfrage.
 */
export function kommentarAenderung(gespeichert: string | null | undefined, eingabe: string): { kommentar: string | null } | null {
  const neu = eingabe.trim() || null;
  const alt = gespeichert?.trim() || null;
  return neu === alt ? null : { kommentar: neu };
}

/** So weit muss ein Finger nach unten wandern, damit es ein Wischen zum Schließen ist (in Pixeln). */
export const WISCH_MINDESTWEG = 60;

/**
 * Ob eine Fingerbewegung das Fenster wegwischen soll: deutlich nach unten und
 * dabei mehr senkrecht als seitlich. Eine schräge Bewegung ist eher ein
 * Versuch, etwas anzutippen oder seitlich zu streichen, und soll nichts
 * schließen.
 */
export function istWischNachUnten(dx: number, dy: number): boolean {
  return dy >= WISCH_MINDESTWEG && dy > Math.abs(dx) * 1.5;
}

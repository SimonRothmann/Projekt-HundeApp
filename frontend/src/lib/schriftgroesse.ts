/**
 * Schriftgröße der Oberfläche.
 *
 * Drei Stufen und kein frei wählbarer Prozentwert: Jede Stufe ist eine
 * Größe, in der die Oberfläche tatsächlich durchgesehen wurde. Ein Regler
 * brächte Zwischenwerte hervor, die nie jemand geprüft hat.
 *
 * Umgesetzt wird sie als Prozentwert auf dem Wurzelelement (siehe
 * globals.css), nicht als Pixelangabe: 100 % ist das, was der Browser
 * vorgibt. Wer dort schon größer eingestellt hat, behält seinen Vorsprung,
 * statt von uns überschrieben zu werden. Und weil sämtliche Größen und
 * Abstände in rem gerechnet sind, wächst das ganze Layout mit - wer schlecht
 * sieht, braucht nicht nur größere Buchstaben, sondern auch größere Knöpfe.
 */
export const SCHRIFTGROESSEN = ["normal", "gross", "sehr-gross"] as const;

export type Schriftgroesse = (typeof SCHRIFTGROESSEN)[number];

export const VORGABE_SCHRIFT: Schriftgroesse = "normal";

/**
 * Wo die Stufe zusätzlich im Browser liegt.
 *
 * Nicht als zweite Wahrheit gedacht, sondern als Zwischenspeicher fürs
 * Zeichnen: Die Einstellung kommt vom Server, der antwortet aber erst
 * einige hundert Millisekunden nach dem ersten Bild. Ohne den
 * Zwischenspeicher wüchse die Schrift bei JEDEM Seitenaufbau sichtbar
 * nach - das sähe kaputt aus. Der Server bleibt maßgeblich und überschreibt
 * den Wert, sobald er da ist.
 */
export const SCHRIFT_SPEICHER = "dogity.schriftgroesse";

/** Attribut am Wurzelelement, auf das die Regeln in globals.css greifen. */
export const SCHRIFT_ATTRIBUT = "data-schrift";

export function istSchriftgroesse(wert: unknown): wert is Schriftgroesse {
  return typeof wert === "string" && (SCHRIFTGROESSEN as readonly string[]).includes(wert);
}

/**
 * Macht aus einem gespeicherten Wert eine gültige Stufe.
 *
 * Alles Unbekannte wird zur Vorgabe - eine aus einer künftigen Fassung
 * stammende Stufe, ein leerer Zwischenspeicher, ein von Hand verbogener
 * Eintrag. Die Oberfläche steht dann normal da statt gar nicht.
 */
export function bestimmeSchriftgroesse(gespeichert: string | null | undefined): Schriftgroesse {
  return istSchriftgroesse(gespeichert) ? gespeichert : VORGABE_SCHRIFT;
}

/**
 * Legt die Stufe auf das Wurzelelement und in den Zwischenspeicher.
 *
 * Die Vorgabe entfernt das Attribut, statt "normal" hineinzuschreiben - so
 * greift gar keine Regel und es bleibt beim Wert des Browsers.
 */
export function wendeSchriftgroesseAn(stufe: Schriftgroesse): void {
  if (typeof document === "undefined") return;

  if (stufe === VORGABE_SCHRIFT) document.documentElement.removeAttribute(SCHRIFT_ATTRIBUT);
  else document.documentElement.setAttribute(SCHRIFT_ATTRIBUT, stufe);

  try {
    if (stufe === VORGABE_SCHRIFT) localStorage.removeItem(SCHRIFT_SPEICHER);
    else localStorage.setItem(SCHRIFT_SPEICHER, stufe);
  } catch {
    // Privater Modus oder gesperrter Speicher: Dann fehlt nur der
    // Zwischenspeicher fürs nächste Zeichnen. Die Einstellung selbst wirkt
    // trotzdem und kommt beim nächsten Aufbau wieder vom Server.
  }
}

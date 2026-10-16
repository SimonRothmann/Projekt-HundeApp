/**
 * Hilfen für die Seite /email-abmelden (Abmelden von den Mails über den Link
 * in der Mail, ohne Anmeldung).
 */

/**
 * Wie lang ein Token höchstens sein darf. Ein echtes ist unter 150 Zeichen
 * lang; der Server weist alles über 512 selbst ab - eine Anfrage dafür
 * brauchen wir gar nicht erst zu stellen.
 */
const TOKEN_HOECHSTENS = 512;

/**
 * Das Token aus der Adresszeile, oder null, wenn es keines sein kann.
 *
 * Das Token ist base64url (Buchstaben, Ziffern, "-" und "_"). Alles andere ist
 * ein verstümmelter Link und geht direkt in den Fehlerzustand, ohne dass der
 * Server etwas gefragt wird.
 */
export function abmeldeTokenAus(wert: string | null | undefined): string | null {
  const token = wert?.trim();
  if (!token || token.length > TOKEN_HOECHSTENS) return null;
  return /^[A-Za-z0-9_-]+$/.test(token) ? token : null;
}

/**
 * Was ein Fehlschlag der Abmeldung bedeutet. Nur eine ausdrückliche Absage des
 * Servers (400) heißt "Link ungültig"; Netz weg, Drosselung (429) oder ein
 * Serverfehler sagen nichts über den Link und sollen ihn nicht als kaputt
 * erscheinen lassen - es genügt, es noch einmal zu versuchen.
 */
export function abmeldeFehlerArt(status: number | null): "ungueltig" | "nichtErreichbar" {
  return status === 400 || status === 404 ? "ungueltig" : "nichtErreichbar";
}

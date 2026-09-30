/**
 * Text in die Zwischenablage legen - auch dort, wo navigator.clipboard fehlt
 * oder verweigert wird.
 *
 * navigator.clipboard gibt es nur in sicheren Kontexten (HTTPS, localhost).
 * Wer die App im Heimnetz über die LAN-Adresse aufruft, oder ein älterer
 * Browser, bekommt ohne Rückfall einen Knopf, der nichts tut. Der Rückfall
 * markiert den Text in einem unsichtbaren Feld und kopiert per
 * document.execCommand - veraltet, aber überall vorhanden.
 *
 * Gibt zurück, ob es geklappt hat; der Aufrufer meldet dem Nutzer das
 * Ergebnis, statt einen Erfolg zu behaupten.
 */
export async function kopiereText(text: string): Promise<boolean> {
  try {
    if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // Verweigert (Berechtigung, nicht fokussiert): weiter mit dem Rückfall.
  }

  try {
    const feld = document.createElement("textarea");
    feld.value = text;
    // Außerhalb des Bildes und nicht editierbar: auf dem Handy soll dabei
    // weder die Tastatur aufgehen noch die Seite springen.
    feld.setAttribute("readonly", "");
    feld.style.position = "fixed";
    feld.style.top = "0";
    feld.style.left = "-9999px";
    document.body.appendChild(feld);
    feld.select();
    feld.setSelectionRange(0, text.length);
    const geklappt = document.execCommand("copy");
    document.body.removeChild(feld);
    return geklappt;
  } catch {
    return false;
  }
}

/**
 * Über das Teilen-Menü des Geräts weitergeben, falls es eines gibt.
 *
 * "geteilt": das Menü wurde benutzt. "abgebrochen": die Person hat es
 * geschlossen - keine Fehlermeldung wert. "nichtVerfuegbar": der Browser
 * kennt navigator.share nicht (Desktop), der Aufrufer weicht aufs Kopieren aus.
 */
export async function teileLink(daten: { title: string; text: string; url: string }): Promise<
  "geteilt" | "abgebrochen" | "nichtVerfuegbar"
> {
  if (typeof navigator === "undefined" || typeof navigator.share !== "function") return "nichtVerfuegbar";
  try {
    await navigator.share(daten);
    return "geteilt";
  } catch (err) {
    if (err instanceof DOMException && err.name === "AbortError") return "abgebrochen";
    // Andere Fehler (nicht erlaubt, nicht unterstützte Daten): dann lieber kopieren.
    return "nichtVerfuegbar";
  }
}

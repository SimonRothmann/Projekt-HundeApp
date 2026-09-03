/**
 * Die wählbaren Kartenhintergründe.
 *
 * Der Stil entscheidet über das Aussehen der Karte, nicht die
 * Kartenbibliothek. Der OSM-Standardstil ist für Kartenzeichner gemacht und
 * zeigt jede Drogerie und jedes Bekleidungsgeschäft - auf einem Acker hilft
 * davon nichts.
 *
 * Bewusst nur Quellen OHNE Schlüssel: CARTO und Stadia sähen moderner aus,
 * liefern ohne API-Schlüssel aber ein Bild mit der Aufschrift
 * "API KEY REQUIRED" - und zwar mit HTTP 200, der Statuscode verrät es
 * nicht. Ein Schlüssel wäre eine Zugangsdatei mehr, die gepflegt,
 * ausgerollt und irgendwann erneuert werden muss.
 */
export type KartenEbene = "strasse" | "luftbild";

export const KARTEN_EBENEN: Record<
  KartenEbene,
  { label: string; url: string; attribution: string; maxZoom: number; abdunkelbar: boolean }
> = {
  strasse: {
    label: "Straße",
    url: "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
    attribution: "&copy; OpenStreetMap-Mitwirkende",
    maxZoom: 19,
    abdunkelbar: true,
  },
  luftbild: {
    // Für das Fährtelegen fachlich die bessere Grundlage: Man sieht den
    // Schlag selbst - Grenzen, Fahrgassen, Hecken - statt Ladenlokale.
    // Esri liefert bis in die Zoomstufen, die dafür nötig sind; der dunkle
    // Straßenstil desselben Anbieters meldet dort "Map data not yet
    // available" und fällt deshalb aus.
    label: "Luftbild",
    url: "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
    attribution: "&copy; Esri, Maxar, Earthstar Geographics",
    maxZoom: 19,
    // Ein invertiertes Luftbild sähe aus wie ein Negativ - das Bild ist
    // ohnehin dunkel genug.
    abdunkelbar: false,
  },
};

const SPEICHER_SCHLUESSEL = "dogity_kartenebene";

/**
 * Die zuletzt gewählte Ebene. Bewusst im localStorage und nicht in den
 * Nutzereinstellungen auf dem Server: Das ist eine Anzeigevorliebe dieses
 * Geräts - auf dem Handy draußen will man das Luftbild, am Rechner beim
 * Auswerten vielleicht die Straßenkarte.
 */
export function gespeicherteEbene(): KartenEbene {
  if (typeof window === "undefined") return "strasse";
  try {
    const wert = window.localStorage.getItem(SPEICHER_SCHLUESSEL);
    return wert === "luftbild" || wert === "strasse" ? wert : "strasse";
  } catch {
    // Privater Modus oder gesperrter Speicher - dann eben die Vorgabe.
    return "strasse";
  }
}

/** Ereignisname, über den sich alle Karten einer Seite abstimmen. */
export const EBENE_GEAENDERT = "dogity:kartenebene";

export function ebeneMerken(ebene: KartenEbene): void {
  try {
    window.localStorage.setItem(SPEICHER_SCHLUESSEL, ebene);
  } catch {
    // Merken ist Beiwerk, ein Fehler darf die Karte nicht stören.
  }
  // Alle offenen Karten mitziehen. Auf einer Seite liegen mehrere - das
  // Vollbild der Aufzeichnung und die Karten der bisherigen Fährten. Ohne
  // diese Nachricht liest jede die Auswahl nur beim Einhängen: Man wechselt
  // im Vollbild auf Luftbild, schließt es, und die Karte darunter zeigt
  // weiter die Straßenkarte.
  window.dispatchEvent(new CustomEvent(EBENE_GEAENDERT, { detail: ebene }));
}

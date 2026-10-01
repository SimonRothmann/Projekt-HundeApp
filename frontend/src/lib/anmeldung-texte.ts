import type { AnmeldeFehler } from "@/lib/anmeldung";
import type { Spalte } from "@/lib/anmeldung-csv";

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** Der ganze Satz zu einem Fehler - für das Formular und das Eintragen von Hand. */
export function anmeldeFehlerSatz(t: Uebersetzer, fehler: AnmeldeFehler): string {
  switch (fehler) {
    case "vorname":
      return t("Bitte den Vornamen angeben.");
    case "nachname":
      return t("Bitte den Nachnamen angeben.");
    case "rufname":
      return t("Bitte den Rufnamen des Hundes angeben.");
    case "rasse":
      return t("Bitte die Hunderasse angeben.");
    case "wurftagFehlt":
      return t("Bitte den Wurftag angeben.");
    case "wurftagFormat":
      return t("Der Wurftag ist kein gültiges Datum.");
    case "wurftagZukunft":
      return t("Der Wurftag darf nicht in der Zukunft liegen.");
    case "wurftagAlt":
      return t("Der Wurftag liegt mehr als zwei Jahre zurück.");
    case "telefon":
      return t("Bitte eine gültige Telefonnummer angeben.");
    case "zuLang":
      return t("Ein Eintrag ist zu lang.");
  }
}

/** Kurzfassung für die Importvorschau: "Zeile 5: {Kurzfassung}". */
export function anmeldeFehlerKurz(t: Uebersetzer, fehler: AnmeldeFehler): string {
  switch (fehler) {
    case "vorname":
      return t("Vorname fehlt");
    case "nachname":
      return t("Nachname fehlt");
    case "rufname":
      return t("Rufname fehlt");
    case "rasse":
      return t("Hunderasse fehlt");
    case "wurftagFehlt":
      return t("Wurftag fehlt");
    case "wurftagFormat":
      return t("Wurftag nicht lesbar");
    case "wurftagZukunft":
      return t("Wurftag liegt in der Zukunft");
    case "wurftagAlt":
      return t("Wurftag mehr als zwei Jahre vor der Anmeldung");
    case "telefon":
      return t("Telefonnummer ungültig");
    case "zuLang":
      return t("Eintrag zu lang");
  }
}

/** Die Überschrift der Spalte, wie sie im Google-Formular heißt. */
export function spaltenName(t: Uebersetzer, spalte: Spalte): string {
  switch (spalte) {
    case "zeitstempel":
      return t("Zeitstempel");
    case "vorname":
      return t("Vorname");
    case "nachname":
      return t("Nachname");
    case "rufname":
      return t("Rufname des Hundes");
    case "rasse":
      return t("Hunderasse");
    case "wurftag":
      return t("Wurftag");
    case "telefon":
      return t("Telefonnummer");
  }
}

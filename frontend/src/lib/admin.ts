import type { useT } from "@/lib/i18n";

/**
 * Reine Hilfen für die Admin-Übersicht - ohne Bildschirm prüfbar. Der
 * Übersetzer kommt von außen (wie in lib/profil.ts).
 */
type Uebersetzer = ReturnType<typeof useT>;

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Wohin ein alter Link `/admin?resetUser=<id>` jetzt führt.
 *
 * Solche Links stehen in Benachrichtigungen, die vor der Aufteilung des
 * Adminbereichs angelegt wurden (Passwort-Reset-Anfrage ohne Mailversand).
 * Durchgereicht wird nur eine echte Id: Der Wert kommt aus der Adresszeile
 * und landet wieder in einer - alles andere, was jemand dort hineinschreibt,
 * wird verworfen (null = kein Weiterleiten, die Übersicht erscheint).
 */
export function resetUserZiel(resetUser: string | null): string | null {
  if (!resetUser || !GUID.test(resetUser)) return null;
  return `/admin/nutzer?resetUser=${resetUser}`;
}

/** Ob ein Wert eine Guid ist (für ?resetUser auf der Nutzer-Seite). */
export function istGuid(wert: string | null | undefined): wert is string {
  return !!wert && GUID.test(wert);
}

export type ZuErledigenZeile = {
  href: string;
  titel: string;
  vorschau: string;
};

/**
 * Die Zeilen von "Zu erledigen": nur, was gerade etwas offen hat. Null
 * heißt auch "nicht abrufbar" - ein fehlgeschlagener Abruf blendet nur seine
 * eigene Zeile aus. Bleibt nichts übrig, ist die Liste leer und die Gruppe
 * verschwindet ganz.
 */
export function zuErledigenZeilen(
  zahlen: { antraege: number | null; verwaisteKonten: number | null },
  t: Uebersetzer,
): ZuErledigenZeile[] {
  const zeilen: ZuErledigenZeile[] = [];
  if (zahlen.antraege) {
    zeilen.push({ href: "/admin/vereine", titel: t("Vereinsanträge"), vorschau: String(zahlen.antraege) });
  }
  if (zahlen.verwaisteKonten) {
    zeilen.push({ href: "/admin/system", titel: t("Daten gelöschter Konten"), vorschau: String(zahlen.verwaisteKonten) });
  }
  return zeilen;
}

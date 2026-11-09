import { uebersetze } from "@/lib/i18n";
import { uebersetzbar, type Sprache } from "@/lib/i18n/sprachen";
import { ortsformat } from "@/lib/ortsformat";
import type { Wartungshinweis } from "@/lib/types";

/**
 * Wartungshinweis: das Banner VOR einer Wartung (aus der Datenbank) und der Zustand
 * "Wartung läuft", den der Proxy während der Wartung erzeugt.
 *
 * Läuft die Wartung, ist das Backend weg und kann keinen Hinweis mehr ausliefern. Dann
 * antwortet Caddy selbst mit 503 und der Kopfzeile X-Dogity-Wartung (deploy/caddy). Der
 * API-Helfer meldet jede Antwort hierher; das Banner zeigt dann den Zustand "während",
 * auch ohne Hinweis aus der Datenbank.
 */

/** Kopfzeile, an der die Antwort des Proxys als Wartung erkennbar ist (und nicht als Ausfall oder Neustart). */
export const WARTUNGS_KOPFZEILE = "X-Dogity-Wartung";

/** Eine 503 mit der Kopfzeile ist die Wartungsantwort des Proxys; eine 503 ohne sie bleibt ein gewöhnlicher Ausfall. */
export function istWartungsantwort(status: number, kopfzeile: string | null): boolean {
  return status === 503 && kopfzeile === "1";
}

/**
 * Der Zustand nach einer API-Antwort: Eine Wartungsantwort schaltet ihn ein, jede
 * erfolgreiche Antwort aus. Andere Fehler (401, 500, 503 ohne Kopfzeile) sagen nichts
 * darüber, ob die Wartung vorbei ist, und lassen ihn, wie er ist.
 */
export function wartungNachAntwort(aktuell: boolean, status: number, kopfzeile: string | null): boolean {
  if (istWartungsantwort(status, kopfzeile)) return true;
  if (status >= 200 && status < 400) return false;
  return aktuell;
}

let wartungLaeuft = false;
const hoerer = new Set<() => void>();

/** Vom API-Helfer nach jeder Antwort aufgerufen. */
export function wartungMelden(status: number, kopfzeile: string | null): void {
  const neu = wartungNachAntwort(wartungLaeuft, status, kopfzeile);
  if (neu === wartungLaeuft) return;
  wartungLaeuft = neu;
  hoerer.forEach((h) => h());
}

/** Für useSyncExternalStore. */
export function wartungAbonnieren(aufruf: () => void): () => void {
  hoerer.add(aufruf);
  return () => {
    hoerer.delete(aufruf);
  };
}

export function wartungLaeuftJetzt(): boolean {
  return wartungLaeuft;
}

/** Nur für Tests: den Zustand zurücksetzen. */
export function wartungZuruecksetzen(): void {
  wartungLaeuft = false;
}

/** Wie oft nach dem Hinweis gefragt wird (nur bei sichtbarem Tab), und wie oft, solange die Wartung läuft. */
export const WARTUNG_ABRUF_MS = 5 * 60_000;
export const WARTUNG_ABRUF_WAEHREND_MS = 30_000;

/** Schlüssel im localStorage, unter dem ein weggeklickter Hinweis steht. Er enthält den Änderungsstand: Ein geänderter Hinweis erscheint wieder. */
export function wegklickSchluessel(geaendertAm: string): string {
  return `dogity_wartung_weg_${geaendertAm}`;
}

// Standardtexte. "{wann}" ist je nach Zeitraum eine der drei Fassungen darunter.
const STANDARD_VORHER = uebersetzbar("Wartungsarbeiten {wann} – Dogity ist in dieser Zeit kurz nicht erreichbar. Einträge auf dem Handy werden gespeichert und danach nachgeschickt.");
const STANDARD_WAEHREND = uebersetzbar("Wartungsarbeiten laufen – Dogity ist gleich wieder da. Deine Einträge werden auf dem Gerät gespeichert und nachgeschickt.");
const WANN_GLEICHER_TAG = uebersetzbar("am {tag} von {beginn} bis {ende} Uhr");
const WANN_ANDERER_TAG = uebersetzbar("von {beginn} bis {ende} Uhr");
const WANN_OHNE_ENDE = uebersetzbar("am {tag} ab {beginn} Uhr");
// Zeitangabe vor einem eigenen Text des Betreibers.
const ZEIT_GLEICHER_TAG = uebersetzbar("{tag} {beginn}–{ende} Uhr");
const ZEIT_ANDERER_TAG = uebersetzbar("{beginn} – {ende} Uhr");
const ZEIT_OHNE_ENDE = uebersetzbar("{tag} ab {beginn} Uhr");

/**
 * Zeitpunkte in Ortszeit und Sprache des Geräts. Die Zeitzone ist die der Person und
 * nicht die des Vereins: Wer im Urlaub ist, liest die Uhrzeit, die sie selbst erlebt.
 */
function uhrzeit(d: Date, ort: string): string {
  return d.toLocaleTimeString(ort, { hour: "2-digit", minute: "2-digit" });
}

function tag(d: Date, ort: string, wochentag: "long" | "short"): string {
  return d.toLocaleDateString(ort, { weekday: wochentag, day: "numeric", month: wochentag });
}

function tagMitUhrzeit(d: Date, ort: string): string {
  return `${tag(d, ort, "short")} ${uhrzeit(d, ort)}`;
}

export type BannerInhalt = {
  /** Die Wartung läuft - das Banner ist dann nicht wegklickbar. */
  laeuft: boolean;
  text: string;
};

/**
 * Was das Banner sagt - oder null, wenn es nichts zu sagen gibt.
 *
 * @param hinweis Der Hinweis aus der Datenbank, falls einer anzuzeigen ist.
 * @param wartungErkannt Der Proxy hat zuletzt mit der Wartungsantwort geantwortet.
 */
export function bannerInhalt(
  hinweis: Pick<Wartungshinweis, "text" | "beginn" | "ende" | "laeuft"> | null,
  wartungErkannt: boolean,
  sprache: Sprache,
): BannerInhalt | null {
  if (!hinweis && !wartungErkannt) return null;
  const laeuft = wartungErkannt || (hinweis?.laeuft ?? false);
  const ort = ortsformat(sprache);

  if (!hinweis) return { laeuft, text: uebersetze(sprache, STANDARD_WAEHREND) };

  const beginn = new Date(hinweis.beginn);
  const ende = hinweis.ende ? new Date(hinweis.ende) : null;
  const gleicherTag = ende !== null && ende.toDateString() === beginn.toDateString();
  const werte = {
    tag: tag(beginn, ort, hinweis.text ? "short" : "long"),
    beginn: ende && !gleicherTag ? tagMitUhrzeit(beginn, ort) : uhrzeit(beginn, ort),
    ende: ende ? (gleicherTag ? uhrzeit(ende, ort) : tagMitUhrzeit(ende, ort)) : "",
  };

  if (hinweis.text) {
    const zeit = uebersetze(sprache, ende ? (gleicherTag ? ZEIT_GLEICHER_TAG : ZEIT_ANDERER_TAG) : ZEIT_OHNE_ENDE, werte);
    return { laeuft, text: `${zeit}: ${hinweis.text}` };
  }
  if (laeuft) return { laeuft, text: uebersetze(sprache, STANDARD_WAEHREND) };

  const wann = uebersetze(sprache, ende ? (gleicherTag ? WANN_GLEICHER_TAG : WANN_ANDERER_TAG) : WANN_OHNE_ENDE, werte);
  return { laeuft, text: uebersetze(sprache, STANDARD_VORHER, { wann }) };
}

/** Datum und Uhrzeit, wie die Eingabefelder sie halten (Ortszeit). */
export type Eingabezeit = { datum: string; zeit: string };

const DATUM_FORM = /^\d{4}-\d{2}-\d{2}$/;
const ZEIT_FORM = /^\d{2}:\d{2}$/;

/** Ein Zeitpunkt (ISO) als Datum und Uhrzeit in Ortszeit für die Eingabefelder; leer bei null. */
export function zuEingabe(iso: string | null): Eingabezeit {
  if (!iso) return { datum: "", zeit: "" };
  const d = new Date(iso);
  const zweistellig = (n: number) => String(n).padStart(2, "0");
  return {
    datum: `${d.getFullYear()}-${zweistellig(d.getMonth() + 1)}-${zweistellig(d.getDate())}`,
    zeit: `${zweistellig(d.getHours())}:${zweistellig(d.getMinutes())}`,
  };
}

/** Datum und Uhrzeit aus den Eingabefeldern (Ortszeit) als ISO-Zeitpunkt; null, wenn eines fehlt oder ungültig ist. */
export function ausEingabe({ datum, zeit }: Eingabezeit): string | null {
  if (!DATUM_FORM.test(datum) || !ZEIT_FORM.test(zeit)) return null;
  const d = new Date(`${datum}T${zeit}`);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export type Entwurf = {
  beginn: Eingabezeit;
  ende: Eingabezeit;
  text: string;
  aktiv: boolean;
};

export type EntwurfErgebnis =
  | { art: "ok"; anfrage: { text: string | null; beginn: string; ende: string | null; aktiv: boolean } }
  | { art: "beginn" | "ende" | "reihenfolge" };

/**
 * Prüft die Eingabe des Betreibers vor dem Senden. Das Ende ist freiwillig, aber nur
 * ganz (Datum und Uhrzeit) oder gar nicht; maßgeblich bleibt die Prüfung im Server.
 */
export function entwurfPruefen(entwurf: Entwurf): EntwurfErgebnis {
  const beginn = ausEingabe(entwurf.beginn);
  if (!beginn) return { art: "beginn" };

  const endeLeer = entwurf.ende.datum === "" && entwurf.ende.zeit === "";
  const ende = endeLeer ? null : ausEingabe(entwurf.ende);
  if (!endeLeer && !ende) return { art: "ende" };
  if (ende && new Date(ende) <= new Date(beginn)) return { art: "reihenfolge" };

  return { art: "ok", anfrage: { text: entwurf.text.trim() || null, beginn, ende, aktiv: entwurf.aktiv } };
}

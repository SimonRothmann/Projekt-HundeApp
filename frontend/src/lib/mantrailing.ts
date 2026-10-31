import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Mantrailing als Art der GPS-Aufzeichnung (Paket M1).
 *
 * Die Aufzeichnung ist dieselbe wie bei der Fährte - die Laufspur der
 * Versteckperson, danach der Lauf des Hundes. Anders sind die Grenzen der
 * Auswertung, ein paar Begriffe und die zusätzlichen Angaben. Was Mantrailing
 * ist, steht an GENAU einer Stelle: hier.
 */

/** Sportart-Code - wie SportCatalogSeeder.MantrailingCode im Backend. */
export const MANTRAILING_CODE = "MANTRAILING";

/** GpsTrack.kind: die Art der Aufzeichnung (GpsTrackKind im Backend, als Zahl). */
export const ART_FAEHRTE = 0;
export const ART_MANTRAILING = 1;

/**
 * Ob etwas Mantrailing ist: ein Sportart-Code, eine Sportart oder eine
 * aufgezeichnete Spur (an `kind`). Lose Prüfung - aus einem älteren Read-Cache
 * oder von einem älteren Backend-Stand fehlt `kind` ganz (undefined = Fährte).
 */
export function istMantrailing(wovon: string | { code: string } | { kind?: number | null } | null | undefined): boolean {
  if (wovon == null) return false;
  if (typeof wovon === "string") return wovon === MANTRAILING_CODE;
  if ("code" in wovon) return wovon.code === MANTRAILING_CODE;
  return wovon.kind === ART_MANTRAILING;
}

/** Grenzen der Auswertung: bis `gruenBis` Meter grün, bis `gelbBis` gelb, darüber rot; "auf der Spur" zählt bis `gruenBis`. */
export type SpurGrenzen = { gruenBis: number; gelbBis: number };

/**
 * Fährte 3/6 m, Mantrailing 10/25 m. Der Hund arbeitet beim Mantrailing an
 * langer Leine und sucht den Geruch auch neben der Laufspur - eng an der Linie
 * wie bei der Fährte ist dort nicht das Ziel. Dieselben Zahlen stehen im
 * Backend (GpsTrackEvaluator.FaehrteToleranz und MantrailingToleranz): beim
 * Ändern beide Stellen anfassen.
 */
export const FAEHRTE_GRENZEN: SpurGrenzen = { gruenBis: 3, gelbBis: 6 };
export const MANTRAILING_GRENZEN: SpurGrenzen = { gruenBis: 10, gelbBis: 25 };

/** Die Grenzen für eine Spur (oder deren Art); ohne Angabe die der Fährte. */
export function spurGrenzen(art?: { kind?: number | null } | null): SpurGrenzen {
  return istMantrailing(art) ? MANTRAILING_GRENZEN : FAEHRTE_GRENZEN;
}

/** Anzeigeverhalten an der Person (TrailIndication im Backend, als Zahl). */
export const ANZEIGEVERHALTEN = [
  { value: 0, label: uebersetzbar("Verbellen") },
  { value: 1, label: uebersetzbar("Vorsitzen") },
  { value: 2, label: uebersetzbar("Vorstehen") },
  { value: 3, label: uebersetzbar("Vorliegen") },
  { value: 4, label: uebersetzbar("Rückverweisen") },
  { value: 5, label: uebersetzbar("Sonstiges") },
] as const;

/** Anzeigeverhalten ohne Überschrift: der Name der Anzeige, oder null, wenn keine angegeben ist. */
export function anzeigeverhaltenLabel(wert: number | null | undefined): string | null {
  return ANZEIGEVERHALTEN.find((a) => a.value === wert)?.label ?? null;
}

const MINUTEN_JE_STUNDE = 60;
/** So lang darf ein Trail liegen (Backend: GpsTrackService.MaxTrailAlterMinuten): eine Woche. */
export const MAX_TRAILALTER_STUNDEN = 168;

/**
 * Trailalter in Stunden für das Eingabefeld: höchstens zwei Nachkommastellen, ohne
 * überflüssige Nullen ("1,5"; in der englischen Oberfläche "1.5" - gelesen wird
 * beides, siehe minutenAusStundenText).
 */
export function stundenText(minuten: number | null | undefined, sprache: "de" | "en" = "de"): string {
  if (minuten == null) return "";
  const text = String(Math.round((minuten / MINUTEN_JE_STUNDE) * 100) / 100);
  return sprache === "en" ? text : text.replace(".", ",");
}

/**
 * Minuten aus einer Eingabe in Stunden ("1,5", "2", "1.25"). null bei leerer
 * Eingabe, undefined bei etwas, das keine gültige Dauer ist (Text, negativ,
 * länger als eine Woche) - der Aufrufer unterscheidet "leer lassen" von "falsch".
 */
export function minutenAusStundenText(text: string): number | null | undefined {
  const bereinigt = text.trim().replace(",", ".");
  if (bereinigt === "") return null;
  if (!/^\d+(\.\d+)?$/.test(bereinigt)) return undefined;
  const stunden = Number(bereinigt);
  if (!Number.isFinite(stunden) || stunden > MAX_TRAILALTER_STUNDEN) return undefined;
  return Math.round(stunden * MINUTEN_JE_STUNDE);
}

/** Was vor dem Legen eines Trails gewählt wird (die Angaben zur Spur, die schon feststehen). */
export type TrailAngabenVorLegen = { doubleBlind: boolean; negativeTrail: boolean; scentArticle: string };

export const LEERE_TRAIL_ANGABEN: TrailAngabenVorLegen = { doubleBlind: false, negativeTrail: false, scentArticle: "" };

/** Das Ergebnis eines Laufs, wie es das Ergebnis-Fenster eingibt. */
export type TrailErgebnisEingabe = { gefunden: boolean | null; anzeige: number | null; alterText: string };

type TrailSpur = { ageMinutes: number | null; doubleBlind?: boolean; negativeTrail?: boolean; scentArticle?: string | null };
type TrailLauf = { personFound?: boolean | null; indication?: number | null };

export type TrailAenderungen =
  | { fehler: "alter" }
  | {
      fehler?: undefined;
      /** Body für PUT .../walk-runs/{id}/trail; null, wenn sich am Ergebnis nichts geändert hat. */
      ergebnis: { personFound: boolean | null; indication: number | null } | null;
      /** Body für PUT .../{id}/trail; null, wenn sich das Trailalter nicht geändert hat. */
      trail: { ageMinutes: number | null; doubleBlind: boolean; negativeTrail: boolean; scentArticle: string | null } | null;
    };

/**
 * Was beim Schließen des Ergebnis-Fensters zu speichern ist - nur, was sich gegenüber dem
 * gespeicherten Stand geändert hat. Das Anzeigeverhalten gibt es nur bei einem Fund und nicht beim
 * Negativ-Trail (wie im Backend, GpsTrackService.UpdateTrailRunAsync); ein unlesbares Trailalter
 * ist ein Fehler, den das Fenster meldet, statt etwas Falsches zu speichern.
 */
export function trailAenderungen(track: TrailSpur, run: TrailLauf, eingabe: TrailErgebnisEingabe): TrailAenderungen {
  const alter = minutenAusStundenText(eingabe.alterText);
  if (alter === undefined) return { fehler: "alter" };

  const anzeige = eingabe.gefunden === true && !track.negativeTrail ? eingabe.anzeige : null;
  const ergebnisGeaendert = eingabe.gefunden !== (run.personFound ?? null) || anzeige !== (run.indication ?? null);

  return {
    ergebnis: ergebnisGeaendert ? { personFound: eingabe.gefunden, indication: anzeige } : null,
    trail:
      alter !== (track.ageMinutes ?? null)
        ? {
            ageMinutes: alter,
            doubleBlind: track.doubleBlind ?? false,
            negativeTrail: track.negativeTrail ?? false,
            scentArticle: track.scentArticle ?? null,
          }
        : null,
  };
}

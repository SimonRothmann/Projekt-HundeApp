import type { Sprache } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { ortsformat } from "@/lib/ortsformat";
import { pruefeErgebnis, punkteText } from "@/lib/pruefung";
import { tagTeile } from "@/lib/kalendertag";
import {
  PRUEFUNG_ERGEBNIS,
  TEAM_ART,
  type PruefungErgebnis,
  type PruefungKandidat,
  type PruefungOrdnung,
  type PruefungTeam,
  type VereinsPruefung,
  type VereinsPruefungHinweis,
  type VereinsPruefungListe,
} from "@/lib/types";

/**
 * Rechnen und Prüfen rund um die Prüfungstage des Vereins (Prüfungsleitung) - als reine
 * Funktionen, damit Liste, Detail, Druckansicht und Fenster dasselbe meinen. Die Rechte,
 * die Reihenfolge und die Regeln für Ergebnis und Ziel entscheidet der Server; hier steht,
 * wie die Oberfläche sortiert, gruppiert und Eingaben vor dem Senden prüft.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** Kommende Tage (heute und später) aufsteigend, vergangene absteigend - der Server liefert alle nach Datum aufsteigend. */
export function kommendeUndVergangene(
  liste: readonly VereinsPruefungListe[],
  heute: string,
): { kommend: VereinsPruefungListe[]; vergangen: VereinsPruefungListe[] } {
  const nachDatum = (a: VereinsPruefungListe, b: VereinsPruefungListe) =>
    a.date.localeCompare(b.date) || (a.startTime ?? "").localeCompare(b.startTime ?? "");
  return {
    kommend: liste.filter((p) => p.date >= heute).sort(nachDatum),
    vergangen: liste.filter((p) => p.date < heute).sort((a, b) => nachDatum(b, a)),
  };
}

/** "10:00" aus "10:00:00"; null ohne Uhrzeit. */
export function uhrzeitText(zeit: string | null | undefined): string | null {
  return zeit ? zeit.slice(0, 5) : null;
}

/** "BH, IBGH 1" - die Namen der Ordnungen in der Sprache der Oberfläche. */
export function ordnungenText(ordnungen: readonly PruefungOrdnung[], sprache: Sprache): string {
  return ordnungen.map((o) => katalogName(o, sprache)).join(", ");
}

/** Tag und Monat ohne Jahr: "14.3." bzw. "14/03". */
export function kurzDatum(iso: string, sprache: Sprache): string {
  const [jahr, monat, tag] = tagTeile(iso);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(ortsformat(sprache), { day: "numeric", month: "numeric" });
}

/** "Samstag, 14. März 2027" - der Prüfungstag als Überschrift. */
export function datumLang(iso: string, sprache: Sprache): string {
  const [jahr, monat, tag] = tagTeile(iso);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(ortsformat(sprache), {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  });
}

/** "Nächste Prüfung: BH, IBGH 1 am 14.3." - die Zeile für Training und Vorstand (ohne Meldeliste, ohne Link). */
export function naechstePruefungText(t: Uebersetzer, hinweis: VereinsPruefungHinweis, sprache: Sprache): string {
  const datum = kurzDatum(hinweis.date, sprache);
  return hinweis.regulations.length === 0
    ? t("Nächste Prüfung am {datum}", { datum })
    : t("Nächste Prüfung: {ordnungen} am {datum}", { ordnungen: ordnungenText(hinweis.regulations, sprache), datum });
}

/** Die Teams je Ordnung, in der Reihenfolge der Ordnungen des Tages; innerhalb in der Reihenfolge der Starterliste. */
export function gruppiereTeams(pruefung: Pick<VereinsPruefung, "regulations" | "entries">): { ordnung: PruefungOrdnung; teams: PruefungTeam[] }[] {
  return pruefung.regulations
    .map((ordnung) => ({
      ordnung,
      teams: pruefung.entries.filter((e) => e.regulationId === ordnung.id).sort((a, b) => a.startNumber - b.startNumber),
    }))
    .filter((gruppe) => gruppe.teams.length > 0);
}

/** Wer geführt wird: Hundeführer:in, bei einem Mitglied, das es nicht mehr gibt, ein Platzhalter. */
export function teamName(t: Uebersetzer, team: Pick<PruefungTeam, "kind" | "handlerName">): string {
  return team.kind === TEAM_ART.EhemaligesMitglied || !team.handlerName ? t("Ehemaliges Mitglied") : team.handlerName;
}

/** Der Hund; bei einem ehemaligen Mitglied gibt es keinen Namen mehr. */
export function hundName(team: Pick<PruefungTeam, "dogName">): string {
  return team.dogName || "–";
}

/** Ob schon ein Ergebnis eingetragen ist - dann gibt es die Ergebnisliste und der Tag lässt sich nicht mehr absagen. */
export function hatErgebnisse(teams: readonly Pick<PruefungTeam, "result">[]): boolean {
  return teams.some((e) => e.result !== PRUEFUNG_ERGEBNIS.Offen);
}

export type ErgebnisZaehlung = { bestanden: number; nichtBestanden: number; nichtAngetreten: number; offen: number };

export function zaehleErgebnisse(teams: readonly Pick<PruefungTeam, "result">[]): ErgebnisZaehlung {
  const anzahl = (art: PruefungErgebnis) => teams.filter((e) => e.result === art).length;
  return {
    bestanden: anzahl(PRUEFUNG_ERGEBNIS.Bestanden),
    nichtBestanden: anzahl(PRUEFUNG_ERGEBNIS.NichtBestanden),
    nichtAngetreten: anzahl(PRUEFUNG_ERGEBNIS.NichtAngetreten),
    offen: anzahl(PRUEFUNG_ERGEBNIS.Offen),
  };
}

/** Das Wort zum Ergebnis, wie es im Chip und auf der Liste steht. */
export function ergebnisWort(t: Uebersetzer, art: PruefungErgebnis): string {
  switch (art) {
    case PRUEFUNG_ERGEBNIS.Bestanden:
      return t("bestanden");
    case PRUEFUNG_ERGEBNIS.NichtBestanden:
      return t("nicht bestanden");
    case PRUEFUNG_ERGEBNIS.NichtAngetreten:
      return t("nicht angetreten");
    default:
      return t("Ergebnis offen");
  }
}

/** Ergebnis mit Punkten: "bestanden · 272 von 300 Punkten". */
export function ergebnisText(t: Uebersetzer, team: Pick<PruefungTeam, "result" | "score">, hoechstens: number | null | undefined): string {
  const wort = ergebnisWort(t, team.result);
  return team.score != null ? `${wort} · ${punkteText(t, team.score, hoechstens)}` : wort;
}

// ---------- Eingaben prüfen ----------

export type TagEingabe = { datum: string; ort: string };

/** Fehlermeldung zu Datum und Ort eines Prüfungstags, sonst null. Die Längen der Texte hält maxLength am Eingabefeld, der Server prüft sie noch einmal. */
export function pruefeTag(t: Uebersetzer, e: TagEingabe): string | null {
  if (!e.datum) return t("Bitte das Datum der Prüfung angeben.");
  if (!e.ort.trim()) return t("Bitte den Ort der Prüfung angeben.");
  return null;
}

export type GastEingabe = { fuehrer: string; hund: string };

/** Fehlermeldung zu einem Gast-Team, sonst null: Name und Hund sind nötig, Verein und Rasse optional. */
export function pruefeGast(t: Uebersetzer, e: GastEingabe): string | null {
  return !e.fuehrer.trim() || !e.hund.trim() ? t("Bitte Name und Hund des Gasts angeben.") : null;
}

/**
 * Fehlermeldung zu einem Ergebnis, sonst null. "Nicht angetreten" und "offen" kennen keine Punkte;
 * sonst gelten dieselben Regeln wie beim Ergebnis eines Ziels (pruefeErgebnis) - mit dem Prüfungstag
 * des Tages als Tag: Vor dem Prüfungstag gibt es kein Ergebnis.
 */
export function pruefeTeamErgebnis(
  t: Uebersetzer,
  art: PruefungErgebnis,
  eingabe: { punkte: string; notiz: string },
  pruefungstag: string,
  hoechstens: number | null | undefined,
  jetzt: number = Date.now(),
): string | null {
  if (art === PRUEFUNG_ERGEBNIS.Offen || art === PRUEFUNG_ERGEBNIS.NichtAngetreten) return null;
  return pruefeErgebnis(t, { datum: pruefungstag, punkte: eingabe.punkte, notiz: eingabe.notiz }, hoechstens, jetzt);
}

function normal(text: string): string {
  return text.toLocaleLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
}

/**
 * Die Mitglieder, die zur Suche passen - nur im Browser. Jedes Wort der Suche muss im Namen oder im Namen
 * eines Hundes vorkommen (Groß-/Kleinschreibung und Akzente egal); leer heißt: alle.
 */
export function filtereKandidaten(liste: readonly PruefungKandidat[], suche: string): PruefungKandidat[] {
  const worte = normal(suche).split(/\s+/).filter(Boolean);
  if (worte.length === 0) return [...liste];
  return liste.filter((k) => {
    const heu = normal([k.firstName, k.lastName, ...k.dogs.map((d) => d.name)].join(" "));
    return worte.every((w) => heu.includes(w));
  });
}

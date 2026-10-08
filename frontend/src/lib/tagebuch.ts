import type { DogTrackRun, TrainingSession } from "@/lib/types";
import { ortsformat } from "@/lib/ortsformat";
import type { Sprache } from "@/lib/i18n/sprachen";

/**
 * Das kompakte Tagebuch als reine Funktionen: die Zeile eines Trainingstags
 * (Datum, Kurzbild) und die Filter über der Liste.
 *
 * Ein Trainingstag ist eine Gruppe von Einheiten (Alt-Daten haben mehrere pro
 * Tag, siehe SessionHistory) - alles hier nimmt deshalb die ganze Gruppe.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

// ---- Tage ----

/**
 * Die Einheiten nach Trainingstag gruppiert, in der Reihenfolge der Liste (vom
 * Server, neueste zuerst). Alt-Daten haben mehrere Einheiten an einem Tag, das
 * Tagebuch zeigt aber EINEN Tag.
 */
export function nachTagen(sessions: readonly TrainingSession[] | null | undefined): Map<string, TrainingSession[]> {
  const tage = new Map<string, TrainingSession[]>();
  for (const s of sessions ?? []) {
    const einheiten = tage.get(s.date);
    if (einheiten) einheiten.push(s);
    else tage.set(s.date, [s]);
  }
  return tage;
}

/** Anzahl der Trainingstage (nicht der Einheiten). */
export function tageAnzahl(sessions: readonly TrainingSession[] | null | undefined): number {
  return nachTagen(sessions).size;
}

// ---- Datum ----

/**
 * "Fr 25.9." bzw. "Fri 25/09": das Datum der Tageszeile. Aus den Teilen des
 * Kalendertags gebaut und nicht aus `new Date("2026-09-25")` - das wäre
 * UTC-Mitternacht und fiele westlich von Greenwich auf den Vortag.
 */
export function tagKurz(iso: string, sprache: Sprache = "de"): string {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  if (!jahr || !monat || !tag) return iso;
  const teile = new Intl.DateTimeFormat(ortsformat(sprache), {
    weekday: "short",
    day: "numeric",
    month: "numeric",
  }).formatToParts(new Date(jahr, monat - 1, tag));
  const wert = (art: string) => teile.find((t) => t.type === art)?.value ?? "";
  const wochentag = wert("weekday").replace(/\.$/, "");
  return sprache === "en"
    ? `${wochentag} ${wert("day")}/${wert("month")}`
    : `${wochentag} ${wert("day")}.${wert("month")}.`;
}

// ---- Kurzbild ----

/**
 * Der Ablauf, der an diesem Tag ausgewertet wurde - der jüngste, wenn es
 * mehrere gibt. Die Auswertung kennt nur die zuletzt ausgewerteten Abläufe des
 * Hundes (siehe StatsService.GetDogTrackStatsAsync); ältere Tage haben keinen.
 */
export function laufAmTag(laeufe: readonly DogTrackRun[] | null | undefined, datum: string): DogTrackRun | null {
  const tag = datum.slice(0, 10);
  const amTag = (laeufe ?? []).filter((l) => l.date.slice(0, 10) === tag);
  return amTag.length > 0 ? amTag[amTag.length - 1] : null;
}

const NOTIZ_MAX = 60;

/**
 * Was eine Tageszeile über den Tag sagt, in einer Zeile:
 * "400 m · Ø 1 m · 98 % · Fußarbeit ★★★★ · +1".
 *
 * Die Fährte kommt zuerst, weil sie der Grund ist, einen Tag aufzuschlagen:
 * ihre Länge (ohne Länge das Wort "Fährte"), dann die Auswertung des Ablaufs
 * (Ø Abweichung, Anteil auf der Spur) und, solange es noch keine gibt, der
 * Untergrund. Danach die erste Übung mit ihren Sternen und die Zahl der
 * übrigen. Ohne beides die erste Zeile der Notiz, sonst der Ort - ein Tag ohne
 * Inhalt soll trotzdem etwas heißen.
 *
 * Länge und Untergrund liefert die Liste der Einheiten mit (trackLengthMeters,
 * trackSurface): ein Abruf je Fährte käme beim Öffnen der Seite einem
 * Request-Gewitter gleich. Die Auswertung kennt dagegen nur die zuletzt
 * ausgewerteten Abläufe, ältere Tage haben keine.
 */
export function tagesKurzbild(
  tag: readonly TrainingSession[],
  lauf: Pick<DogTrackRun, "avgDeviationMeters" | "onTrackPercent"> | null,
  t: Uebersetzer,
): string {
  const teile: string[] = [];

  if (tag.some((s) => s.hasGpsTrack)) {
    const laenge = tag.find((s) => s.trackLengthMeters)?.trackLengthMeters;
    teile.push(laenge ? t("{meter} m", { meter: Math.round(laenge) }) : t("Fährte"));
    if (lauf) {
      teile.push(
        t("Ø {meter} m · {prozent} %", {
          meter: Math.round(lauf.avgDeviationMeters),
          prozent: Math.round(lauf.onTrackPercent),
        }),
      );
    } else {
      const untergrund = tag.find((s) => s.trackSurface?.trim())?.trackSurface?.trim();
      // Der Untergrund ist eine Auswahl aus festen Wörtern (kommagetrennt), wie
      // in der Fährten-Karte - dort wird jedes einzeln übersetzt.
      if (untergrund) teile.push(untergrund.split(", ").map((teil) => t(teil)).join(", "));
    }
  }

  const uebungen = tag.flatMap((s) => s.exercises);
  if (uebungen.length > 0) {
    const erste = uebungen[0];
    const sterne = Math.min(Math.max(Math.round(erste.rating), 0), 5);
    teile.push(sterne > 0 ? `${erste.exerciseName} ${"★".repeat(sterne)}` : erste.exerciseName);
    if (uebungen.length > 1) teile.push(`+${uebungen.length - 1}`);
  }

  if (teile.length > 0) return teile.join(" · ");

  const notiz = tag
    .map((s) => s.notes?.trim().split("\n")[0])
    .find((zeile): zeile is string => !!zeile);
  if (notiz) return notiz.length > NOTIZ_MAX ? `${notiz.slice(0, NOTIZ_MAX - 1)}…` : notiz;

  const ort = tag.map((s) => s.locationName?.trim()).find((name): name is string => !!name);
  return ort ?? t("Training");
}

// ---- Filter ----

export type TagesFilter = "alle" | "faehrten" | "feedback";

/** Ob an diesem Tag eine Fährte liegt. */
export function hatFaehrte(tag: readonly TrainingSession[]): boolean {
  return tag.some((s) => s.hasGpsTrack);
}

/** Ob an diesem Tag Feedback einer Trainer:in liegt. */
export function hatFeedback(tag: readonly TrainingSession[]): boolean {
  return tag.some((s) => !!s.trainerFeedback);
}

export function tagPasstZuFilter(filter: TagesFilter, tag: readonly TrainingSession[]): boolean {
  if (filter === "faehrten") return hatFaehrte(tag);
  if (filter === "feedback") return hatFeedback(tag);
  return true;
}

/**
 * Welche Filter angeboten werden. "Fährten" und "Mit Feedback" nur, wenn es
 * solche Tage überhaupt gibt - ein Filter, der nichts findet, ist Rauschen. Gibt
 * es keinen der beiden, entfällt die ganze Leiste (leere Liste): "Alle" allein
 * wäre ein Knopf ohne Wirkung.
 */
export function verfuegbareFilter(tage: readonly (readonly TrainingSession[])[]): TagesFilter[] {
  const filter: TagesFilter[] = [];
  if (tage.some(hatFaehrte)) filter.push("faehrten");
  if (tage.some(hatFeedback)) filter.push("feedback");
  return filter.length > 0 ? ["alle", ...filter] : [];
}

/**
 * Der Filter, der gilt: der gewählte, solange er angeboten wird, sonst "alle".
 * Nach einem Löschen kann der letzte Tag der gewählten Art verschwinden - dann
 * soll die Liste nicht leer bleiben.
 */
export function geltenderFilter(gewaehlt: TagesFilter, angeboten: readonly TagesFilter[]): TagesFilter {
  return angeboten.includes(gewaehlt) ? gewaehlt : "alle";
}

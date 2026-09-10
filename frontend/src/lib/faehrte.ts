import type { GpsTrack, Sport } from "@/lib/types";

/**
 * Sportarten, bei denen eine Fährte gelegt wird. Codes statt Namen: Namen
 * werden umbenannt ("Leinenführigkeit" wurde "Fußarbeit"), Codes bleiben.
 */
export const FAEHRTEN_SPORTARTEN = ["FAERTE", "FPR", "IGP1", "IGP2", "IGP3", "GPR", "STOEPR"];

/**
 * Ob einem Hund das Fährtelegen angeboten wird.
 *
 * Solange keine Einschränkung gilt (noch nicht geladen oder leere Auswahl),
 * ja - sonst nur, wenn eine Fährten-Sportart ausdrücklich dabei ist. Von der
 * Hundeseite hierher gezogen, damit die Startseite dieselbe Antwort gibt: eine
 * Kachel "Fährte legen", die auf eine Hundeseite ohne Recorder führt, wäre
 * ein toter Link.
 */
export function laeuftFaehrte(dogSportIds: string[] | null, sports: Sport[]): boolean {
  return (
    dogSportIds === null ||
    dogSportIds.length === 0 ||
    sports.some((s) => dogSportIds.includes(s.id) && FAEHRTEN_SPORTARTEN.includes(s.code))
  );
}

export type OffeneFaehrte = {
  track: GpsTrack;
  /** Ende des Legens: letzter automatischer GPS-Punkt. */
  gelegtBis: Date;
  /** Fährtenalter in Minuten - die Größe, in der das Alter im Sport angegeben wird. */
  alterMinuten: number;
};

/**
 * Gelegte Fährten ohne Ablauf, in Legereihenfolge.
 *
 * Das Alter zählt ab dem Ende des Legens, nicht ab dem Beginn: ab da altert
 * der letzte Abschnitt, und bei zwanzig Minuten Legezeit ist der Unterschied
 * erheblich. Manuelle Marker zählen nicht - sie tragen unter Umständen einen
 * späteren Zeitstempel und würden das Alter verfälschen.
 */
export function nichtAbgelaufeneFaehrten(tracks: GpsTrack[], jetzt: number = Date.now()): OffeneFaehrte[] {
  return tracks
    .filter((track) => track.walkRuns.length === 0)
    .map((track) => {
      const automatisch = track.points.filter((p) => p.pointType !== 1);
      const letzter = automatisch[automatisch.length - 1];
      if (!letzter) return null;
      const gelegtBis = new Date(letzter.timestamp);
      return { track, gelegtBis, alterMinuten: Math.max(0, Math.round((jetzt - gelegtBis.getTime()) / 60000)) };
    })
    .filter((f): f is OffeneFaehrte => f !== null)
    .sort((a, b) => a.gelegtBis.getTime() - b.gelegtBis.getTime());
}

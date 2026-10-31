import { istMantrailing, MANTRAILING_CODE } from "@/lib/mantrailing";
import type { GpsTrack, Sport } from "@/lib/types";

/**
 * Sportarten, bei denen eine Spur aufgezeichnet wird: die Fährte und, seit
 * Paket M1, das Mantrailing (der Trail ist die Laufspur der Versteckperson).
 * Codes statt Namen: Namen werden umbenannt ("Leinenführigkeit" wurde
 * "Fußarbeit"), Codes bleiben. Dieselbe Liste führt das Backend
 * (Erinnerungsregeln.FaehrtenSportCodes) - beim Ändern beide Stellen anfassen.
 */
export const FAEHRTEN_SPORTARTEN = ["FAERTE", "FPR", "IGP1", "IGP2", "IGP3", "GPR", "STOEPR", MANTRAILING_CODE];

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

/** Welche Spuren ein Hund legen kann: Fährte, Trail (Mantrailing) oder beides. */
export type SpurArten = { faehrte: boolean; mantrailing: boolean };

/**
 * Fährte und Trail getrennt: Die Fährte wie in {@link laeuftFaehrte} - ohne
 * Einschränkung, oder mit einer Fährten-Sportart dabei. Der Trail nur, wenn
 * Mantrailing AUSDRÜCKLICH gewählt ist; bei leerer Auswahl ("alles") bleibt es
 * bei der Fährte, sonst bekäme jeder Hund einen Trail angeboten, der nie trailt.
 */
export function spurArten(dogSportIds: string[] | null, sports: Sport[]): SpurArten {
  const gewaehlt = sports.filter((s) => dogSportIds?.includes(s.id));
  const mantrailing = gewaehlt.some((s) => istMantrailing(s));
  const keineEinschraenkung = dogSportIds === null || dogSportIds.length === 0;
  const faehrte =
    keineEinschraenkung || gewaehlt.some((s) => !istMantrailing(s) && FAEHRTEN_SPORTARTEN.includes(s.code));
  return { faehrte, mantrailing };
}

export type OffeneFaehrte = {
  track: GpsTrack;
  /** Ende des Legens: letzter automatischer GPS-Punkt. */
  gelegtBis: Date;
  /** Fährtenalter in Minuten - die Größe, in der das Alter im Sport angegeben wird. */
  alterMinuten: number;
};

/**
 * Ende des Legens: der Zeitstempel des letzten automatischen GPS-Punkts.
 *
 * Manuelle Marker zählen nicht - sie tragen unter Umständen einen späteren
 * Zeitstempel und würden das Alter verfälschen. Null, wenn die Fährte keinen
 * automatischen Punkt (mit gültiger Zeit) hat.
 */
export function legeEnde(track: Pick<GpsTrack, "points">): Date | null {
  const automatisch = track.points.filter((p) => p.pointType !== 1);
  const letzter = automatisch[automatisch.length - 1];
  if (!letzter) return null;
  const ende = new Date(letzter.timestamp);
  return Number.isNaN(ende.getTime()) ? null : ende;
}

/**
 * Gelegte Fährten ohne Ablauf, in Legereihenfolge.
 *
 * Das Alter zählt ab dem Ende des Legens, nicht ab dem Beginn: ab da altert
 * der letzte Abschnitt, und bei zwanzig Minuten Legezeit ist der Unterschied
 * erheblich.
 */
export function nichtAbgelaufeneFaehrten(tracks: GpsTrack[], jetzt: number = Date.now()): OffeneFaehrte[] {
  return tracks
    .filter((track) => track.walkRuns.length === 0)
    .map((track) => {
      const gelegtBis = legeEnde(track);
      if (!gelegtBis) return null;
      return { track, gelegtBis, alterMinuten: Math.max(0, Math.round((jetzt - gelegtBis.getTime()) / 60000)) };
    })
    .filter((f): f is OffeneFaehrte => f !== null)
    .sort((a, b) => a.gelegtBis.getTime() - b.gelegtBis.getTime());
}

/** So lange nach dem Ende des Legens kann eine Fährte noch abgelaufen werden. */
export const ABLAUF_FRIST_STUNDEN = 24;

/**
 * Und ein Trail: Er liegt bis zu 24 Stunden (SV-MT 3, ca. 24 h; DVG MT-III mindestens 15 h),
 * danach folgt die Suche - mit 24 Stunden wäre er schon beim Start nicht mehr zu suchen.
 */
export const ABLAUF_FRIST_STUNDEN_TRAIL = 48;

/**
 * Ob eine Fährte (noch) abgelaufen werden kann: bis 24 Stunden nach dem Ende
 * des Legens - unabhängig vom Kalendertag.
 *
 * Abends gelegt und am nächsten Morgen gesucht ist im Fährtensport der
 * Normalfall (Übernachtfährte); ein Stichtag um Mitternacht würde genau das
 * sperren. Nach einem Tag ist die Fährte dagegen so alt, dass ein Ablauf kein
 * Fährtenalter mehr beschreibt, das sich vergleichen ließe. Ohne Zeitstempel
 * (Fährte ohne automatische Punkte) lässt sich die Frist nicht prüfen: nein.
 * Beim Trail gilt die längere Frist (siehe {@link ABLAUF_FRIST_STUNDEN_TRAIL}).
 */
export function kannNochAblaufen(track: Pick<GpsTrack, "points" | "kind">, jetzt: number = Date.now()): boolean {
  const ende = legeEnde(track);
  if (!ende) return false;
  const frist = istMantrailing(track) ? ABLAUF_FRIST_STUNDEN_TRAIL : ABLAUF_FRIST_STUNDEN;
  return jetzt - ende.getTime() <= frist * 60 * 60 * 1000;
}

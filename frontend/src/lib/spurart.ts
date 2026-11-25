/**
 * Die Art einer GPS-Aufzeichnung (GpsTrackKind im Backend, als Zahl): Fährte, Trail (Mantrailing) und
 * seit Paket SF1 die Schweißfährte der Jagd. Was welche Art ist, steht an GENAU einer Stelle: hier.
 *
 * Wichtig für jede Prüfung an einer Spur: "kein Mantrailing" heißt NICHT "Fährte" - dazwischen liegt die
 * Schweißfährte, die weder Gegenstände noch Material noch Schritte noch den Sport-Leitfaden kennt.
 * Wer eine Spur nach ihrer Art behandelt, fragt deshalb {@link spurArt}.
 */

/** Sportart-Code der Jagd (Paket JK1): Nur wer ihn ausdrücklich gewählt hat, bekommt die Schweißfährte angeboten. */
export const SCHWEISS_CODE = "JAGD";

/** GpsTrack.kind (GpsTrackKind im Backend, als Zahl). */
export const ART_FAEHRTE = 0;
export const ART_MANTRAILING = 1;
export const ART_SCHWEISS = 2;

export type SpurArtName = "faehrte" | "trail" | "schweiss";

/**
 * Ob etwas die Jagd bzw. eine Schweißfährte ist: ein Sportart-Code, eine Sportart oder eine
 * aufgezeichnete Spur (an `kind`). Lose Prüfung wie bei istMantrailing: aus einem älteren Read-Cache
 * fehlt `kind` ganz (undefined = Fährte).
 */
export function istSchweiss(wovon: string | { code: string } | { kind?: number | null } | null | undefined): boolean {
  if (wovon == null) return false;
  if (typeof wovon === "string") return wovon === SCHWEISS_CODE;
  if ("code" in wovon) return wovon.code === SCHWEISS_CODE;
  return wovon.kind === ART_SCHWEISS;
}

/** Die Art einer Spur; ohne Angabe (älterer Stand) die Fährte. */
export function spurArt(track: { kind?: number | null } | null | undefined): SpurArtName {
  if (track?.kind === ART_MANTRAILING) return "trail";
  if (track?.kind === ART_SCHWEISS) return "schweiss";
  return "faehrte";
}

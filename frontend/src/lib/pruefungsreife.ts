import type { Sprache } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { zahlText } from "@/lib/ortsformat";
import {
  REIFE_AMPEL,
  REIFE_GRUND,
  REIFE_ZUSTAND,
  type Pruefungsreife,
  type PruefungsreifeKurz,
  type PruefungsreifeTheorie,
  type PruefungsreifeUebung,
  type ReifeAmpel,
  type ReifeZustand,
} from "@/lib/types";

/**
 * Prüfungsreife je Ziel in Worten: Die Zahlen und Zustände rechnet der Server
 * (Application/Planning/Pruefungsreife.cs) - hier steht nur, wie sie als Satz,
 * Chip und Grund erscheinen. Es ist eine Faustregel aus dem Tagebuch, kein Urteil
 * der Richter:in; die Texte sagen das, wo es darauf ankommt.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** Das Wort zur Ampel, wie es im Chip steht. */
export const AMPEL_WORT: Record<ReifeAmpel, string> = {
  [REIFE_AMPEL.Gruen]: uebersetzbar("grün"),
  [REIFE_AMPEL.Gelb]: uebersetzbar("gelb"),
  [REIFE_AMPEL.Rot]: uebersetzbar("rot"),
};

/** Das Wort zum Zustand einer Übung - neben Form und Farbe, damit nichts nur an der Farbe hängt. */
export const ZUSTAND_WORT: Record<ReifeZustand, string> = {
  [REIFE_ZUSTAND.Offen]: uebersetzbar("offen"),
  [REIFE_ZUSTAND.Wackelig]: uebersetzbar("wackelig"),
  [REIFE_ZUSTAND.Sicher]: uebersetzbar("sicher"),
};

/**
 * Der Chip auf der Zielkarte: "Prüfungsreife: gelb · 2 offen, 3 wackelig".
 * Ist nichts offen und nichts wackelig, steht "alles sicher".
 */
export function chipText(t: Uebersetzer, kurz: Pick<PruefungsreifeKurz, "ampel" | "offen" | "wackelig">): string {
  const teile: string[] = [];
  if (kurz.offen > 0) teile.push(t("{n} offen", { n: kurz.offen }));
  if (kurz.wackelig > 0) teile.push(t("{n} wackelig", { n: kurz.wackelig }));
  if (teile.length === 0) teile.push(t("alles sicher"));
  return `${t("Prüfungsreife: {ampel}", { ampel: t(AMPEL_WORT[kurz.ampel]) })} · ${teile.join(", ")}`;
}

/**
 * Ein Satz zur Gesamtampel: "Fast so weit: 2 Übungen wackeln noch."
 *
 * Gelb kann auch heißen, dass die Übungen sitzen und nur die Theorie fehlt -
 * dann sagt der Satz das, statt von null wackeligen Übungen zu sprechen.
 */
export function ampelSatz(
  t: Uebersetzer,
  reife: Pick<Pruefungsreife, "ampel" | "offen" | "wackelig" | "theorie">,
): string {
  if (reife.ampel === REIFE_AMPEL.Gruen) {
    return reife.offen + reife.wackelig === 0 && (!reife.theorie || reife.theorie.zustand === REIFE_ZUSTAND.Sicher)
      ? t("Bereit: Alle Übungen sitzen sicher.")
      : t("Bereit: Fast alle Übungen sitzen sicher.");
  }

  if (reife.ampel === REIFE_AMPEL.Gelb) {
    if (reife.offen === 0 && reife.wackelig === 0) return t("Fast so weit: Die Übungen sitzen – jetzt fehlt noch die Theorie.");
    if (reife.offen === 0)
      return reife.wackelig === 1
        ? t("Fast so weit: 1 Übung wackelt noch.")
        : t("Fast so weit: {n} Übungen wackeln noch.", { n: reife.wackelig });
    return t("Auf dem Weg – offen: {offen}, wackelig: {wackelig}.", { offen: reife.offen, wackelig: reife.wackelig });
  }

  if (reife.offen > 0)
    return reife.offen === 1
      ? t("Noch nicht so weit: 1 Übung wurde noch nie trainiert.")
      : t("Noch nicht so weit: {n} Übungen wurden noch nie trainiert.", { n: reife.offen });
  return t("Noch nicht so weit: Die meisten Übungen sitzen noch nicht sicher.");
}

/** "Noch 5 Wochen bis zur Prüfung" - die Trainingswochen, Pausentage nicht mitgezählt. */
export function zeitText(t: Uebersetzer, reife: Pick<Pruefungsreife, "tage" | "wochen" | "pausenTage">): string {
  if (reife.tage <= 0) return t("Der Prüfungstag ist heute oder schon vorbei.");
  const zeit = reife.wochen === 1 ? t("Noch 1 Woche bis zur Prüfung") : t("Noch {n} Wochen bis zur Prüfung", { n: reife.wochen });
  return reife.pausenTage > 0 ? t("{zeit} (ohne {tage} Tage Pause)", { zeit, tage: reife.pausenTage }) : zeit;
}

/** "zuletzt vor 35 Tagen", "zuletzt gestern", "zuletzt heute" - mit dem Pausenanteil, wenn es einen gibt. */
export function zuletztText(t: Uebersetzer, tageSeit: number, pausenTage: number): string {
  const zeit =
    tageSeit <= 0 ? t("zuletzt heute") : tageSeit === 1 ? t("zuletzt gestern") : t("zuletzt vor {n} Tagen", { n: tageSeit });
  return pausenTage > 0 ? t("{zeit} (davon {tage} Tage Pause)", { zeit, tage: pausenTage }) : zeit;
}

/** Der Grund hinter dem Zustand einer Übung: "noch nie trainiert", "Bewertung im Schnitt 2,8", "zuletzt vor 35 Tagen". */
export function grundText(t: Uebersetzer, u: PruefungsreifeUebung, sprache: Sprache): string {
  if (u.zustand === REIFE_ZUSTAND.Offen || u.tageSeit === null) return t("noch nie trainiert");

  if (u.zustand === REIFE_ZUSTAND.Sicher) return zuletztText(t, u.tageSeit, u.pausenTage);

  switch (u.grund) {
    case REIFE_GRUND.SchwacheBewertung:
      return t("Bewertung im Schnitt {wert}", { wert: zahlText(u.bewertung ?? 0, sprache) });
    case REIFE_GRUND.LangeHer:
      return zuletztText(t, u.tageSeit, u.pausenTage);
    default:
      return t("noch nicht oft genug gut gelaufen");
  }
}

/** Die Theoriezeile als Satz (Sachkunde: Ergebnis der letzten Übungsprüfungen). */
export function theorieText(t: Uebersetzer, theorie: Pick<PruefungsreifeTheorie, "zustand" | "gewertet" | "bestanden">): string {
  if (theorie.zustand === REIFE_ZUSTAND.Offen) return t("Noch keine Übungsprüfung abgelegt");
  if (theorie.zustand === REIFE_ZUSTAND.Sicher) return t("Die letzten {n} Übungsprüfungen bestanden", { n: theorie.gewertet });
  return t("{bestanden} von den letzten {gesamt} als Übung bestanden", { bestanden: theorie.bestanden, gesamt: theorie.gewertet });
}

/** Wohin und mit welchem Wort die Theoriezeile führt: erst die Übungsprüfung, danach die falschen Fragen. */
export function theorieLink(t: Uebersetzer, theorie: PruefungsreifeTheorie): { href: string; text: string } {
  const href = `/lernen/${theorie.catalogCode.toLowerCase()}`;
  if (theorie.zustand === REIFE_ZUSTAND.Offen) return { href, text: t("Übungsprüfung machen") };
  if (theorie.inFehlerspeicher > 0) return { href, text: t("Falsche Fragen üben ({n})", { n: theorie.inFehlerspeicher }) };
  return { href, text: t("Neue Übungsprüfung") };
}

/** Teilt die Übungen in das, was noch fehlt (offen, wackelig), und das Sichere - die Reihenfolge des Servers bleibt. */
export function teileUebungen(uebungen: readonly PruefungsreifeUebung[]): {
  fehlend: PruefungsreifeUebung[];
  sicher: PruefungsreifeUebung[];
} {
  return {
    fehlend: uebungen.filter((u) => u.zustand !== REIFE_ZUSTAND.Sicher),
    sicher: uebungen.filter((u) => u.zustand === REIFE_ZUSTAND.Sicher),
  };
}

/** Die Regeln der Faustregel in Klartext - für das aufklappbare "Wie wird das berechnet?". Zahlen wie auf dem Server. */
export const REGELN = {
  sicherBox: 4,
  sicherBewertung: 4,
  sicherTage: 28,
  gruenProzent: 80,
  rotProzent: 50,
  verschiebenWochen: 3,
} as const;

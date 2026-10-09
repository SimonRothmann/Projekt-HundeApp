import type { DogGender, OnboardingStatus } from "@/lib/types";

/**
 * Was der Erststart auf der Startseite gerade zeigt - als reine Funktion, damit
 * die Regel ohne Browser prüfbar ist (Muster wie startseite.ts).
 *
 * Es gibt nur zwei Momente:
 * - **hund**: noch kein Hund. Die Karte "Wie heißt dein Hund?" legt ihn an und
 *   öffnet gleich danach das Eintragen-Fenster - ohne Seitenwechsel.
 * - **gabelung**: das erste Training ist gespeichert. EINMAL fragt die Karte
 *   "Gut gemacht! Wie geht es weiter?" nach den zwei Wegen (Prüfungsziel oder
 *   Verein).
 *
 * Alles dazwischen (Hund da, Training noch nicht) ist die normale Startseite
 * mit ihrer Eintragen-Karte: Wer das Fenster ohne Speichern schließt, soll
 * nicht von einem Leitfaden verfolgt werden.
 */
export type ErststartZustand = "hund" | "gabelung" | null;

/**
 * So lange nach dem ersten Training gilt die Gabelung. "Einmalig" heißt hier:
 * Sie steht, bis jemand einen der Wege geht oder sie wegklickt - aber nur kurz.
 * Wer sein erstes Training vor Monaten eingetragen hat (also jeder, der schon
 * vor dieser Karte dabei war), soll nicht plötzlich ein "Gut gemacht!" sehen.
 */
export const GABELUNG_TAGE = 7;

const TAG_MS = 24 * 60 * 60 * 1000;

export function erststartZustand(status: OnboardingStatus | null, jetzt: number = Date.now()): ErststartZustand {
  if (status === null || status.isDismissed) return null;

  if (!status.hasDog) return "hund";

  // Ziel gesetzt, Verein oder Gruppe angefragt/beigetreten: Die Gabelung hat
  // ihren Zweck erfüllt - einer der beiden Wege ist gegangen.
  const einWegGegangen =
    status.hasGoal ||
    status.hasClubMembership ||
    status.hasPendingClubRequest ||
    status.hasGroupMembership ||
    status.hasPendingGroupRequest;
  if (!status.hasTraining || einWegGegangen) return null;

  const erstes = status.firstTrainingAt ? Date.parse(status.firstTrainingAt) : NaN;
  if (Number.isNaN(erstes)) return null;
  // Eine Uhr, die minimal vorgeht, darf die Karte nicht verschwinden lassen.
  return jetzt - erstes <= GABELUNG_TAGE * TAG_MS ? "gabelung" : null;
}

/**
 * Prüft die Eingabe der Karte "Wie heißt dein Hund?" (und des Formulars auf der
 * Hundeliste): Name und Geschlecht sind Pflicht.
 *
 * Das Geschlecht hat bewusst KEINE Vorbelegung: Das Backend nimmt bei fehlender
 * Angabe still "Rüde" (die 0 des Enums), und eine Hündin würde so unbemerkt als
 * Rüde angelegt. Deshalb speichert das Frontend erst nach einer Auswahl.
 */
export type ErsterHund =
  | { ok: true; name: string; geschlecht: DogGender }
  | { ok: false; fehler: "name" | "geschlecht" };

export function pruefeErstenHund(name: string, geschlecht: DogGender | null): ErsterHund {
  const bereinigt = name.trim();
  if (bereinigt === "") return { ok: false, fehler: "name" };
  if (geschlecht === null) return { ok: false, fehler: "geschlecht" };
  return { ok: true, name: bereinigt, geschlecht };
}

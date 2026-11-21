import type { DogGender, OnboardingStatus, Regulation, Sport } from "@/lib/types";
import { einstiegsOrdnung } from "@/lib/einstieg";

/**
 * Was der Erststart auf der Startseite gerade zeigt - als reine Funktion, damit
 * die Regel ohne Browser prüfbar ist (Muster wie startseite.ts).
 *
 * Es gibt drei Momente:
 * - **hund**: noch kein Hund. Die Karte "Wie heißt dein Hund?" legt ihn an und
 *   öffnet gleich danach das Eintragen-Fenster - ohne Seitenwechsel.
 * - **erstesTraining**: Hund da, aber noch kein Training (egal ob schon in einer
 *   Gruppe). Eine ruhige Karte mit einem Hauptknopf "Erstes Training eintragen"
 *   und - wenn es passt - dem Vorschlag eines Ziels. Kein Leitfaden, der verfolgt:
 *   Sie lässt sich ausblenden und verschwindet mit dem ersten Training.
 * - **gabelung**: das erste Training ist gespeichert. EINMAL fragt die Karte
 *   "Gut gemacht! Wie geht es weiter?" nach den zwei Wegen (Prüfungsziel oder
 *   Verein).
 */
export type ErststartZustand = "hund" | "erstesTraining" | "gabelung" | null;

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

  // Vor dem isComplete-Entscheid: Wer schon in einer Gruppe ist (Erststart "erledigt"), aber noch nie selbst
  // ein Training eingetragen hat, ist genau der, den die Karte erreichen soll.
  if (!status.hasTraining) return "erstesTraining";

  // Ob der Erststart erledigt ist, entscheidet der Server (isComplete: Hund und Training oder
  // Gruppe, siehe docs/ERSTSTART.md) - keine zweite Auslegung hier. Das Frontend regelt nur noch,
  // wie lange danach die Gabelung steht (Weg gegangen, sieben Tage).
  if (!status.isComplete) return null;

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
 * Ob die Karte "Wie heißt dein Hund?" den Weg über den Verein anbietet ("Du hast
 * einen Einladungslink von deinem Verein?").
 *
 * Wer schon im Verein ist (auch als Trainer:in oder mit Vereinsrolle, aber ohne
 * Hund) oder die Anfrage schon gestellt hat, braucht ihn nicht: Der Link würde
 * zu etwas auffordern, das erledigt ist. Ist der Status unbekannt (Abruf
 * fehlgeschlagen), bleibt es beim Angebot wie bisher.
 */
export function zeigeVereinsweg(status: OnboardingStatus | null): boolean {
  return status === null || (!status.hasClubMembership && !status.hasPendingClubRequest);
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

/**
 * Der Hund der Karte "Erstes Training eintragen": der älteste (firstDogId des Servers), sonst der erste
 * nicht archivierte. Null ohne einen solchen.
 */
export function erstesTrainingHund<T extends { id: string; archivedAt?: string | null }>(
  status: OnboardingStatus,
  hunde: readonly T[],
): T | null {
  const aktive = hunde.filter((hund) => !hund.archivedAt);
  return aktive.find((hund) => hund.id === status.firstDogId) ?? aktive[0] ?? null;
}

/**
 * Das Ziel, das die Karte vorschlägt: die Einstiegsprüfung der ersten Profil-Sportart, für die es eine Ordnung gibt
 * (Reihenfolge der Auswahl im Profil). Null ohne gewählte Sportart oder ohne Ordnung - dann zeigt die Karte nur
 * das Eintragen. `ordnungen` sind die für die Person sichtbaren Ordnungen je Sportart-Id.
 */
export function zielvorschlag(
  profilSportIds: readonly string[],
  sportarten: readonly Sport[],
  ordnungen: Readonly<Record<string, readonly Regulation[]>>,
): { sport: Sport; ordnung: Regulation } | null {
  for (const id of profilSportIds) {
    const sport = sportarten.find((s) => s.id === id);
    const ordnung = sport ? einstiegsOrdnung(sport.code, ordnungen[id] ?? []) : null;
    if (sport && ordnung) return { sport, ordnung };
  }
  return null;
}

/** Wie weit das Ziel der Karte gedeiht: Zieldatum ist heute + so viele Tage (12 Wochen). */
export const ZIELVORSCHLAG_TAGE = 84;

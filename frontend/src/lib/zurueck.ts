import { internerPfad } from "@/lib/interner-pfad";
import { HUNDELISTE_IMMER, REITER_HERKUNFT } from "@/lib/hundeliste";

/**
 * Wohin "Zurück" auf einer Unterseite führt (siehe SubpageBackButton).
 *
 * Ein ?from= gewinnt, wenn es ein seiteninterner Pfad ist. Sonst die
 * übergeordnete Route - außer für Personen, die keine Trainer:innen sind:
 * Seiten unter /trainer (Anfragen, Verein) erreichen sie als
 * Mitgliederverwaltung oder Vorstand, die Trainer-Übersicht darüber zeigt
 * ihnen aber nichts. Sie landen bei den Vereinen. Solange nicht feststeht, ob
 * jemand Trainer:in ist (null), bleibt es bei der Route.
 *
 * Eine Hundeseite ohne ?from= führt auf die Hundeliste: /dogs selbst leitete
 * gleich wieder auf einen Hund weiter. Die Liste selbst (/dogs?liste=1) führt
 * zurück auf /dogs, also auf den zuletzt angesehenen Hund.
 */
export function zurueckZiel(pfad: string, from: string | null | undefined, istTrainer: boolean | null): string {
  const ausFrom = internerPfad(from);
  if (ausFrom) return ausFrom;
  if (pfad === "/dogs") return "/dogs";
  const parent = pfad.slice(0, pfad.lastIndexOf("/")) || "/dashboard";
  if (parent === "/dogs") return HUNDELISTE_IMMER;
  return parent === "/trainer" && istTrainer === false ? "/clubs" : parent;
}

/**
 * Ob dies die Hundeseite als Startpunkt des Reiters "Hunde" ist (der Reiter hat
 * sie mit ?from=/dogs geöffnet): Sie ist dann wie eine Hauptseite und hat kein
 * "Zurück". Kam man von woanders (Startseite, Statistik, Gruppe), bleibt es.
 */
export function istHundeStartseite(pfad: string, from: string | null | undefined): boolean {
  return /^\/dogs\/[^/]+$/.test(pfad) && internerPfad(from) === REITER_HERKUNFT;
}

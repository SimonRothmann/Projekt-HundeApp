import { internerPfad } from "@/lib/interner-pfad";

/**
 * Wohin "Zurück" auf einer Unterseite führt (siehe SubpageBackButton).
 *
 * Ein ?from= gewinnt, wenn es ein seiteninterner Pfad ist. Sonst die
 * übergeordnete Route - außer für Personen, die keine Trainer:innen sind:
 * Seiten unter /trainer (Anfragen, Verein) erreichen sie als
 * Mitgliederverwaltung oder Vorstand, die Trainer-Übersicht darüber zeigt
 * ihnen aber nichts. Sie landen bei den Vereinen. Solange nicht feststeht, ob
 * jemand Trainer:in ist (null), bleibt es bei der Route.
 */
export function zurueckZiel(pfad: string, from: string | null | undefined, istTrainer: boolean | null): string {
  const ausFrom = internerPfad(from);
  if (ausFrom) return ausFrom;
  const parent = pfad.slice(0, pfad.lastIndexOf("/")) || "/dashboard";
  return parent === "/trainer" && istTrainer === false ? "/clubs" : parent;
}

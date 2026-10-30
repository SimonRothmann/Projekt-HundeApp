import type { ClubOverviewMember } from "@/lib/types";

/**
 * Reine Hilfen für die Vereinsübersicht (Vorstand, Verwaltung) - ohne
 * Bildschirm prüfbar. Die Zahlen kommen fertig vom Server
 * (siehe ClubOverviewService); hier steht nur, was der Browser zeigt.
 */

/** Der Name zur Anzeige; leer, wenn das Konto nicht mehr existiert (dann setzt die Oberfläche einen Platzhalter). */
export function mitgliedName(m: Pick<ClubOverviewMember, "firstName" | "lastName">): string {
  return `${m.firstName} ${m.lastName}`.trim();
}

function normal(text: string): string {
  return text.toLocaleLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
}

/**
 * Die Mitglieder, die zur Suche passen - nur im Browser, ohne weitere
 * Abfrage. Jedes Wort der Suche muss in Name oder einem Gruppennamen
 * vorkommen (Groß-/Kleinschreibung und Akzente egal, "Müller" findet "muller");
 * leer heißt: alle. Die Reihenfolge der Liste bleibt.
 */
export function filtereMitglieder(liste: readonly ClubOverviewMember[], suche: string): ClubOverviewMember[] {
  const worte = normal(suche).split(/\s+/).filter(Boolean);
  if (worte.length === 0) return [...liste];
  return liste.filter((m) => {
    const heu = normal([mitgliedName(m), ...m.groups].join(" "));
    return worte.every((w) => heu.includes(w));
  });
}

/** Der Monat zur Anzeige, kurz: "Okt. 2026" bzw. "Oct 2026". */
export function monatsLabel(jahr: number, monat: number, ortsformat: string): string {
  return new Date(jahr, monat - 1, 1).toLocaleDateString(ortsformat, { month: "short", year: "numeric" });
}

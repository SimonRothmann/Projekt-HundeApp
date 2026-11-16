/**
 * Beschriftung der Zeitachse unter dem Wochendiagramm der Statistik: nur jede
 * dritte Woche (von der jüngsten aus gezählt) bekommt die Kalenderwoche, damit
 * die Zahlen bei 375 px nicht ineinanderlaufen. `woche` kommt vom Server als
 * "2026-KW46"; null heißt "keine Beschriftung".
 */
export function wochenBeschriftungen(wochen: readonly { week: string }[], schritt = 3): (number | null)[] {
  return wochen.map((w, i) => {
    if ((wochen.length - 1 - i) % schritt !== 0) return null;
    const nummer = Number(w.week.slice(-2));
    return Number.isInteger(nummer) && nummer > 0 ? nummer : null;
  });
}

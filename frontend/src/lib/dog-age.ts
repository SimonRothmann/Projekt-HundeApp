/**
 * Alter eines Hundes aus dem Geburtsdatum.
 *
 * Bewusst in Monaten, solange der Hund unter zwei Jahren ist: die
 * Zulassungsgrenzen im Hundesport sind in Monaten formuliert (BH ab 15,
 * IAD ab 16, IGP 3 ab 20 Monaten). "1 Jahr" hilft da niemandem weiter,
 * "17 Monate" beantwortet die Frage sofort.
 */

/** Volle Lebensmonate. Null, wenn kein oder ein künftiges Datum vorliegt. */
export function dogAgeInMonths(birthday: string | null | undefined, today = new Date()): number | null {
  if (!birthday) return null;

  // Bewusst die Datumsteile zerlegen statt Date-Differenzen zu rechnen:
  // "2024-03-15" wird als UTC-Mitternacht gelesen, der Vergleich liefe
  // sonst je nach Zeitzone um einen Tag daneben.
  const parts = birthday.slice(0, 10).split("-").map(Number);
  if (parts.length !== 3 || parts.some(Number.isNaN)) return null;
  const [year, month, day] = parts;

  let months = (today.getFullYear() - year) * 12 + (today.getMonth() + 1 - month);
  // Der Monat ist erst voll, wenn der Tag im Monat erreicht ist.
  if (today.getDate() < day) months -= 1;

  return months < 0 ? null : months;
}

/** Anzeigetext, z.B. "7 Monate", "1 Jahr 5 Monate", "3 Jahre". */
export function formatDogAge(birthday: string | null | undefined, today = new Date()): string | null {
  const months = dogAgeInMonths(birthday, today);
  if (months === null) return null;

  if (months < 12) return months === 1 ? "1 Monat" : `${months} Monate`;

  const years = Math.floor(months / 12);
  const rest = months % 12;

  // Ab zwei Jahren nur noch Jahre - da zählt der Monat nicht mehr.
  if (years >= 2) return `${years} Jahre`;
  if (rest === 0) return "1 Jahr";
  return `1 Jahr ${rest === 1 ? "1 Monat" : `${rest} Monate`}`;
}

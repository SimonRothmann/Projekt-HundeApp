/**
 * Wann der Ungelesen-Zähler der Glocke beim Zurückkehren in die App neu
 * abgerufen wird.
 *
 * Der 60-Sekunden-Takt bleibt. Er hilft aber nicht, wenn die App im
 * Hintergrund lag: Der Browser bremst Zeitgeber in versteckten Tabs, und wer
 * das Handy nach einer Stunde entsperrt, sähe die Glocke noch im alten Stand.
 * Deshalb zusätzlich ein Abruf beim Sichtbarwerden - aber nicht öfter als alle
 * 10 Sekunden, damit schnelles Hin- und Herwechseln die Schnittstelle nicht
 * mit Abrufen überhäuft.
 */
export const RUECKKEHR_MINDESTABSTAND_MS = 10_000;

export function abrufFaellig(
  letzterAbruf: number,
  jetzt: number,
  mindestabstandMs: number = RUECKKEHR_MINDESTABSTAND_MS,
): boolean {
  return jetzt - letzterAbruf >= mindestabstandMs;
}

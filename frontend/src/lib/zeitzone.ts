/**
 * Die Zeitzone des Geräts als IANA-Kennung ("Europe/London"); null, wenn der Browser sie nicht
 * nennt. Der Server rechnet "heute", Wochen und Erinnerungszeiten der Person in dieser Zone (Ortszeit).
 */
export function geraeteZeitzone(): string | null {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
  } catch {
    return null;
  }
}

/** Ob die gespeicherte Zone gemeldet werden muss: Das Gerät nennt eine, und sie weicht vom Gespeicherten ab. */
export function mussMelden(gespeichert: string | null | undefined, geraet: string | null): geraet is string {
  return geraet !== null && geraet !== (gespeichert ?? null);
}

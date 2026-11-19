import type { Regulation } from "@/lib/types";

/**
 * Die Prüfungsordnung, mit der man in einer Sportart einsteigt (Stufe 1): ihre
 * Übungen bietet das Eintragen-Fenster beim allerersten Training an.
 *
 * Feste Vorzugsliste je Sport-Code (Vergleich über `seedKey`, der dem Namen der
 * Katalogordnung entspricht); die erste vorhandene gewinnt. `regulations` sind
 * die für die Person sichtbaren Ordnungen (nach Land gefiltert) - Österreich
 * sieht so auch die ÖKV-Fassung der BH, Deutschland nur "BH". Ohne Treffer
 * (oder ohne Eintrag für den Code) gilt die erste Ordnung der Liste: Bei den
 * übrigen Sportarten ist das Stufe 1 bzw. die einzige.
 */
const EINSTIEG: Record<string, readonly string[]> = {
  AGILITY: ["Agility 0 (A0)", "ÖKV Agility LK 1 (A 1)", "SKG Agility Klasse 1"],
  BH: ["ÖKV BH/VT (Mensch-Hund-Team-Prüfung)", "BH"],
  BHCH: ["SKG BH 1"],
  BSP: ["ÖKV Breitensport VK 1"],
  FAERTE: ["FCI-IFH 1"],
  HOOPERS: ["Hoopers 0 (H0)", "ÖKV Hoopers 1 (H1)"],
  MANTRAILING: ["DVG MT-I", "SV-MT 1"],
  OBEDIENCE: ["VDH Obedience Beginner (O-B)", "ÖKV Obedience Beginner", "SKG Obedience Beginners", "FCI-Obedience 1"],
  RALLYO: ["VDH Rally Obedience Beginner (RO B)", "ÖKV Rally Obedience Beginner (RO B)", "FCI-Rally Obedience International"],
  THS: ["VDH-VK1"],
};

export function einstiegsOrdnung(sportCode: string, regulations: readonly Regulation[]): Regulation | null {
  for (const name of EINSTIEG[sportCode] ?? []) {
    const treffer = regulations.find((r) => r.seedKey === name);
    if (treffer) return treffer;
  }
  return regulations[0] ?? null;
}

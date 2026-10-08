import { MODULE, type Country } from "@/lib/types";
import type { useT } from "@/lib/i18n";
import { uebersetzbar, SPRACHE_NAME, type Sprache } from "@/lib/i18n/sprachen";
import { VORGABE_LAND } from "@/lib/i18n/laender";
import { SCHRIFT_NAME, type Schriftgroesse } from "@/lib/schriftgroesse";

/**
 * Was die Einstellungsliste im Profil rechts neben den Zeilen als Vorschau
 * zeigt - als reine Funktionen, damit sich die Texte ohne Bildschirm prüfen
 * lassen. Der Übersetzer kommt von außen: Der Aufrufer weiß, in welcher
 * Sprache gerade gerendert wird.
 */
type Uebersetzer = ReturnType<typeof useT>;

/**
 * Die abschaltbaren Module mit ihren Texten.
 *
 * Module werden ABGEWÄHLT (siehe docs/VERBAENDE_SPRACHEN_MODULE.md): So
 * erscheint ein künftig hinzukommendes Modul bei allen von selbst, ohne dass
 * jemand etwas anfassen muss.
 */
export const MODUL_TEXTE: { key: string; titel: string; beschreibung: string; nurTrainer?: boolean }[] = [
  {
    key: MODULE.faehrte,
    titel: uebersetzbar("Fährte & GPS"),
    beschreibung: uebersetzbar("Fährten aufzeichnen, ablaufen und auswerten."),
  },
  {
    key: MODULE.sachkunde,
    titel: uebersetzbar("Sachkunde"),
    beschreibung: uebersetzbar("Fragentrainer zur Begleithundeprüfung (SWHV, deutschsprachig)."),
  },
  {
    key: MODULE.gruppentraining,
    titel: uebersetzbar("Gruppentraining"),
    beschreibung: uebersetzbar("Einheiten und Terminplanung für Trainingsgruppen."),
    // Nur Trainer:innen sehen diesen Bereich überhaupt. Allen anderen einen
    // Schalter für etwas anzubieten, das sie nie hatten, verwirrt nur.
    nurTrainer: true,
  },
  { key: MODULE.wetter, titel: uebersetzbar("Wetter"), beschreibung: uebersetzbar("Temperatur und Wetter zum Training.") },
  { key: MODULE.statistik, titel: uebersetzbar("Statistik"), beschreibung: uebersetzbar("Auswertungen über Trainings und Verfassung.") },
];

/** Die Module, die diese Person überhaupt zu sehen bekommt. */
export function sichtbareModule(istTrainer: boolean | null) {
  return MODUL_TEXTE.filter((m) => !m.nurTrainer || istTrainer);
}

/** "4 von 5 an" - gezählt wird nur, was die Person auch in der Liste sieht. */
export function vorschauFunktionen(abgewaehlt: readonly string[], istTrainer: boolean | null, t: Uebersetzer): string {
  const sichtbar = sichtbareModule(istTrainer);
  const an = sichtbar.filter((m) => !abgewaehlt.includes(m.key)).length;
  return t("{an} von {gesamt} an", { an, gesamt: sichtbar.length });
}

/**
 * Die gewählten Sportarten als kurze Aufzählung. Nichts gewählt heißt "alle"
 * (siehe SportartenSection): Das ist die Aussage der Einstellung, nicht
 * ein leerer Wert.
 */
export function vorschauSportarten(
  sportIds: readonly string[],
  sportarten: readonly { id: string; name: string }[] | null,
  t: Uebersetzer,
): string {
  if (sportIds.length === 0) return t("alle");
  // Die Liste ist noch unterwegs: lieber keine Vorschau als eine falsche.
  if (sportarten === null) return "";

  const namen = sportarten.filter((s) => sportIds.includes(s.id)).map((s) => s.name);
  // Gewählte Sportarten, die es nicht mehr gibt: Die Zahl stimmt trotzdem.
  if (namen.length === 0) return t("{n} gewählt", { n: sportIds.length });
  if (namen.length <= 2) return namen.join(", ");
  return t("{namen} +{n}", { namen: namen.slice(0, 2).join(", "), n: namen.length - 2 });
}

/** Name des Farbschemas. Alles außer "light" ist dunkel: Dunkel ist die Vorgabe der App. */
export function themeName(aufgeloest: string | undefined, t: Uebersetzer): string {
  return aufgeloest === "light" ? t("Hell") : t("Dunkel");
}

/** "Dunkel · Normal · Deutsch" */
export function vorschauDarstellung(
  aufgeloestesTheme: string | undefined,
  schrift: Schriftgroesse,
  sprache: Sprache,
  t: Uebersetzer,
): string {
  return [themeName(aufgeloestesTheme, t), t(SCHRIFT_NAME[schrift]), SPRACHE_NAME[sprache]].join(" · ");
}

/**
 * Ob die Länderauswahl überhaupt angeboten wird.
 *
 * Eine Wahl zwischen einem Land mit Inhalt und dreizehn leeren ist keine:
 * Sie sähe aus wie ein Defekt. Erst wenn mindestens zwei Länder
 * Prüfungsordnungen haben, lohnt sich die Auswahl.
 *
 * Ausnahme: Hat jemand früher ein anderes Land gespeichert, bleibt die Liste
 * sichtbar - sonst käme die Person nie mehr zurück in den deutschen
 * Geltungsbereich, und mit ihm fehlte ihr die Sachkunde. Der gespeicherte
 * Wert bleibt in jedem Fall unangetastet.
 */
export function zeigeLaenderwahl(laender: readonly Country[], gewaehltesLand: string): boolean {
  const mitInhalt = laender.filter((l) => l.regulationCount > 0).length;
  return mitInhalt >= 2 || gewaehltesLand !== VORGABE_LAND;
}

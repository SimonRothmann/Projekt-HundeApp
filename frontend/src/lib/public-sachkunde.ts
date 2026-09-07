import type { QuizCatalog, QuizQuestion } from "@/lib/types";

/**
 * Lesender Zugriff auf die Sachkunde-Fragenkataloge OHNE Login.
 *
 * Wie beim Prüfungsordnungskatalog (lib/public-catalog.ts): das Backend gibt
 * diese Stammdaten anonym heraus, sie enthalten nichts Personenbezogenes und
 * lassen sich deshalb serverseitig rendern.
 *
 * Der Grund ist hier aber noch handfester: Die Sachkunde lernt man Wochen
 * bevor man ein Trainingstagebuch braucht. Wer "Sachkunde Begleithundeprüfung
 * Fragen" sucht, hat noch keinen Zugang zur App - und soll trotzdem üben
 * können.
 */

const API = process.env.NEXT_PUBLIC_API_URL || "https://api.dogity.net";

/** Die Kataloge ändern sich, wenn der Verband eine neue Fassung herausgibt. */
const REVALIDATE_SECONDS = 86_400;

export async function getQuizCatalogs(): Promise<QuizCatalog[]> {
  try {
    const antwort = await fetch(`${API}/api/sachkunde/catalogs`, {
      next: { revalidate: REVALIDATE_SECONDS },
    });
    if (!antwort.ok) return [];
    return (await antwort.json()) as QuizCatalog[];
  } catch {
    // Eine öffentliche Seite darf am Ausfall des Backends nicht zerbrechen -
    // sie zeigt dann einen Hinweis statt eines Fehlers (siehe Seite).
    return [];
  }
}

export async function getQuizCatalog(code: string): Promise<QuizCatalog | null> {
  const alle = await getQuizCatalogs();
  return alle.find((k) => k.code.toLowerCase() === code.toLowerCase()) ?? null;
}

/** Ein Abschnitt des Fragenkatalogs samt seiner Fragen. */
export type Fragenabschnitt = {
  key: string;
  name: string;
  fragen: QuizQuestion[];
};

/**
 * Bündelt die Fragen nach ihrem Abschnitt.
 *
 * Die Reihenfolge ist die des Katalogs, nicht die des Alphabets: die
 * Abschnitte A bis E folgen der Prüfungsvorlage des Verbands, und wer eine
 * Frage im Original nachschlagen will, sucht sie an derselben Stelle.
 */
export function nachAbschnitten(fragen: QuizQuestion[]): Fragenabschnitt[] {
  const abschnitte: Fragenabschnitt[] = [];
  for (const frage of fragen) {
    const vorhanden = abschnitte.find((abschnitt) => abschnitt.key === frage.section);
    if (vorhanden) vorhanden.fragen.push(frage);
    else abschnitte.push({ key: frage.section, name: frage.sectionName, fragen: [frage] });
  }
  return abschnitte;
}

export async function getQuizQuestions(code: string): Promise<QuizQuestion[]> {
  try {
    const antwort = await fetch(`${API}/api/sachkunde/catalogs/${encodeURIComponent(code)}/questions`, {
      next: { revalidate: REVALIDATE_SECONDS },
    });
    if (!antwort.ok) return [];
    return (await antwort.json()) as QuizQuestion[];
  } catch {
    return [];
  }
}

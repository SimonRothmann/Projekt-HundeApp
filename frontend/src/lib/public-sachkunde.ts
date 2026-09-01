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

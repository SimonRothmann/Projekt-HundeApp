import { slugify } from "@/lib/seo";

/**
 * Lesender Zugriff auf den Sportarten- und Prüfungsordnungskatalog OHNE Login.
 *
 * Das Backend gibt diese Stammdaten bereits anonym heraus (siehe
 * SportsController, [AllowAnonymous]) - sie enthalten nichts Personenbezogenes.
 * Genau deshalb lassen sie sich serverseitig rendern und von Suchmaschinen
 * lesen, und genau danach wird gesucht ("IGP 1 Prüfungsordnung",
 * "Begleithundeprüfung Ablauf").
 *
 * Bewusst NICHT über lib/api.ts: der Client dort liest den Token aus
 * localStorage und läuft nur im Browser.
 */

// Zur Bauzeit eingesetzt (siehe Dockerfile, NEXT_PUBLIC_API_URL). Der Rückfall
// zeigt auf die Produktions-API - der Katalog ist überall derselbe, und eine
// öffentliche Seite ohne Inhalt wäre schlimmer als Daten aus Prod.
const API = process.env.NEXT_PUBLIC_API_URL || "https://api.dogity.net";

/** Einmal am Tag neu holen. Prüfungsordnungen ändern sich jährlich, nicht stündlich. */
const REVALIDATE_SECONDS = 86_400;

export type Sport = { id: string; name: string; description: string | null };

export type Regulation = {
  id: string;
  name: string;
  description: string | null;
  latestKnownVersionLabel: string | null;
  sourceUrl: string | null;
};

export type RegulationExercise = {
  exerciseId: string;
  exerciseName: string;
  isMandatory: boolean;
  maxPoints: number;
  scoringNotes: string | null;
};

export type RegulationDetail = {
  regulation: Regulation;
  currentVersion: { versionLabel: string | null; validFrom: string | null } | null;
  exercises: RegulationExercise[];
};

/** Ein Eintrag der öffentlichen Übersicht - Prüfungsordnung samt Sportart. */
export type CatalogEntry = {
  slug: string;
  sport: Sport;
  regulation: Regulation;
};

async function getJson<T>(path: string): Promise<T | null> {
  try {
    const response = await fetch(`${API}${path}`, {
      next: { revalidate: REVALIDATE_SECONDS },
      headers: { Accept: "application/json" },
    });
    if (!response.ok) return null;
    return (await response.json()) as T;
  } catch {
    // Eine öffentliche Seite darf nicht mit einem Fehler antworten, nur weil
    // das Backend gerade klemmt - der Aufrufer zeigt dann weniger an.
    return null;
  }
}

export async function getSports(): Promise<Sport[]> {
  return (await getJson<Sport[]>("/api/sports")) ?? [];
}

export async function getRegulations(sportId: string): Promise<Regulation[]> {
  return (await getJson<Regulation[]>(`/api/sports/${sportId}/regulations`)) ?? [];
}

export async function getRegulationDetail(regulationId: string): Promise<RegulationDetail | null> {
  return getJson<RegulationDetail>(`/api/sports/regulations/${regulationId}`);
}

/**
 * Alle Prüfungsordnungen mit ihrer Sportart und einem sprechenden Bezeichner.
 *
 * Der Bezeichner kommt aus dem Namen, nicht aus der ID: "/pruefungsordnung/igp-1"
 * sagt Mensch und Suchmaschine etwas, eine GUID nicht. Kollidieren zwei Namen,
 * gewinnt der erste und der zweite bekommt seine ID angehängt - lieber eine
 * hässliche Adresse als zwei Seiten, die sich gegenseitig überschreiben.
 */
export async function getCatalog(): Promise<CatalogEntry[]> {
  const sports = await getSports();
  const entries: CatalogEntry[] = [];
  const used = new Set<string>();

  for (const sport of sports) {
    for (const regulation of await getRegulations(sport.id)) {
      let slug = slugify(regulation.name);
      if (!slug || used.has(slug)) slug = `${slug}-${regulation.id.slice(0, 8)}`;
      used.add(slug);
      entries.push({ slug, sport, regulation });
    }
  }

  return entries;
}

export async function findCatalogEntry(slug: string): Promise<CatalogEntry | null> {
  return (await getCatalog()).find((entry) => entry.slug === slug) ?? null;
}

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

/**
 * Prüfungsfamilie, unter der ein Eintrag in der Übersicht steht.
 *
 * Ohne das zerfällt die Seite: "Internationale Begleithundeprüfung 1/2/3" sind
 * im Backend DREI getrennte Sportarten mit je einer Prüfungsordnung, die
 * Übersicht zeigte deshalb vierzehn Überschriften mit oft nur einem Eintrag.
 * Wer IBGH sucht, will die drei Stufen beieinander sehen.
 */
export type CatalogFamily = {
  key: string;
  title: string;
  description: string;
  entries: CatalogEntry[];
};

/** Reihenfolge = Reihenfolge auf der Seite; zuerst passende Regel gewinnt. */
const FAMILIES: { key: string; title: string; description: string; matches: (name: string) => boolean }[] = [
  {
    key: "bh",
    title: "BH – Begleithundeprüfung",
    description: "Die Einstiegsprüfung. Voraussetzung für fast alles Weitere.",
    matches: (name) => name === "BH",
  },
  {
    key: "ibgh",
    title: "IBGH – Internationale Begleithundeprüfung",
    description: "Reine Unterordnung in drei Stufen, ohne Fährte und ohne Schutzdienst.",
    matches: (name) => name.startsWith("IBGH"),
  },
  {
    key: "igp",
    title: "IGP – Internationale Gebrauchshundeprüfung",
    description: "Die Vollprüfung aus Fährte, Unterordnung und Schutzdienst, je 100 Punkte.",
    matches: (name) => /^FCI-IGP [123]$/.test(name),
  },
  {
    key: "faehrte",
    title: "Fährtenarbeit",
    description: "Die Fährten der IGP sowie die eigenständigen Fährtenhundprüfungen.",
    matches: (name) => name.includes("Fährte") || name.startsWith("FCI-IFH") || name === "FCI-IGP FH",
  },
  {
    key: "einzel",
    title: "Einzelprüfungen",
    description: "Einzelne Abteilungen der IGP, getrennt geprüft – ohne eigenes Ausbildungskennzeichen.",
    matches: (name) => /^FCI-(FPr|UPr|GPr|SPr|StöPr) /.test(name),
  },
  {
    key: "ausdauer",
    title: "Ausdauer",
    description: "Nachweis der körperlichen Belastbarkeit, ohne Punktewertung.",
    matches: (name) => name.startsWith("FCI-IAD"),
  },
];

/** Bündelt den Katalog in Familien. Alles ohne Treffer landet unter "Weitere". */
export function groupIntoFamilies(catalog: CatalogEntry[]): CatalogFamily[] {
  const buckets = new Map<string, CatalogEntry[]>();
  const rest: CatalogEntry[] = [];

  for (const entry of catalog) {
    const family = FAMILIES.find((f) => f.matches(entry.regulation.name));
    if (!family) {
      rest.push(entry);
      continue;
    }
    buckets.set(family.key, [...(buckets.get(family.key) ?? []), entry]);
  }

  const result: CatalogFamily[] = FAMILIES.filter((f) => buckets.has(f.key)).map((f) => ({
    key: f.key,
    title: f.title,
    description: f.description,
    // Innerhalb einer Familie nach Namen sortieren, damit Stufe 1 vor 2 vor 3 steht.
    entries: [...(buckets.get(f.key) ?? [])].sort((a, b) =>
      a.regulation.name.localeCompare(b.regulation.name, "de"),
    ),
  }));

  if (rest.length > 0) {
    result.push({ key: "weitere", title: "Weitere", description: "", entries: rest });
  }

  return result;
}

import type { Metadata } from "next";
import Link from "next/link";
import { getCatalog, type CatalogEntry } from "@/lib/public-catalog";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { absoluteUrl } from "@/lib/seo";

export const metadata: Metadata = {
  title: "Prüfungsordnungen im Hundesport – BH, IBGH, IGP und mehr",
  description:
    "Übersicht der Prüfungsordnungen im Gebrauchshundesport: Begleithundeprüfung, IBGH 1-3, IGP 1-3 sowie FPr, GPr, SPr, StöPr und UPr mit Übungen und Punkten.",
  alternates: { canonical: "/pruefungsordnungen" },
};

/** Nach Sportart bündeln, damit die Liste einer Gliederung folgt statt alphabetisch zu zerfallen. */
function groupBySport(catalog: CatalogEntry[]) {
  const groups = new Map<string, { sportName: string; entries: CatalogEntry[] }>();
  for (const entry of catalog) {
    const group = groups.get(entry.sport.id) ?? { sportName: entry.sport.name, entries: [] };
    group.entries.push(entry);
    groups.set(entry.sport.id, group);
  }
  return [...groups.values()];
}

export default async function RegulationsIndexPage() {
  const catalog = await getCatalog();
  const groups = groupBySport(catalog);

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    name: "Prüfungsordnungen im Hundesport",
    url: absoluteUrl("/pruefungsordnungen"),
    inLanguage: "de",
    hasPart: catalog.map((entry) => ({
      "@type": "WebPage",
      name: entry.regulation.name,
      url: absoluteUrl(`/pruefungsordnungen/${entry.slug}`),
    })),
  };

  return (
    <div className="flex min-h-full min-w-0 flex-col">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }}
      />
      <MarketingHeader />

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-10">
        <h1 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">
          Prüfungsordnungen im Hundesport
        </h1>
        <p className="mt-4 max-w-2xl text-sm text-muted-foreground sm:text-base">
          Die Prüfungen des Gebrauchshundesports mit ihren Übungen und Punkten. In Dogity dienen sie als Grundlage für
          Ziele und Trainingsplanung – hier stehen sie zum Nachschlagen offen.
        </p>

        {groups.length === 0 ? (
          <p className="mt-10 text-sm text-muted-foreground">
            Die Übersicht ist gerade nicht abrufbar. Bitte später erneut versuchen.
          </p>
        ) : (
          <div className="mt-10 flex flex-col gap-9">
            {groups.map((group) => (
              <section key={group.sportName} className="min-w-0">
                <h2 className="text-xl font-bold tracking-tight">{group.sportName}</h2>
                <ul className="mt-3 flex flex-col gap-2">
                  {group.entries.map((entry) => (
                    <li key={entry.slug} className="min-w-0">
                      <Link
                        href={`/pruefungsordnungen/${entry.slug}`}
                        className="block rounded-lg border border-border/60 px-3 py-2.5 transition-colors hover:bg-muted coarse:min-h-11"
                      >
                        <span className="block font-medium [overflow-wrap:anywhere]">{entry.regulation.name}</span>
                        {entry.regulation.description && (
                          <span className="mt-0.5 block text-xs text-muted-foreground">
                            {entry.regulation.description.split("\n")[0]}
                          </span>
                        )}
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
        )}
      </main>

      <MarketingFooter />
    </div>
  );
}

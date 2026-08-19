import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { findCatalogEntry, getCatalog, getRegulationDetail } from "@/lib/public-catalog";
import { absoluteUrl } from "@/lib/seo";

type Params = { params: Promise<{ slug: string }> };

/**
 * Erzeugt die Seiten beim Bauen vor. Damit steht der Inhalt sofort im HTML,
 * ohne dass beim ersten Aufruf erst das Backend befragt werden muss.
 */
export async function generateStaticParams() {
  return (await getCatalog()).map((entry) => ({ slug: entry.slug }));
}

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { slug } = await params;
  const entry = await findCatalogEntry(slug);
  if (!entry) return {};

  const title = `${entry.regulation.name} – Prüfungsordnung, Übungen und Punkte`;
  // Erste Zeile der Beschreibung als Suchergebnis-Text; sie fasst die Prüfung
  // zusammen. Auf 155 Zeichen gekürzt, weil Google danach abschneidet.
  const summary = entry.regulation.description?.split("\n")[0] ?? "";
  const description =
    summary.length > 0
      ? `${summary} Übungen und Punkte der ${entry.regulation.name} im Überblick.`.slice(0, 155)
      : `Übungen, Punkte und Anforderungen der ${entry.regulation.name} (${entry.sport.name}) im Überblick.`;

  return {
    title,
    description,
    alternates: { canonical: `/pruefungsordnungen/${slug}` },
    openGraph: {
      type: "article",
      title,
      description,
      url: absoluteUrl(`/pruefungsordnungen/${slug}`),
    },
  };
}

export default async function RegulationPage({ params }: Params) {
  const { slug } = await params;
  const entry = await findCatalogEntry(slug);
  if (!entry) notFound();

  const detail = await getRegulationDetail(entry.regulation.id);
  const exercises = detail?.exercises ?? [];
  const scored = exercises.filter((exercise) => exercise.maxPoints > 0);
  const totalPoints = scored.reduce((sum, exercise) => sum + exercise.maxPoints, 0);

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "Article",
    headline: `${entry.regulation.name} – Prüfungsordnung, Übungen und Punkte`,
    about: entry.sport.name,
    inLanguage: "de",
    url: absoluteUrl(`/pruefungsordnungen/${slug}`),
    isPartOf: { "@type": "WebSite", name: "Dogity", url: absoluteUrl("/") },
  };

  return (
    <div className="flex min-h-full min-w-0 flex-col">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }}
      />
      <MarketingHeader />

      <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-10">
        <nav className="text-xs text-muted-foreground">
          <Link href="/pruefungsordnungen" className="hover:text-foreground">
            Prüfungsordnungen
          </Link>
          <span aria-hidden> › </span>
          <span>{entry.sport.name}</span>
        </nav>

        <h1 className="mt-3 text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">
          {entry.regulation.name}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">{entry.sport.name}</p>

        {entry.regulation.description && (
          <div className="mt-6 flex flex-col gap-2">
            {entry.regulation.description
              .split("\n")
              .filter((line) => line.trim().length > 0)
              .map((line, index) => (
                <p key={index} className="text-sm sm:text-base">
                  {line}
                </p>
              ))}
          </div>
        )}

        {exercises.length > 0 && (
          <section className="mt-10 min-w-0">
            <h2 className="text-xl font-bold tracking-tight">Übungen</h2>
            {totalPoints > 0 && (
              <p className="mt-1 text-sm text-muted-foreground">
                {scored.length} bewertete Übungen, {totalPoints} Punkte insgesamt.
              </p>
            )}
            <ul className="mt-4 flex flex-col gap-3">
              {exercises.map((exercise) => (
                <li
                  key={exercise.exerciseId}
                  className="min-w-0 rounded-lg border border-border/60 px-3 py-2.5"
                >
                  <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
                    <h3 className="font-medium [overflow-wrap:anywhere]">{exercise.exerciseName}</h3>
                    {exercise.maxPoints > 0 && (
                      <span className="text-sm font-semibold text-primary">{exercise.maxPoints} Punkte</span>
                    )}
                  </div>
                  {exercise.scoringNotes && (
                    <p className="mt-1 text-sm text-muted-foreground [overflow-wrap:anywhere]">
                      {exercise.scoringNotes}
                    </p>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* Rechtlich sauber UND ehrlich: die Angaben sind eine Zusammenfassung,
            verbindlich ist immer die offizielle Ordnung des Verbands. */}
        <p className="mt-10 rounded-lg border border-border/60 bg-muted/40 px-3 py-2.5 text-xs text-muted-foreground">
          Zusammenfassung ohne Gewähr. Verbindlich ist ausschließlich die jeweils gültige Prüfungsordnung des VDH bzw.
          der FCI.
          {detail?.currentVersion?.versionLabel && ` Hinterlegter Stand: ${detail.currentVersion.versionLabel}.`}
        </p>

        <section className="mt-10 border-t border-border/60 pt-8">
          <h2 className="text-xl font-bold tracking-tight">Auf diese Prüfung hintrainieren</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            In Dogity lässt sich {entry.regulation.name} als Ziel setzen. Daraus entsteht ein Wochenplan, der schwache
            Übungen häufiger einplant, und jedes Training wird mit Bewertung und Notizen festgehalten.
          </p>
          <Link href="/register" className={cn(buttonVariants({ size: "lg" }), "mt-5 inline-flex h-11 px-6 text-sm")}>
            Kostenlos starten
          </Link>
        </section>
      </main>

      <MarketingFooter />
    </div>
  );
}

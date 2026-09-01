import type { Metadata } from "next";
import Link from "next/link";
import { getQuizCatalogs } from "@/lib/public-sachkunde";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { absoluteUrl } from "@/lib/seo";
import { buttonVariants } from "@/components/ui/button";
import { GraduationCap } from "lucide-react";

export const metadata: Metadata = {
  title: "Sachkunde für die Begleithundeprüfung – Fragen üben",
  description:
    "Die Fragen zur Sachkundeprüfung der BH/VT kostenlos üben: Verhalten, Gesundheit, Recht, Verbände und Prüfungswesen. Mit Wiedervorlage und Fehlerspeicher – wie beim Führerschein.",
  alternates: { canonical: "/sachkunde" },
};

export default async function SachkundeIndexPage() {
  const catalogs = await getQuizCatalogs();

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    name: "Sachkunde zur Begleithundeprüfung",
    url: absoluteUrl("/sachkunde"),
    inLanguage: "de",
    hasPart: catalogs.map((catalog) => ({
      "@type": "WebPage",
      name: catalog.name,
      url: absoluteUrl(`/sachkunde/${catalog.code.toLowerCase()}`),
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
          Sachkunde für die Begleithundeprüfung
        </h1>
        <p className="mt-4 max-w-2xl text-sm text-muted-foreground sm:text-base">
          Der theoretische Teil der BH/VT: Der Hundeführer beantwortet Fragen zu Verhalten, Haltung, Recht und
          Prüfungswesen. Hier lässt sich das üben – Frage für Frage, mit sofortiger Auflösung. Falsch beantwortete
          Fragen kommen wieder, bis sie sitzen.
        </p>

        {catalogs.length === 0 ? (
          <p className="mt-10 text-sm text-muted-foreground">
            Die Fragenkataloge sind gerade nicht abrufbar. Bitte später erneut versuchen.
          </p>
        ) : (
          <div className="mt-10 flex flex-col gap-6">
            {catalogs.map((catalog) => (
              <section
                key={catalog.code}
                className="min-w-0 rounded-xl border border-border/60 bg-card p-5 sm:p-6"
              >
                <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                  <h2 className="text-xl font-bold tracking-tight">{catalog.name}</h2>
                  <span className="text-sm text-muted-foreground tabular-nums">
                    {catalog.questionCount} Fragen
                  </span>
                </div>

                {catalog.description && (
                  <p className="mt-2 max-w-2xl text-sm text-muted-foreground">{catalog.description}</p>
                )}

                <ul className="mt-4 flex flex-wrap gap-2">
                  {catalog.sections.map((section) => (
                    <li
                      key={section.key}
                      className="rounded-full border border-border/60 px-3 py-1 text-xs text-muted-foreground"
                    >
                      {section.name}
                      <span className="ml-1.5 tabular-nums">{section.questionCount}</span>
                    </li>
                  ))}
                </ul>

                <div className="mt-5 flex flex-wrap items-center gap-3">
                  <Link
                    href={`/sachkunde/${catalog.code.toLowerCase()}`}
                    className={buttonVariants({ size: "sm" })}
                  >
                    <GraduationCap className="size-4" />
                    Üben
                  </Link>
                  <span className="text-xs text-muted-foreground">
                    Fragen: {catalog.publisher}
                    {catalog.edition && <> · Stand {catalog.edition}</>}
                  </span>
                </div>
              </section>
            ))}
          </div>
        )}

        <p className="mt-10 max-w-2xl text-xs text-muted-foreground">
          Angemeldet merkt sich Dogity, was schon sitzt: Jede Frage wandert nach einer richtigen Antwort ein Fach
          weiter und kommt erst später wieder – falsch beantwortet, kommt sie sofort erneut. Ohne Anmeldung lässt
          sich der Katalog frei durchgehen, nur ohne gespeicherten Lernstand.
        </p>
      </main>

      <MarketingFooter />
    </div>
  );
}

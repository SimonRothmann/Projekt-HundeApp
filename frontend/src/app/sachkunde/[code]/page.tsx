import type { Metadata } from "next";
import { notFound } from "next/navigation";
import Link from "next/link";
import { getQuizCatalog, getQuizCatalogs } from "@/lib/public-sachkunde";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { QuizTrainer } from "@/components/sachkunde/quiz-trainer";
import { absoluteUrl } from "@/lib/seo";
import { ChevronLeft } from "lucide-react";

type Props = { params: Promise<{ code: string }> };

/** Beide Kataloge vorbauen - es sind zwei, und sie ändern sich jährlich. */
export async function generateStaticParams() {
  const catalogs = await getQuizCatalogs();
  return catalogs.map((catalog) => ({ code: catalog.code.toLowerCase() }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { code } = await params;
  const catalog = await getQuizCatalog(code);
  if (!catalog) return { title: "Fragenkatalog nicht gefunden" };

  return {
    title: `${catalog.name} – Fragen üben`,
    description:
      `${catalog.questionCount} Fragen zur Sachkundeprüfung der Begleithundeprüfung üben: ` +
      `${catalog.sections.map((s) => s.name).join(", ")}. Mit sofortiger Auflösung und Wiedervorlage.`,
    alternates: { canonical: `/sachkunde/${catalog.code.toLowerCase()}` },
  };
}

export default async function SachkundeCatalogPage({ params }: Props) {
  const { code } = await params;
  const catalog = await getQuizCatalog(code);
  if (!catalog) notFound();

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "LearningResource",
    name: catalog.name,
    url: absoluteUrl(`/sachkunde/${catalog.code.toLowerCase()}`),
    inLanguage: "de",
    learningResourceType: "Quiz",
    educationalLevel: catalog.audience === "Youth" ? "Jugend" : "Erwachsene",
    publisher: { "@type": "Organization", name: catalog.publisher },
  };

  return (
    <div className="flex min-h-full min-w-0 flex-col">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }}
      />
      <MarketingHeader />

      <main className="mx-auto w-full max-w-2xl flex-1 px-4 py-8">
        <Link
          href="/sachkunde"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ChevronLeft className="size-4" />
          Alle Fragenkataloge
        </Link>

        <h1 className="mt-3 text-2xl font-extrabold tracking-tight text-balance sm:text-3xl">{catalog.name}</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          {catalog.questionCount} Fragen · Fragen: {catalog.publisher}
          {catalog.edition && <> · Stand {catalog.edition}</>}
        </p>

        <div className="mt-6">
          <QuizTrainer catalog={catalog} />
        </div>
      </main>

      <MarketingFooter />
    </div>
  );
}

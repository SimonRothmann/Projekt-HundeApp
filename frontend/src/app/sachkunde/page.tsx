import type { Metadata } from "next";
import { getQuizCatalogs } from "@/lib/public-sachkunde";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { absoluteUrl } from "@/lib/seo";
import { KatalogListe } from "@/components/sachkunde/katalog-liste";
import { SachkundeAppWeiterleitung } from "@/components/sachkunde/app-weiterleitung";

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
      <SachkundeAppWeiterleitung />
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

        <div className="mt-10">
          <KatalogListe catalogs={catalogs} basis="/sachkunde" />
        </div>

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

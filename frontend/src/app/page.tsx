import type { Metadata } from "next";
import { AuthedRedirect } from "@/components/marketing/authed-redirect";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { StartseiteInhalt } from "@/components/marketing/startseite-inhalt";
import { FAQ } from "@/components/marketing/startseite-daten";
import { SITE } from "@/lib/seo";

export const metadata: Metadata = {
  title: SITE.title,
  description: SITE.description,
  alternates: { canonical: "/" },
};

export default function HomePage() {
  // Beschreibt der Suchmaschine, WAS diese Seite ist. Ohne strukturierte Daten
  // muss Google das aus dem Fließtext raten.
  const jsonLd = {
    "@context": "https://schema.org",
    "@graph": [
      {
        "@type": "SoftwareApplication",
        name: SITE.name,
        url: SITE.url,
        applicationCategory: "SportsApplication",
        operatingSystem: "Web, Android, iOS",
        description: SITE.description,
        inLanguage: "de",
        offers: { "@type": "Offer", price: "0", priceCurrency: "EUR" },
      },
      {
        "@type": "FAQPage",
        mainEntity: FAQ.map((item) => ({
          "@type": "Question",
          name: item.question,
          acceptedAnswer: { "@type": "Answer", text: item.answer },
        })),
      },
      {
        "@type": "WebSite",
        name: SITE.name,
        url: SITE.url,
        inLanguage: "de",
      },
    ],
  };

  return (
    <div className="flex min-h-full min-w-0 flex-col">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }}
      />
      <AuthedRedirect />
      <MarketingHeader />

      <StartseiteInhalt />

      <MarketingFooter />
    </div>
  );
}

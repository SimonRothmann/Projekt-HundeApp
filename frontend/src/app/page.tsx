import type { Metadata } from "next";
import Link from "next/link";
import {
  BookOpenCheck,
  CalendarRange,
  CloudSun,
  Dog,
  Footprints,
  NotebookPen,
  Users,
  WifiOff,
} from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { AuthedRedirect } from "@/components/marketing/authed-redirect";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { SupportButton } from "@/components/support-button";
import { LetzteNeuerung } from "@/components/letzte-neuerung";
import { SITE } from "@/lib/seo";

export const metadata: Metadata = {
  title: "Dogity – Trainingstagebuch für den Hundesport",
  description: SITE.description,
  alternates: { canonical: "/" },
};

/**
 * Alles hier ist durch tatsächlich vorhandene Funktionen gedeckt - bewusst
 * keine Versprechen auf Vorrat. Eine Startseite, die mehr behauptet als die
 * App kann, bringt Besucher, die sofort wieder gehen; Suchmaschinen werten
 * genau das ab.
 */
const FEATURES = [
  {
    icon: NotebookPen,
    title: "Trainingstagebuch",
    text: "Jede Einheit mit Übungen, Bewertung und Notizen festhalten - in wenigen Sekunden, direkt auf dem Hundeplatz. Trainer können Rückmeldung zu einzelnen Übungen geben.",
  },
  {
    icon: Footprints,
    title: "Fährten per GPS aufzeichnen",
    text: "Gelegte Fährte und Ablauf des Hundes aufzeichnen und übereinanderlegen. Die Auswertung zeigt die Abweichung von der Fährte, gefundene Gegenstände und Stockungen - und unterscheidet dabei, ob der Hund an einem Gegenstand verweist oder wirklich sucht.",
  },
  {
    icon: CloudSun,
    title: "Wetter automatisch",
    text: "Temperatur beim Legen und beim Suchen samt Änderung dazwischen. Genau die bestimmt maßgeblich, wie sich die Geruchsspur hält - ermittelt wird sie ohne eine einzige Eingabe.",
  },
  {
    icon: CalendarRange,
    title: "Trainingsplan, der mitdenkt",
    text: "Aus Prüfungstermin und Ziel entsteht ein Wochenplan, der schwache Übungen häufiger einplant und sitzende seltener. Was mehr geübt werden soll, lässt sich von Hand höher gewichten.",
  },
  {
    icon: BookOpenCheck,
    title: "Prüfungsordnungen hinterlegt",
    text: "BH, IBGH 1-3, IGP 1-3 sowie FPr, GPr, SPr, StöPr und UPr mit ihren Übungen und Punkten - als Grundlage für Ziele und Trainingsplanung.",
  },
  {
    icon: Users,
    title: "Verein und Trainingsgruppen",
    text: "Vereine führen Mitglieder und Gruppen, Trainer planen Gruppentrainings und weisen Übungen zu. Mehrere Trainer können sich eine Gruppe teilen.",
  },
  {
    icon: WifiOff,
    title: "Läuft auch ohne Netz",
    text: "Auf dem Feld gibt es selten guten Empfang. Eingaben werden lokal gespeichert und nachgereicht, sobald wieder Verbindung besteht.",
  },
  {
    icon: Dog,
    title: "Mehrere Hunde, eine Historie",
    text: "Jeder Hund hat sein eigenes Tagebuch, seine Ziele und seine Auswertung - über Jahre hinweg nachvollziehbar.",
  },
];

const FAQ = [
  {
    question: "Was kostet Dogity?",
    answer:
      "Dogity ist kostenlos nutzbar. Es gibt kein Abonnement, keine Bezahlschranke und keine Werbung.",
  },
  {
    question: "Wie kann ich Dogity unterstützen?",
    answer:
      "Freiwillig über Ko-fi. Dogity wird in der Freizeit entwickelt und aus eigener Tasche betrieben; jede Unterstützung hilft, Server und Domain zu bezahlen. Am Funktionsumfang ändert sie nichts - es gibt keine bezahlten Zusatzfunktionen.",
  },
  {
    question: "Für welche Hundesportarten ist Dogity gedacht?",
    answer:
      "Für den Gebrauchshundesport nach VDH und FCI: Begleithundeprüfung (BH), IBGH 1 bis 3 und IGP 1 bis 3 mit Fährte, Unterordnung und Schutzdienst, dazu die Einzelprüfungen FPr, GPr, SPr, StöPr und UPr.",
  },
  {
    question: "Brauche ich eine App aus dem App Store?",
    answer:
      "Nein. Dogity läuft im Browser und lässt sich auf dem Smartphone über 'Zum Home-Bildschirm hinzufügen' wie eine App ablegen - inklusive Offline-Betrieb.",
  },
  {
    question: "Kann ich Fährten wirklich mit dem Handy aufzeichnen?",
    answer:
      "Ja. Beim Legen zeichnet das Handy die Fährte samt Winkeln und Gegenständen auf, beim Ablauf den Weg des Hundes. Beides wird übereinandergelegt und ausgewertet.",
  },
  {
    question: "Kann unser Verein Dogity nutzen?",
    answer:
      "Ja. Vereine können Mitglieder und Trainingsgruppen verwalten, Trainer planen Gruppentrainings und geben den Mitgliedern Rückmeldung zu ihren Trainingseinheiten.",
  },
];

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

      <main className="mx-auto w-full max-w-5xl flex-1 px-4">
        <section className="py-14 sm:py-20">
          <h1 className="text-3xl font-extrabold tracking-tight text-balance sm:text-5xl">
            Das Trainingstagebuch für den Hundesport
          </h1>
          <p className="mt-4 max-w-2xl text-base text-muted-foreground sm:text-lg">
            Training festhalten, Fährten per GPS aufzeichnen und auswerten, Prüfungen vorbereiten. Für Hundesportler,
            Trainer und Vereine im Gebrauchshundesport – kostenlos und ohne Installation.
          </p>
          <div className="mt-7 flex flex-wrap gap-3">
            <Link href="/register" className={cn(buttonVariants({ size: "lg" }), "h-11 px-6 text-sm")}>
              Kostenlos starten
            </Link>
            <Link
              href="/pruefungsordnungen"
              className={cn(buttonVariants({ variant: "outline", size: "lg" }), "h-11 px-6 text-sm")}
            >
              Prüfungsordnungen ansehen
            </Link>
          </div>
        </section>

        <section className="border-t border-border/60 py-12">
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Was Dogity kann</h2>
          <div className="mt-8 grid gap-6 sm:grid-cols-2">
            {FEATURES.map(({ icon: Icon, title, text }) => (
              <article key={title} className="flex min-w-0 gap-3">
                <Icon className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden />
                <div className="min-w-0">
                  <h3 className="font-semibold">{title}</h3>
                  <p className="mt-1 text-sm text-muted-foreground">{text}</p>
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="border-t border-border/60 py-12">
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Für wen Dogity gedacht ist</h2>
          <div className="mt-6 grid gap-6 sm:grid-cols-3">
            <div className="min-w-0">
              <h3 className="font-semibold">Hundesportler</h3>
              <p className="mt-1 text-sm text-muted-foreground">
                Wer regelmäßig trainiert und auf eine Prüfung hinarbeitet, sieht nach Wochen sonst kaum noch, was
                wirklich sitzt. Dogity macht den Verlauf sichtbar.
              </p>
            </div>
            <div className="min-w-0">
              <h3 className="font-semibold">Trainer</h3>
              <p className="mt-1 text-sm text-muted-foreground">
                Gruppentrainings planen, Übungen zuweisen und den Mitgliedern gezielt Rückmeldung geben – auch zwischen
                den Trainingsterminen.
              </p>
            </div>
            <div className="min-w-0">
              <h3 className="font-semibold">Vereine</h3>
              <p className="mt-1 text-sm text-muted-foreground">
                Mitglieder, Trainingsgruppen und Termine an einer Stelle, statt verteilt über Aushang, Gruppenchat und
                Zettelwirtschaft.
              </p>
            </div>
          </div>
        </section>

        <section className="border-t border-border/60 py-12">
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Häufige Fragen</h2>
          <dl className="mt-6 flex flex-col gap-6">
            {FAQ.map((item) => (
              <div key={item.question} className="min-w-0">
                <dt className="font-semibold">{item.question}</dt>
                <dd className="mt-1 text-sm text-muted-foreground">{item.answer}</dd>
              </div>
            ))}
          </dl>
        </section>

        <section className="border-t border-border/60 py-12">
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Dogity unterstützen</h2>
          <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
            Dogity entsteht in der Freizeit und läuft auf selbst bezahlten Servern – kostenlos, werbefrei und ohne
            bezahlte Zusatzfunktionen. Wer mag, kann die Entwicklung freiwillig über Ko-fi unterstützen.
          </p>
          <SupportButton className="mt-6" />
        </section>

        <section className="border-t border-border/60 py-12">
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Zuletzt geändert</h2>
          <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
            An Dogity wird weitergearbeitet. Was zuletzt dazugekommen ist:
          </p>
          <div className="mt-6 rounded-lg border border-border/60 p-4 sm:p-5">
            <LetzteNeuerung />
          </div>
        </section>

        <section className="border-t border-border/60 py-12">
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Loslegen</h2>
          <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
            Konto anlegen, Hund eintragen, erstes Training erfassen. Mehr braucht es nicht.
          </p>
          <Link
            href="/register"
            className={cn(buttonVariants({ size: "lg" }), "mt-6 inline-flex h-11 px-6 text-sm")}
          >
            Kostenlos starten
          </Link>
        </section>
      </main>

      <MarketingFooter />
    </div>
  );
}

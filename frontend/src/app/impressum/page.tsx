import type { Metadata } from "next";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { BETREIBER, STAND } from "@/lib/rechtliches";

export const metadata: Metadata = {
  title: "Impressum",
  description: "Anbieterkennzeichnung nach § 5 DDG für Dogity.",
  alternates: { canonical: "/impressum" },
};

function Abschnitt({ titel, children }: { titel: string; children: React.ReactNode }) {
  return (
    <section className="border-t border-border/60 py-8">
      <h2 className="text-xl font-bold tracking-tight sm:text-2xl">{titel}</h2>
      <div className="mt-4 flex flex-col gap-3 text-sm leading-relaxed text-muted-foreground">{children}</div>
    </section>
  );
}

/**
 * Anbieterkennzeichnung nach § 5 DDG.
 *
 * Muss von jeder Seite aus in höchstens zwei Schritten erreichbar sein -
 * deshalb steht der Verweis in der Fußzeile der öffentlichen Seiten, unter
 * den Anmeldeformularen und im Profil. Die Angaben selbst kommen aus
 * lib/rechtliches.ts, damit sie nicht an mehreren Stellen auseinanderlaufen.
 */
export default function ImpressumPage() {
  return (
    <div className="flex min-h-full min-w-0 flex-col">
      <MarketingHeader />

      <main className="mx-auto w-full max-w-3xl flex-1 px-4">
        <section className="py-12 sm:py-16">
          <h1 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">Impressum</h1>
          <p className="mt-3 text-sm text-muted-foreground">Angaben gemäß § 5 Digitale-Dienste-Gesetz (DDG).</p>
        </section>

        <Abschnitt titel="Anbieter">
          <p className="text-foreground">
            {BETREIBER.name}
            <br />
            {BETREIBER.strasse}
            <br />
            {BETREIBER.plz} {BETREIBER.ort}
            <br />
            {BETREIBER.land}
          </p>
          <p>
            Dogity wird privat und in der Freizeit betrieben. Die Nutzung ist kostenlos, es gibt keine Werbung und keine
            bezahlten Zusatzfunktionen. Eine Umsatzsteuer-Identifikationsnummer besteht daher nicht.
          </p>
        </Abschnitt>

        <Abschnitt titel="Kontakt">
          <p>
            E-Mail:{" "}
            <a
              href={`mailto:${BETREIBER.email}`}
              className="text-primary-text underline-offset-4 hover:underline [overflow-wrap:anywhere]"
            >
              {BETREIBER.email}
            </a>
          </p>
          <p>
            Anfragen zum Datenschutz – Auskunft, Berichtigung, Löschung – gehen an dieselbe Adresse. Was du selbst
            erledigen kannst, ohne zu schreiben, steht in der{" "}
            <a href="/datenschutz" className="text-primary-text underline-offset-4 hover:underline">
              Datenschutzerklärung
            </a>
            .
          </p>
        </Abschnitt>

        <Abschnitt titel="Verantwortlich für den Inhalt">
          <p>
            Verantwortlich nach § 18 Abs. 2 Medienstaatsvertrag: {BETREIBER.name}, Anschrift wie oben.
          </p>
        </Abschnitt>

        <Abschnitt titel="Haftung für Inhalte und Links">
          <p>
            Die Inhalte dieser Seiten werden mit Sorgfalt erstellt, für ihre Richtigkeit und Vollständigkeit kann ich
            aber keine Gewähr übernehmen. Das gilt ausdrücklich auch für die hinterlegten Prüfungsordnungen: Verbindlich
            ist immer die jeweils gültige Fassung des herausgebenden Verbandes, nicht die Darstellung in Dogity.
          </p>
          <p>
            Trainingspläne und Auswertungen in Dogity sind Hilfsmittel, keine fachliche Beratung. Wie ein Hund trainiert
            wird und was ihm zumutbar ist, entscheidet der Mensch am anderen Ende der Leine – im Zweifel zusammen mit
            Trainer:in oder Tierarzt.
          </p>
          <p>
            Diese Seite verweist an einzelnen Stellen auf fremde Internetseiten. Auf deren Inhalte habe ich keinen
            Einfluss und übernehme dafür keine Haftung; verantwortlich ist jeweils deren Anbieter.
          </p>
        </Abschnitt>

        <Abschnitt titel="Urheberrecht">
          <p>
            Die von mir erstellten Inhalte und Werke unterliegen dem deutschen Urheberrecht. Die Bezeichnungen der
            Prüfungsordnungen und die Namen der Übungen stammen von den jeweiligen Verbänden und werden hier zur
            Trainingsplanung wiedergegeben. Deine eigenen Einträge – Hunde, Trainings, Fährten, Notizen – gehören dir;
            ich nutze sie nicht für eigene Zwecke.
          </p>
        </Abschnitt>

        <Abschnitt titel="Verbraucherstreitbeilegung">
          <p>
            Ich bin nicht bereit und nicht verpflichtet, an Streitbeilegungsverfahren vor einer
            Verbraucherschlichtungsstelle teilzunehmen.
          </p>
        </Abschnitt>

        <p className="border-t border-border/60 py-8 text-xs text-muted-foreground">Stand: {STAND}</p>
      </main>

      <MarketingFooter />
    </div>
  );
}

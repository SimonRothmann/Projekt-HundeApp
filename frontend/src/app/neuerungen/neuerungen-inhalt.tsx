"use client";

import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import { VersionStand } from "@/components/version-stand";
import { Badge } from "@/components/ui/badge";
import {
  AENDERUNGSART_LABEL,
  AKTUELLE_VERSION,
  formatiereVeroeffentlichung,
  NACHTRAEGLICH_BIS,
  VERSIONSHINWEISE,
  type Aenderungsart,
} from "@/lib/versionshinweise";
import { SITE } from "@/lib/seo";
import { useT } from "@/lib/i18n";


/**
 * Drei Arten, drei Erscheinungsbilder - und zwar abgestuft, nicht bunt:
 * Neues ist die Nachricht, Verbessertes die Randnotiz, Behobenes die stille
 * Auskunft. Drei gleich kräftige Farben würden die Liste zum Flickenteppich
 * machen, ohne eine einzige Information hinzuzufügen.
 */
const ART_VARIANTE: Record<Aenderungsart, "default" | "secondary" | "outline"> = {
  neu: "default",
  verbessert: "secondary",
  behoben: "outline",
};

export function NeuerungenInhalt() {
  const t = useT();

  return (
    <div className="flex min-h-full min-w-0 flex-col">
      <MarketingHeader />

      <main className="mx-auto w-full max-w-3xl flex-1 px-4">
        <section className="py-10 sm:py-14">
          <h1 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">Neuerungen</h1>
          <p className="mt-3 text-base text-muted-foreground">
            Dogity wird laufend weiterentwickelt. Hier steht, was sich wann geändert hat – und welche Fassung gerade
            läuft.
          </p>

          {/* Der technische Stand gehört nach oben, nicht ins Kleingedruckte:
              Wer diese Seite aufruft, will meist genau eine Frage beantwortet
              haben - ist meine Änderung schon draußen? */}
          <div className="mt-6 rounded-lg border border-border/60 bg-muted/40 px-4 py-3">
            <VersionStand verlinkt={false} className="text-sm" />
          </div>
        </section>

        <div className="flex flex-col gap-10 border-t border-border/60 py-10">
          {VERSIONSHINWEISE.map((hinweis) => (
            <article key={hinweis.version} className="min-w-0">
              <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <h2 className="text-xl font-bold tracking-tight">Version {hinweis.version}</h2>
                <time dateTime={hinweis.datum} className="text-sm text-muted-foreground">
                  {formatiereVeroeffentlichung(hinweis.datum)}
                </time>
                {hinweis.version === AKTUELLE_VERSION && (
                  <Badge variant="secondary" className="shrink-0">
                    Aktuell
                  </Badge>
                )}
              </div>
              <p className="mt-1 font-medium text-balance">{t(hinweis.titel)}</p>

              <ul className="mt-4 flex flex-col gap-3">
                {hinweis.aenderungen.map((aenderung, index) => (
                  <li key={index} className="flex min-w-0 items-start gap-2.5">
                    <Badge variant={ART_VARIANTE[aenderung.art]} className="mt-0.5 shrink-0">
                      {t(AENDERUNGSART_LABEL[aenderung.art])}
                    </Badge>
                    <span className="min-w-0 text-sm text-muted-foreground [overflow-wrap:anywhere]">
                      {t(aenderung.text)}
                    </span>
                  </li>
                ))}
              </ul>
            </article>
          ))}
        </div>

        <section className="border-t border-border/60 py-8">
          <p className="text-sm text-muted-foreground">
            Die Einträge bis einschließlich Version {NACHTRAEGLICH_BIS} sind nachträglich aus der
            Entwicklungsgeschichte zusammengetragen. Die Daten stimmen, die Einteilung in Fassungen ist im Nachhinein
            gezogen. Ab der nächsten Fassung entsteht jeder Eintrag zusammen mit der Änderung selbst.
          </p>
          <p className="mt-3 text-sm text-muted-foreground">
            {SITE.name} ist kostenlos und werbefrei. Rückmeldungen und Wünsche sind willkommen.
          </p>
        </section>
      </main>

      <MarketingFooter />
    </div>
  );
}

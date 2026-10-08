"use client";

import Link from "next/link";
import { GraduationCap } from "lucide-react";
import type { QuizCatalog } from "@/lib/types";
import { buttonVariants } from "@/components/ui/button";
import { useT } from "@/lib/i18n";

/**
 * Die Fragenkataloge als Karten - gemeinsam für die öffentliche Seite
 * (/sachkunde) und die App-Route (/lernen). `basis` sagt, unter welchem
 * Pfad die Kataloge liegen; alles Weitere ist dasselbe.
 *
 * Auf den öffentlichen Seiten gilt Deutsch (siehe useT): Der Übersetzer fällt
 * dort auf den deutschen Satz zurück, die Suchmaschine sieht also dasselbe
 * wie zuvor.
 */
export function KatalogListe({ catalogs, basis }: { catalogs: QuizCatalog[]; basis: "/sachkunde" | "/lernen" }) {
  const t = useT();

  if (catalogs.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        {t("Die Fragenkataloge sind gerade nicht abrufbar. Bitte später erneut versuchen.")}
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {catalogs.map((catalog) => (
        <section key={catalog.code} className="min-w-0 rounded-xl border border-border/60 bg-card p-5 sm:p-6">
          <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
            <h2 className="text-xl font-bold tracking-tight">{catalog.name}</h2>
            <span className="text-sm text-muted-foreground tabular-nums">
              {t("{n} Fragen", { n: catalog.questionCount })}
            </span>
          </div>

          {catalog.description && <p className="mt-2 max-w-2xl text-sm text-muted-foreground">{catalog.description}</p>}

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
              href={`${basis}/${catalog.code.toLowerCase()}`}
              className={buttonVariants({ size: "sm" })}
            >
              <GraduationCap className="size-4" />
              {t("Üben")}
            </Link>
            <span className="text-xs text-muted-foreground">
              <KatalogHerkunft catalog={catalog} />
            </span>
          </div>
        </section>
      ))}
    </div>
  );
}

/** "Fragen: SWHV · Stand 2024" - wer die Fragen herausgibt und in welcher Ausgabe. */
export function KatalogHerkunft({ catalog }: { catalog: Pick<QuizCatalog, "publisher" | "edition"> }) {
  const t = useT();
  return (
    <>
      {t("Fragen: {herausgeber}", { herausgeber: catalog.publisher })}
      {catalog.edition && <> · {t("Stand {ausgabe}", { ausgabe: catalog.edition })}</>}
    </>
  );
}

/** Die Zeile unter der Katalogüberschrift: Umfang und Herkunft. */
export function KatalogDaten({ catalog }: { catalog: QuizCatalog }) {
  const t = useT();
  return (
    <p className="mt-2 text-sm text-muted-foreground">
      {t("{n} Fragen", { n: catalog.questionCount })} · <KatalogHerkunft catalog={catalog} />
    </p>
  );
}

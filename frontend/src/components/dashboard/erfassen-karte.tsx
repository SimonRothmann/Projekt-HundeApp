"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { ChevronRight, NotebookPen, Route } from "lucide-react";
import type { Dog } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { useT } from "@/lib/i18n";

/**
 * Die beiden häufigsten Handgriffe in EINER Karte: Training erfassen und
 * Fährte legen.
 *
 * Früher waren es zwei große Kacheln (zusammen etwa 390 px hoch), die bei
 * mehreren Hunden jeweils noch die Hunde enthielten. Jetzt ist jeder Handgriff
 * eine Zeile: Mit genau einem Hund ist die ganze Zeile der Knopf; mit mehreren
 * stehen die Hunde als Chips darunter. "Training erfassen" öffnet das
 * Eintragen-Fenster gleich hier auf der Startseite, "Fährte legen" führt zur
 * Hundeseite. Chips und Zeilen sind mindestens 44 px hoch.
 */
export function ErfassenKarte({
  hunde,
  faehrtenHundeIds,
  onEintragen,
}: {
  hunde: Dog[];
  faehrtenHundeIds: string[];
  onEintragen: (dogId: string) => void;
}) {
  const t = useT();
  const faehrtenHunde = hunde.filter((h) => faehrtenHundeIds.includes(h.id));

  return (
    <Card size="sm">
      <CardContent className="flex flex-col gap-3 divide-y *:not-first:pt-3">
        <Zeile
          icon={NotebookPen}
          titel={t("Training erfassen")}
          beschreibung={hunde.length === 1 ? t("Einheit für {name} eintragen", { name: hunde[0].name }) : null}
          hunde={hunde}
          onWaehle={onEintragen}
        />
        {faehrtenHunde.length > 0 && (
          <Zeile
            icon={Route}
            titel={t("Fährte legen")}
            beschreibung={faehrtenHunde.length === 1 ? t("Fährte mit {name} legen", { name: faehrtenHunde[0].name }) : null}
            hunde={faehrtenHunde}
            ziel={(id) => `/dogs/${id}#faehrte-aufnehmen`}
          />
        )}
      </CardContent>
    </Card>
  );
}

function Zeile({
  icon: Icon,
  titel,
  beschreibung,
  hunde,
  ziel,
  onWaehle,
}: {
  icon: LucideIcon;
  titel: string;
  beschreibung: string | null;
  hunde: Dog[];
  /** Adresse der Hundeseite - oder, wenn der Handgriff hier erledigt wird, `onWaehle`. */
  ziel?: (dogId: string) => string;
  onWaehle?: (dogId: string) => void;
}) {
  // Beide Symbole in derselben Farbe: zwei Akzentfarben nebeneinander lasen
  // sich wie zwei Zustände, es sind aber zwei gleichrangige Wege.
  const symbol = (
    <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary-text ring-1 ring-primary/20">
      <Icon className="size-5" />
    </span>
  );

  if (hunde.length === 1) {
    const inhalt = (
      <>
        {symbol}
        <span className="min-w-0 flex-1">
          <span className="block font-medium">{titel}</span>
          {beschreibung && <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">{beschreibung}</span>}
        </span>
        <ChevronRight className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" aria-hidden />
      </>
    );
    const klasse = "group flex min-h-11 w-full min-w-0 items-center gap-3 rounded-lg text-left";
    return onWaehle ? (
      <button type="button" onClick={() => onWaehle(hunde[0].id)} aria-haspopup="dialog" className={klasse}>
        {inhalt}
      </button>
    ) : (
      <Link href={ziel!(hunde[0].id)} className={klasse}>
        {inhalt}
      </Link>
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-2">
      <p className="flex items-center gap-3 font-medium">
        {symbol}
        {titel}
      </p>
      <div className="flex flex-wrap gap-2">
        {hunde.map((hund) => {
          const klasse =
            "inline-flex min-h-11 min-w-0 items-center gap-2 rounded-full border border-surface-border bg-surface py-1 pr-4 pl-1.5 text-sm font-medium transition-colors hover:border-primary/50";
          const inhalt = (
            <>
              <DogAvatar dogId={hund.id} hasImage={hund.hasImage} name={hund.name} className="size-8" iconClassName="size-4" />
              <span className="truncate">{hund.name}</span>
            </>
          );
          return onWaehle ? (
            <button key={hund.id} type="button" onClick={() => onWaehle(hund.id)} aria-haspopup="dialog" className={klasse}>
              {inhalt}
            </button>
          ) : (
            <Link key={hund.id} href={ziel!(hund.id)} className={klasse}>
              {inhalt}
            </Link>
          );
        })}
      </div>
    </div>
  );
}

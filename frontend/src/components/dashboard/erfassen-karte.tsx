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
 * eine Zeile: Mit genau einem Hund ist die ganze Zeile der Knopf und landet
 * direkt an der richtigen Stelle der Hundeseite; mit mehreren stehen die Hunde
 * als Chips darunter. Chips und Zeilen sind mindestens 44 px hoch.
 */
export function ErfassenKarte({ hunde, faehrtenHundeIds }: { hunde: Dog[]; faehrtenHundeIds: string[] }) {
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
          ziel={(id) => `/dogs/${id}#training-erfassen`}
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
}: {
  icon: LucideIcon;
  titel: string;
  beschreibung: string | null;
  hunde: Dog[];
  ziel: (dogId: string) => string;
}) {
  // Beide Symbole in derselben Farbe: zwei Akzentfarben nebeneinander lasen
  // sich wie zwei Zustände, es sind aber zwei gleichrangige Wege.
  const symbol = (
    <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary-text ring-1 ring-primary/20">
      <Icon className="size-5" />
    </span>
  );

  if (hunde.length === 1) {
    return (
      <Link href={ziel(hunde[0].id)} className="group flex min-h-11 min-w-0 items-center gap-3 rounded-lg">
        {symbol}
        <span className="min-w-0 flex-1">
          <span className="block font-medium">{titel}</span>
          {beschreibung && <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">{beschreibung}</span>}
        </span>
        <ChevronRight className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" aria-hidden />
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
        {hunde.map((hund) => (
          <Link
            key={hund.id}
            href={ziel(hund.id)}
            className="inline-flex min-h-11 min-w-0 items-center gap-2 rounded-full border border-surface-border bg-surface py-1 pr-4 pl-1.5 text-sm font-medium transition-colors hover:border-primary/50"
          >
            <DogAvatar dogId={hund.id} hasImage={hund.hasImage} name={hund.name} className="size-8" iconClassName="size-4" />
            <span className="truncate">{hund.name}</span>
          </Link>
        ))}
      </div>
    </div>
  );
}

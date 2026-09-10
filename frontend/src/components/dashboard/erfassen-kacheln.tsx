"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { NotebookPen, Route } from "lucide-react";
import type { Dog } from "@/lib/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { cn } from "@/lib/utils";

import { useT } from "@/lib/i18n";

/**
 * Die beiden häufigsten Handgriffe als Kacheln: Training erfassen und Fährte
 * legen.
 *
 * Mit einem Hund ist die ganze Kachel ein Link und landet direkt an der
 * richtigen Stelle der Hundeseite. Mit mehreren Hunden stehen die Hunde in der
 * Kachel zum Antippen - vorher führte "Training erfassen" dann nur zur
 * Hundeliste, von dort ging es zum Hund und 1,7 Bildschirme nach unten.
 */
export function ErfassenKacheln({ hunde, faehrtenHundeIds }: { hunde: Dog[]; faehrtenHundeIds: string[] }) {
  const t = useT();
  const faehrtenHunde = hunde.filter((h) => faehrtenHundeIds.includes(h.id));

  return (
    <>
      <Kachel
        icon={NotebookPen}
        akzent
        titel={t("Training erfassen")}
        beschreibung={
          hunde.length === 1 ? t("Einheit für {name} eintragen", { name: hunde[0].name }) : t("Für welchen Hund?")
        }
        hunde={hunde}
        ziel={(id) => `/dogs/${id}#training-erfassen`}
        breit={faehrtenHunde.length === 0}
      />
      {faehrtenHunde.length > 0 && (
        <Kachel
          icon={Route}
          titel={t("Fährte legen")}
          beschreibung={
            faehrtenHunde.length === 1
              ? t("Fährte mit {name} aufnehmen", { name: faehrtenHunde[0].name })
              : t("Mit welchem Hund?")
          }
          hunde={faehrtenHunde}
          ziel={(id) => `/dogs/${id}#faehrte-aufnehmen`}
          breit={false}
        />
      )}
    </>
  );
}

function Kachel({
  icon: Icon,
  akzent = false,
  titel,
  beschreibung,
  hunde,
  ziel,
  breit,
}: {
  icon: LucideIcon;
  akzent?: boolean;
  titel: string;
  beschreibung: string;
  hunde: Dog[];
  ziel: (dogId: string) => string;
  breit: boolean;
}) {
  const kopf = (
    <CardHeader className="flex-row items-center gap-4 space-y-0">
      <span
        className={cn(
          "flex size-12 shrink-0 items-center justify-center rounded-xl ring-1 transition-colors",
          akzent
            ? "bg-accent/15 text-accent ring-accent/25 group-hover:bg-accent/20"
            : "bg-primary/10 text-primary-text ring-primary/20 group-hover:bg-primary/15",
        )}
      >
        <Icon className="size-6" />
      </span>
      <div className="min-w-0">
        <CardTitle>{titel}</CardTitle>
        <CardDescription>{beschreibung}</CardDescription>
      </div>
    </CardHeader>
  );

  if (hunde.length === 1) {
    return (
      <Link href={ziel(hunde[0].id)} className={cn("group block", breit && "sm:col-span-2")}>
        <Card className="h-full transition-all duration-150 hover:-translate-y-0.5 hover:shadow-[var(--shadow-glow)]">{kopf}</Card>
      </Link>
    );
  }

  return (
    <Card className={cn("h-full", breit && "sm:col-span-2")}>
      {kopf}
      <CardContent className="flex flex-wrap gap-2">
        {hunde.map((hund) => (
          <Link
            key={hund.id}
            href={ziel(hund.id)}
            className="inline-flex min-w-0 items-center gap-2 rounded-full border border-surface-border bg-surface py-1 pr-3.5 pl-1 text-sm font-medium transition-colors hover:border-primary/50 coarse:min-h-11"
          >
            <DogAvatar dogId={hund.id} hasImage={hund.hasImage} name={hund.name} className="size-8" iconClassName="size-4" />
            <span className="truncate">{hund.name}</span>
          </Link>
        ))}
      </CardContent>
    </Card>
  );
}

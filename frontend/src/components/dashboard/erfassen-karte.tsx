"use client";

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
 * mehreren Hunden jeweils noch die Hunde enthielten. Jetzt gilt: Mit genau einem
 * Hund ist jeder Handgriff eine Zeile, die ganz der Knopf ist. Mit mehreren
 * Hunden gibt es eine Zeile je Hund (Avatar und Name) mit zwei kompakten Knöpfen,
 * "Training" und - wenn der Hund Fährten läuft - "Fährte" bzw. "Trail"; sonst
 * standen bei drei Hunden sechs fast gleiche Chips untereinander. Beide öffnen
 * ihr Fenster gleich hier auf der Startseite - "Fährte legen" führte früher erst
 * auf die Hundeseite. Knöpfe und Zeilen sind mindestens 44 px hoch.
 */
export function ErfassenKarte({
  hunde,
  faehrtenHundeIds,
  nurTrailHundeIds = [],
  onEintragen,
  onFaehrte,
}: {
  hunde: Dog[];
  faehrtenHundeIds: string[];
  /** Hunde, die trailen, aber keine Fährte laufen: "Trail legen" statt "Fährte legen". */
  nurTrailHundeIds?: string[];
  onEintragen: (dogId: string) => void;
  onFaehrte: (dogId: string) => void;
}) {
  const t = useT();

  if (hunde.length > 1) {
    return (
      <Card size="sm">
        {/* divide-y zieht die Linie UNTER jede Zeile außer der letzten (Tailwind 4): Der
            Abstand gehört deshalb oben UND unten an jede Zeile. Knapp, weil die
            Knöpfe selbst schon 44 px hoch sind. */}
        <CardContent className="flex flex-col gap-2">
          <p className="text-sm font-semibold">{t("Erfassen")}</p>
          <ul className="flex flex-col divide-y">
            {hunde.map((hund) => {
              const nurTrailHund = nurTrailHundeIds.includes(hund.id);
              return (
                <li key={hund.id} className="flex min-w-0 items-center gap-1 py-1 first:pt-0 last:pb-0">
                  <DogAvatar dogId={hund.id} hasImage={hund.hasImage} name={hund.name} className="size-8" iconClassName="size-4" />
                  <span className="min-w-0 flex-1 truncate font-medium">{hund.name}</span>
                  <KompaktKnopf
                    icon={NotebookPen}
                    text={t("Training")}
                    name={t("Training für {name} erfassen", { name: hund.name })}
                    onClick={() => onEintragen(hund.id)}
                  />
                  {faehrtenHundeIds.includes(hund.id) && (
                    <KompaktKnopf
                      icon={Route}
                      text={nurTrailHund ? t("Trail") : t("Fährte")}
                      name={nurTrailHund ? t("Trail mit {name} legen", { name: hund.name }) : t("Fährte mit {name} legen", { name: hund.name })}
                      onClick={() => onFaehrte(hund.id)}
                    />
                  )}
                </li>
              );
            })}
          </ul>
        </CardContent>
      </Card>
    );
  }

  const faehrtenHunde = hunde.filter((h) => faehrtenHundeIds.includes(h.id));
  const nurTrail = faehrtenHunde.length > 0 && nurTrailHundeIds.includes(faehrtenHunde[0].id);

  return (
    <Card size="sm">
      <CardContent className="flex flex-col divide-y">
        <Zeile
          icon={NotebookPen}
          titel={t("Training erfassen")}
          beschreibung={hunde.length === 1 ? t("Einheit für {name} eintragen", { name: hunde[0].name }) : null}
          onWaehle={() => onEintragen(hunde[0].id)}
        />
        {faehrtenHunde.length > 0 && (
          <Zeile
            icon={Route}
            titel={nurTrail ? t("Trail legen") : t("Fährte legen")}
            beschreibung={
              nurTrail
                ? t("Trail mit {name} legen", { name: faehrtenHunde[0].name })
                : t("Fährte mit {name} legen", { name: faehrtenHunde[0].name })
            }
            onWaehle={() => onFaehrte(faehrtenHunde[0].id)}
          />
        )}
      </CardContent>
    </Card>
  );
}

/** Gleicher Abstand über und unter der Trennlinie zwischen den Zeilen. */
const ABSTAND = "py-3 first:pt-0 last:pb-0";

function KompaktKnopf({ icon: Icon, text, name, onClick }: { icon: LucideIcon; text: string; name: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-haspopup="dialog"
      aria-label={name}
      className="inline-flex min-h-11 shrink-0 items-center gap-1.5 rounded-full px-3 text-sm font-medium text-primary-text transition-colors hover:bg-muted active:bg-muted"
    >
      <Icon className="size-4" aria-hidden />
      {text}
    </button>
  );
}

/** Eine ganze Zeile als Knopf - für genau einen Hund. */
function Zeile({
  icon: Icon,
  titel,
  beschreibung,
  onWaehle,
}: {
  icon: LucideIcon;
  titel: string;
  beschreibung: string | null;
  onWaehle: () => void;
}) {
  // Beide Symbole in derselben Farbe: zwei Akzentfarben nebeneinander lasen
  // sich wie zwei Zustände, es sind aber zwei gleichrangige Wege.
  return (
    <button
      type="button"
      onClick={onWaehle}
      aria-haspopup="dialog"
      className={`group flex min-h-11 w-full min-w-0 items-center gap-3 rounded-lg text-left ${ABSTAND}`}
    >
      <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary-text ring-1 ring-primary/20">
        <Icon className="size-5" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block font-medium">{titel}</span>
        {beschreibung && <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">{beschreibung}</span>}
      </span>
      <ChevronRight className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" aria-hidden />
    </button>
  );
}

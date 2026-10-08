"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { MoreHorizontal } from "lucide-react";
import type { Dog } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { formatDogAge } from "@/lib/dog-age";
import { aktiveHunde, hundeAdresse, zeigeHundeChips } from "@/lib/hundeseite";
import { useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * Kopf der Hundeseite: Name, Rasse und Alter, rechts der Knopf "Verwalten"
 * (Bearbeiten, Drucken, Mitbesitzer, Archivieren, Löschen - siehe
 * DogActionsSheet). Darunter, wer mehr als einen aktiven Hund hat, die Chips
 * zum direkten Wechseln.
 *
 * Der Zurück-Knopf steht darüber und gehört der App-Hülle (SubpageBackButton).
 */
export function DogHeader({
  dog,
  meineHunde,
  gehoertMir,
  onVerwalten,
}: {
  dog: Dog;
  meineHunde: Dog[];
  /** Eigener oder mitbesessener Hund - bei einem betreuten fremden Hund gibt es keine Chips. */
  gehoertMir: boolean;
  onVerwalten: () => void;
}) {
  const t = useT();

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <DogAvatar dogId={dog.id} hasImage={dog.hasImage} name={dog.name} />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="min-w-0 text-2xl font-semibold tracking-tight [overflow-wrap:anywhere]">{dog.name}</h1>
            {dog.archivedAt && <Badge variant="secondary">{t("Archiviert")}</Badge>}
          </div>
          <p className="text-muted-foreground [overflow-wrap:anywhere]">
            {[dog.breed ?? t("Unbekannte Rasse"), formatDogAge(dog.birthday, new Date(), t)].filter(Boolean).join(" · ")}
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="icon"
          className="size-11 shrink-0"
          aria-label={t("Verwalten")}
          aria-haspopup="dialog"
          onClick={onVerwalten}
        >
          <MoreHorizontal className="size-5" />
        </Button>
      </div>
      {zeigeHundeChips(meineHunde, gehoertMir) && <DogSwitcher hunde={meineHunde} aktuellId={dog.id} />}
    </div>
  );
}

/**
 * Chips zum Wechseln zwischen den eigenen Hunden. Umbrechen statt scrollen: ab
 * fünf Hunden würde eine Zeile seitlich wegwischen, und das gibt es in dieser
 * App nirgends.
 */
function DogSwitcher({ hunde, aktuellId }: { hunde: Dog[]; aktuellId: string }) {
  const t = useT();
  const router = useRouter();
  const suchparameter = useSearchParams();

  return (
    <nav aria-label={t("Hund wechseln")} className="flex flex-wrap gap-2">
      {aktiveHunde(hunde).map((hund) => {
        const aktuell = hund.id === aktuellId;
        return (
          <button
            key={hund.id}
            type="button"
            aria-current={aktuell ? "page" : undefined}
            // replace statt push: Der Wechsel soll keine Kette von Einträgen im
            // Verlauf anlegen, durch die man sich mit "Zurück" kämpfen müsste.
            onClick={() => {
              if (!aktuell) router.replace(hundeAdresse(hund.id, suchparameter.toString()));
            }}
            className={cn(
              "inline-flex min-h-11 max-w-full min-w-0 items-center rounded-full border px-4 text-sm font-medium transition-colors",
              aktuell
                ? "border-primary bg-primary/15 text-primary-text"
                : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
            )}
          >
            <span className="truncate">{hund.name}</span>
          </button>
        );
      })}
    </nav>
  );
}

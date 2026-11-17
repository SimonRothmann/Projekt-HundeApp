"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { MoreHorizontal, Plus } from "lucide-react";
import type { Dog } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { formatDogAge } from "@/lib/dog-age";
import { aktiveHunde, hundeAdresse, zeigeHundeLeiste } from "@/lib/hundeseite";
import { useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * Kopf der Hundeseite: Hundefoto (oder Symbol), Name, Rasse und Alter, rechts
 * der Knopf "Verwalten" (Bearbeiten, Drucken, Mitbesitzer:in, Archivieren,
 * Löschen - siehe DogActionsSheet). Darüber die runden Avatare zum direkten
 * Wechseln und am Ende ein "+" für einen weiteren Hund - schon bei einem Hund,
 * damit der Weg dorthin zu finden ist.
 *
 * Der Zurück-Knopf steht darüber und gehört der App-Hülle (SubpageBackButton).
 */
export function DogHeader({
  dog,
  meineHunde,
  gehoertMir,
  onVerwalten,
  onNeuerHund,
}: {
  dog: Dog;
  meineHunde: Dog[];
  /** Eigener oder mitbesessener Hund - bei einem betreuten fremden Hund gibt es keine Leiste. */
  gehoertMir: boolean;
  onVerwalten: () => void;
  /** Öffnet das Fenster "Neuer Hund" (das "+" in der Leiste). */
  onNeuerHund: () => void;
}) {
  const t = useT();

  return (
    <div className="flex flex-col gap-4">
      {zeigeHundeLeiste(meineHunde, gehoertMir) && <DogSwitcher hunde={meineHunde} aktuellId={dog.id} onNeuerHund={onNeuerHund} />}
      <div className="flex items-center gap-4">
        <DogAvatar dogId={dog.id} hasImage={dog.hasImage} name={dog.name} className="size-20" iconClassName="size-9" />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            {/* Lange Namen (Zuchtnamen) eine Stufe kleiner und mit Silbentrennung statt Umbruch mitten im Wort. */}
            <h1
              lang="de"
              className={cn(
                "min-w-0 font-bold tracking-tight hyphens-auto [overflow-wrap:break-word]",
                dog.name.length > 16 ? "text-2xl" : "text-3xl",
              )}
            >
              {dog.name}
            </h1>
            {dog.archivedAt && <Badge variant="secondary">{t("Archiviert")}</Badge>}
          </div>
          <p className="text-muted-foreground [overflow-wrap:anywhere]">
            {[dog.breed ?? t("Unbekannte Rasse"), formatDogAge(dog.birthday, new Date(), t)].filter(Boolean).join(" · ")}
          </p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-11 shrink-0 rounded-full text-muted-foreground"
          aria-label={t("Verwalten")}
          aria-haspopup="dialog"
          onClick={onVerwalten}
        >
          <MoreHorizontal className="size-5" />
        </Button>
      </div>
    </div>
  );
}

/**
 * Runde Avatare mit Namen zum Wechseln zwischen den eigenen Hunden, der
 * aktuelle mit Ring, am Ende ein "+" zum Anlegen. Umbrechen statt scrollen: ab fünf Hunden würde eine Zeile
 * seitlich wegwischen, und das gibt es in dieser App nirgends.
 */
function DogSwitcher({ hunde, aktuellId, onNeuerHund }: { hunde: Dog[]; aktuellId: string; onNeuerHund: () => void }) {
  const t = useT();
  const router = useRouter();
  const suchparameter = useSearchParams();

  return (
    <nav aria-label={t("Hund wechseln")} className="flex flex-wrap gap-x-3 gap-y-2">
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
            className="flex min-h-11 w-16 min-w-0 flex-col items-center gap-1"
          >
            <span
              aria-hidden
              className={cn(
                "rounded-full ring-2 ring-offset-2 ring-offset-background transition-colors",
                aktuell ? "ring-primary" : "ring-transparent",
              )}
            >
              <DogAvatar dogId={hund.id} hasImage={hund.hasImage} name={hund.name} className="size-11" iconClassName="size-5" />
            </span>
            <span className={cn("w-full truncate text-center text-xs", aktuell ? "font-medium text-primary-text" : "text-muted-foreground")}>
              {hund.name}
            </span>
          </button>
        );
      })}
      <button
        type="button"
        aria-label={t("Weiteren Hund anlegen")}
        aria-haspopup="dialog"
        onClick={onNeuerHund}
        className="flex min-h-11 w-16 min-w-0 flex-col items-center gap-1"
      >
        <span
          aria-hidden
          className="flex size-11 items-center justify-center rounded-full border border-dashed border-input text-muted-foreground ring-2 ring-transparent ring-offset-2 ring-offset-background"
        >
          <Plus className="size-5" />
        </span>
        <span aria-hidden className="w-full truncate text-center text-xs text-muted-foreground">
          {/* "Neuer Hund" passte in 64 px nur abgeschnitten. */}
          {t("Neu")}
        </span>
      </button>
    </nav>
  );
}

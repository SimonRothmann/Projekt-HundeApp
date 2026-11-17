"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import { MODULE, type Dog } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ChevronRight, CirclePause, Dog as DogIcon, Plus, TriangleAlert } from "lucide-react";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { HundEinladungen } from "@/components/dogs/hund-einladungen";
import { NeuerHundSheet } from "@/components/dogs/neuer-hund-sheet";
import { direktZumHund, hundeseiteVomReiter, leseZuletztHund } from "@/lib/hundeliste";
import { formatDogAge } from "@/lib/dog-age";
import { aktuellePause, pausenZeile } from "@/lib/pausen";
import { ueberfaelligZeile } from "@/lib/gesundheit";
import { usePreferences } from "@/lib/preferences-context";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

import { useSprache, useT } from "@/lib/i18n";

/**
 * Der Reiter "Hunde": führt immer gleich auf die Hundeseite (zuletzt angesehener, sonst erster
 * aktiver Hund - siehe direktZumHund). Hier bleibt man nur ohne aktiven Hund (Leerzustand zum
 * Anlegen, wartende Einladungen) oder mit ?liste=1: die Liste als Gesamt- und Archivansicht,
 * erreichbar über das ⋯-Fenster der Hundeseite.
 */
export default function DogsPage() {
  const [dogs, setDogs] = useState<Dog[] | null>(null);
  // null = noch nicht bekannt. window statt useSearchParams: Die Seite wird vorab gebaut, und für
  // den einen Parameter braucht es keine Suspense-Grenze.
  const [liste, setListe] = useState<boolean | null>(null);
  // Offline ohne zwischengespeicherte Hundeseite: dann ist die Liste das Bessere.
  const [bleibt, setBleibt] = useState(false);
  const [neuerHundOffen, setNeuerHundOffen] = useState(false);
  const router = useRouter();
  const t = useT();
  const sprache = useSprache();
  const { moduleEnabled } = usePreferences();
  const gesundheitAn = moduleEnabled(MODULE.gesundheit);

  // Das Ziel steht schon beim Rendern fest, damit die Liste nie aufblitzt: Bis es entschieden ist
  // (und beim Weiterleiten) stehen Platzhalter da. Der Lesecache der Hunde genügt dafür.
  const ziel = liste === false && dogs !== null && !bleibt ? direktZumHund(dogs, { zuletzt: leseZuletztHund(), listeGewuenscht: false }) : null;
  const entscheidet = liste === null || dogs === null || ziel !== null;

  useEffect(() => {
    if (!ziel) return;
    void (async () => {
      if (!navigator.onLine && !(await getCachedData(`dog-page-${ziel}`))) {
        setBleibt(true);
        return;
      }
      router.replace(hundeseiteVomReiter(ziel));
    })();
  }, [ziel, router]);

  async function loadDogs() {
    const cached = await getCachedData<Dog[]>("dogs-list");
    if (cached) setDogs(cached);

    try {
      const data = await api.get<Dog[]>("/api/dogs");
      setDogs(data);
      await setCachedData("dogs-list", data);
    } catch (err) {
      if (!cached) toast.error(err instanceof ApiError ? err.message : t("Hunde konnten nicht geladen werden."));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf beim Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDogs();
    setListe(new URLSearchParams(window.location.search).has("liste"));
  }, []);

  // Archivierte Hunde (z.B. verstorben) getrennt vom aktiven Bestand anzeigen -
  // sie bleiben erreichbar, verstellen aber nicht die tägliche Liste.
  const activeDogs = dogs?.filter((d) => !d.archivedAt) ?? [];
  const archivedDogs = dogs?.filter((d) => d.archivedAt) ?? [];

  // Die Hunde einer Gruppe stehen in EINER Karte, je Hund eine Zeile mit Trennlinie.
  function dogListe(hunde: Dog[], archived = false) {
    return (
      <div className="flex flex-col divide-y overflow-hidden rounded-xl bg-card ring-1 ring-foreground/10">
        {hunde.map((dog) => dogZeile(dog, archived))}
      </div>
    );
  }

  function dogZeile(dog: Dog, archived: boolean) {
    const pause = aktuellePause(dog);
    // Der Stand kommt nur für Besitzer:innen vom Server; archivierte Hunde melden nichts mehr.
    const ueberfaellig = gesundheitAn && !archived ? ueberfaelligZeile(t, dog.gesundheit) : null;
    return (
      <Link
        key={dog.id}
        href={`/dogs/${dog.id}`}
        className={cn(
          "flex min-h-11 min-w-0 items-center gap-3 px-4 py-3 transition-colors hover:bg-muted/60 active:bg-muted",
          archived && "opacity-70",
        )}
      >
        <DogAvatar dogId={dog.id} hasImage={dog.hasImage} name={dog.name} />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-base font-medium [overflow-wrap:anywhere]">{dog.name}</span>
            {archived && <Badge variant="secondary">{t("Archiviert")}</Badge>}
          </div>
          <p className="text-sm text-muted-foreground">
            {[dog.breed ?? t("Unbekannte Rasse"), formatDogAge(dog.birthday, new Date(), t)].filter(Boolean).join(" · ")}
          </p>
          {pause && (
            <p className="mt-0.5 flex items-start gap-1.5 text-sm font-medium text-amber-800 dark:text-amber-300">
              <CirclePause className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              <span className="min-w-0 [overflow-wrap:anywhere]">{pausenZeile(t, pause, sprache)}</span>
            </p>
          )}
          {ueberfaellig && (
            <p className="mt-0.5 flex items-start gap-1.5 text-sm font-medium text-destructive">
              <TriangleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              <span className="min-w-0 [overflow-wrap:anywhere]">{ueberfaellig}</span>
            </p>
          )}
        </div>
        <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
      </Link>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold tracking-tight">{t("Meine Hunde")}</h1>
        <Button onClick={() => setNeuerHundOffen(true)} size="sm" aria-haspopup="dialog">
          <Plus className="size-4" />
          {t("Hund hinzufügen")}
        </Button>
      </div>

      <HundEinladungen onAngenommen={loadDogs} />

      <NeuerHundSheet
        open={neuerHundOffen}
        onOpenChange={setNeuerHundOffen}
        onAngelegt={(hund) => {
          toast.success(t("{name} ist angelegt.", { name: hund.name }));
          router.push(hundeseiteVomReiter(hund.id));
        }}
      />

      {entscheidet || dogs === null ? (
        <div aria-busy="true" className="flex flex-col gap-3">
          <span className="sr-only">{t("Lädt…")}</span>
          <div className="h-20 rounded-xl bg-card motion-safe:animate-pulse" />
          <div className="h-20 rounded-xl bg-card motion-safe:animate-pulse" />
        </div>
      ) : dogs.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center text-muted-foreground">
            <DogIcon className="size-10" />
            <p>{t("Leg deinen ersten Hund an.")}</p>
            <Button className="h-11 text-base" aria-haspopup="dialog" onClick={() => setNeuerHundOffen(true)}>
              <Plus className="size-4" />
              {t("Hund anlegen")}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <>
          {activeDogs.length > 0 ? (
            dogListe(activeDogs)
          ) : (
            <p className="text-sm text-muted-foreground">{t("Alle Hunde sind archiviert.")}</p>
          )}

          {archivedDogs.length > 0 && (
            <div className="flex flex-col gap-3">
              <h2 className="text-sm font-medium text-muted-foreground">{t("Archivierte Hunde")}</h2>
              {dogListe(archivedDogs, true)}
            </div>
          )}
        </>
      )}
    </div>
  );
}

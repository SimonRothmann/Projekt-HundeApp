"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { ChevronRight, CircleCheck, PawPrint } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { erstesTrainingHund, ZIELVORSCHLAG_TAGE, zielvorschlag } from "@/lib/erststart";
import { heuteIso, plusTage } from "@/lib/kalendertag";
import { ordnungenPfad } from "@/lib/katalog-land";
import { katalogName } from "@/lib/katalog-name";
import { VORGABE_LAND } from "@/lib/i18n/laender";
import { usePreferences } from "@/lib/preferences-context";
import { ladeSportarten } from "@/lib/sportarten";
import type { Dog, Goal, OnboardingStatus, Regulation } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { AusblendenKnopf } from "@/components/onboarding/ausblenden-knopf";
import { useSprache, useT } from "@/lib/i18n";

/**
 * "Erstes Training eintragen" - die Karte für Konten mit Hund, aber ohne Training (erststartZustand
 * "erstesTraining"). Der Hauptknopf öffnet das Eintragen-Fenster des (ältesten) Hundes; dort sind die Übungen
 * der Einstiegsprüfung schon vorgeschlagen (lib/einstieg.ts).
 *
 * Darunter, nur ohne Ziel und mit bekannter Sportart (Profil), ein Vorschlag: die Einstiegsprüfung als Ziel in
 * 12 Wochen. Ein Tipp legt das Ziel samt Plan an (das Datum lässt sich später ändern); danach steht hier der
 * Hinweis mit dem Weg zum Plan. Wie die übrigen Erststart-Karten ausblendbar, verschwindet sie mit dem ersten Training.
 */
export function ErstesTrainingKarte({
  status,
  hunde,
  onEintragen,
  onChanged,
  onDismissed,
}: {
  status: OnboardingStatus;
  hunde: readonly Dog[];
  onEintragen: (dogId: string) => void;
  /** Das Ziel ist angelegt: Die Startseite lädt neu (die Zielkarte mit dem Plan erscheint). */
  onChanged: () => Promise<void>;
  onDismissed: () => void;
}) {
  const t = useT();
  const sprache = useSprache();
  const { preferences } = usePreferences();
  const hund = erstesTrainingHund(status, hunde);
  const land = preferences.country ?? VORGABE_LAND;
  const profilSportIds = preferences.sportIds;
  const [vorschlag, setVorschlag] = useState<ReturnType<typeof zielvorschlag>>(null);
  const [laeuft, setLaeuft] = useState(false);
  // Bleibt stehen, auch wenn der Server-Status nach dem Anlegen schon "hat Ziel" sagt.
  const [gesetzt, setGesetzt] = useState(false);

  // Lädt etwas nicht, fällt der Vorschlag still weg - das Eintragen ist die Hauptsache.
  useEffect(() => {
    if (status.hasGoal || profilSportIds.length === 0) return;
    let abgebrochen = false;
    (async () => {
      try {
        const [sportarten, listen] = await Promise.all([
          ladeSportarten(),
          Promise.all(profilSportIds.map((id) => api.get<Regulation[]>(ordnungenPfad(id, land)))),
        ]);
        if (!abgebrochen) setVorschlag(zielvorschlag(profilSportIds, sportarten, Object.fromEntries(profilSportIds.map((id, i) => [id, listen[i]]))));
      } catch {
        // Kein Vorschlag.
      }
    })();
    return () => {
      abgebrochen = true;
    };
  }, [status.hasGoal, profilSportIds, land]);

  async function zielSetzen() {
    if (!hund || !vorschlag) return;
    setLaeuft(true);
    try {
      await api.post<Goal>("/api/goals", {
        dogId: hund.id,
        sportId: vorschlag.sport.id,
        regulationId: vorschlag.ordnung.id,
        targetDate: plusTage(heuteIso(), ZIELVORSCHLAG_TAGE),
        notes: null,
        isCustom: false,
      });
      setGesetzt(true);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ziel konnte nicht angelegt werden."));
    } finally {
      setLaeuft(false);
    }
  }

  if (!hund) return null;

  return (
    <Card className="border-primary/40 bg-primary/5">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <PawPrint className="size-5 shrink-0" aria-hidden />
          {t("Erstes Training eintragen")}
        </CardTitle>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">
          {t("Das dauert nur ein paar Sekunden – die Übungen sind schon vorgeschlagen.")}
        </p>

        <Button type="button" className="h-11 w-full text-base" onClick={() => onEintragen(hund.id)}>
          {t("Training mit {name} eintragen", { name: hund.name })}
          <ChevronRight className="size-4" aria-hidden />
        </Button>

        {gesetzt ? (
          <div className="flex flex-col gap-1" role="status">
            <p className="flex items-start gap-2 text-sm font-medium">
              <CircleCheck className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
              <span className="min-w-0 [overflow-wrap:anywhere]">{t("Ziel gesetzt – dein Trainingsplan steht.")}</span>
            </p>
            <Link
              href={`/dogs/${hund.id}?tab=plan`}
              className="flex min-h-11 items-center self-start text-sm text-primary-text underline-offset-4 hover:underline"
            >
              {t("Zum Trainingsplan")}
            </Link>
          </div>
        ) : (
          vorschlag &&
          !status.hasGoal && (
            <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
              <p className="min-w-0 flex-1 basis-48 text-sm [overflow-wrap:anywhere]">
                {t("{ordnung} als Ziel in 12 Wochen?", { ordnung: katalogName(vorschlag.ordnung, sprache) })}
              </p>
              <Button type="button" size="sm" variant="ghost" className="text-primary-text coarse:min-h-11" disabled={laeuft} onClick={zielSetzen}>
                {laeuft ? t("Wird generiert…") : t("Ziel setzen")}
              </Button>
            </div>
          )
        )}

        <AusblendenKnopf onDismissed={onDismissed} />
      </CardContent>
    </Card>
  );
}

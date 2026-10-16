"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { DogBetreuung } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { useSprache, useT } from "@/lib/i18n";
import { datumKurz } from "@/lib/pruefung";

/**
 * Die Trainer:innen, die einen Hund betreuen, mit dem Knopf zum Beenden.
 *
 * Eine Trainer:in übernimmt die Betreuung in ihrer Gruppe, und die
 * Besitzer:in wird nur benachrichtigt. Ohne diese Liste sähe sie später
 * nirgends, wer Einblick hat - und könnte es nur beenden, indem sie die
 * Gruppe verlässt. Es dürfen alle Besitzer:innen, auch Mitbesitzer:innen.
 */
export function BetreuungenSection({
  dogId,
  dogName,
  betreuungen,
  onChanged,
}: {
  dogId: string;
  dogName: string;
  betreuungen: DogBetreuung[];
  /** Nach dem Beenden: die Liste neu laden. */
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const sprache = useSprache();
  const [laeuft, setLaeuft] = useState<string | null>(null);

  async function beenden(b: DogBetreuung) {
    const frage = t(
      "Betreuung von {hund} durch {name} beenden? {name} sieht danach Tagebuch, Ziele und Fährten von {hund} nicht mehr.",
      { hund: dogName, name: b.trainerName },
    );
    if (!window.confirm(frage)) return;
    setLaeuft(b.trainerId);
    try {
      await api.delete(`/api/dogs/${dogId}/betreuungen/${b.trainerId}`);
      toast.success(t("Betreuung beendet."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setLaeuft(null);
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-surface-border bg-surface p-3">
      <p className="text-sm text-muted-foreground">
        {t("Diese Trainer:innen betreuen {hund} und sehen Tagebuch, Ziele und Fährten.", { hund: dogName })}
      </p>
      <ul className="flex flex-col gap-3">
        {betreuungen.map((b) => (
          <li key={b.trainerId} className="flex flex-col gap-2">
            <div className="min-w-0 text-sm [overflow-wrap:anywhere]">
              <p className="font-medium">{b.trainerName}</p>
              <p className="text-muted-foreground">
                {t("seit {datum}", { datum: datumKurz(b.startDate, sprache) })}
                {b.groupName ? ` · ${t("Gruppe {gruppe}", { gruppe: b.groupName })}` : ""}
              </p>
            </div>
            <Button
              variant="outline"
              className="min-h-11 w-full text-destructive hover:text-destructive"
              disabled={laeuft === b.trainerId}
              onClick={() => beenden(b)}
            >
              {t("Betreuung beenden")}
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}

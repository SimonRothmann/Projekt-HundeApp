"use client";

import { useState } from "react";
import { CirclePause } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { DogPause } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { pausenZeile } from "@/lib/pausen";
import { useSprache, useT } from "@/lib/i18n";

/**
 * Das Band oben auf der Hundeseite, solange der Hund pausiert: "Pause:
 * verletzt · bis 03.11." bzw. "… bis auf Weiteres", daneben "Pause beenden".
 *
 * Besitzer:innen (auch Mitbesitzer:innen) sehen den Grund und können die Pause
 * beenden. Eine betreuende Trainer:in liest nur "Pause bis 03.11." - der Server
 * liefert ihr den Grund gar nicht aus (siehe DogPauseDto) - und hat keinen
 * Knopf: Die Pause gehört der Familie.
 */
export function PauseBand({
  dogId,
  pause,
  darfBeenden,
  onChanged,
}: {
  dogId: string;
  pause: DogPause;
  /** Nur Besitzer:innen und Mitbesitzer:innen. */
  darfBeenden: boolean;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const sprache = useSprache();
  const [laeuft, setLaeuft] = useState(false);

  async function beenden() {
    setLaeuft(true);
    try {
      await api.put(`/api/dogs/${dogId}/pausen/${pause.id}/beenden`);
      toast.success(t("Pause beendet."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setLaeuft(false);
    }
  }

  return (
    <div
      role="status"
      className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm"
    >
      <p className="flex min-w-0 flex-1 basis-32 items-start gap-2 font-medium text-amber-800 dark:text-amber-300">
        <CirclePause className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span className="min-w-0 [overflow-wrap:anywhere]">{pausenZeile(t, pause, sprache)}</span>
      </p>
      {darfBeenden && (
        <Button type="button" variant="outline" size="sm" className="coarse:min-h-11" disabled={laeuft} onClick={beenden}>
          {laeuft ? t("Beendet…") : t("Pause beenden")}
        </Button>
      )}
    </div>
  );
}

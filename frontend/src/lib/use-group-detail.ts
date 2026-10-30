"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { GroupDetail } from "@/lib/types";

/**
 * Die Gruppe samt Mitgliedern, Trainer:innen und Einladungen - gemeinsam für
 * die Gruppenseite und deren Einstellungen. `laden` holt sie nach einer
 * Änderung neu.
 */
export function useGroupDetail(groupId: string) {
  const t = useT();
  const [detail, setDetail] = useState<GroupDetail | null>(null);
  // Ohne das bliebe die Seite bei "Lädt…" stehen, etwa wenn ein alter Link auf
  // eine Gruppe zeigt, die jemand mit der Rolle Training nicht mehr verwaltet.
  const [fehlgeschlagen, setFehlgeschlagen] = useState(false);

  const laden = useCallback(async () => {
    try {
      setDetail(await api.get<GroupDetail>(`/api/groups/${groupId}`));
      setFehlgeschlagen(false);
    } catch (err) {
      setFehlgeschlagen(true);
      toast.error(err instanceof ApiError ? err.message : t("Gruppe konnte nicht geladen werden."));
    }
    // t bewusst nicht in der Liste: nur für die Fehlermeldung.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden]);

  return { detail, laden, fehlgeschlagen };
}

"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { GroupDetail } from "@/lib/types";

/**
 * Warum die Gruppe nicht kam: Der Server sagt "gibt es nicht / darfst du nicht" (404, 403) -
 * alles andere (Netz, Serverfehler, Drosselung) ist ein Ladefehler, der sich wiederholen lässt.
 */
export function ladefehlerArt(err: unknown): "nichtVerfuegbar" | "ladefehler" {
  return err instanceof ApiError && (err.status === 404 || err.status === 403) ? "nichtVerfuegbar" : "ladefehler";
}

/**
 * Die Gruppe samt Mitgliedern, Trainer:innen und Einladungen - gemeinsam für
 * die Gruppenseite und deren Einstellungen. `laden` holt sie nach einer
 * Änderung neu.
 */
export function useGroupDetail(groupId: string) {
  const t = useT();
  const [detail, setDetail] = useState<GroupDetail | null>(null);
  // Ohne das bliebe die Seite bei "Lädt…" stehen. Zwei verschiedene Gründe: Der Server sagt "gibt es
  // nicht / darfst du nicht" (404, 403 - etwa ein alter Link auf eine Gruppe, die jemand mit der
  // Rolle Training nicht mehr verwaltet), oder das Laden scheiterte (Netz, Serverfehler) und lässt
  // sich wiederholen. Die Aussage "kannst du nicht öffnen" gilt nur für den ersten.
  const [fehler, setFehler] = useState<"nichtVerfuegbar" | "ladefehler" | null>(null);

  const laden = useCallback(async () => {
    try {
      setDetail(await api.get<GroupDetail>(`/api/groups/${groupId}`));
      setFehler(null);
    } catch (err) {
      setFehler(ladefehlerArt(err));
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

  /** Nach einem Ladefehler: zurück zu "Lädt…" und noch einmal versuchen. */
  function nochmal() {
    setFehler(null);
    void laden();
  }

  return { detail, laden, fehlgeschlagen: fehler === "nichtVerfuegbar", ladefehler: fehler === "ladefehler", nochmal };
}

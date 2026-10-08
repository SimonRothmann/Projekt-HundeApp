"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";
import type { Profile } from "@/lib/types";

/**
 * Die Profildaten des Kontos (Name, E-Mail, Avatar). Name und E-Mail kennt
 * auch der Anmelde-Zustand; der Avatar nicht - deshalb der eigene Abruf.
 */
export function useProfil() {
  const t = useT();
  const { user } = useAuth();
  const [profil, setProfil] = useState<Profile | null>(null);

  useEffect(() => {
    if (!user) return;
    api
      .get<Profile>("/api/profile")
      .then(setProfil)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : t("Profil konnte nicht geladen werden.")));
    // t bewusst nicht in der Liste: Der Uebersetzer wird hier nur im
    // Fehlerfall gebraucht. Stuende er drin, liefe der ganze Abruf bei
    // jedem Sprachwechsel erneut - Daten neu laden, weil ein Toast
    // anders heissen wuerde.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user]);

  return { profil, setProfil };
}

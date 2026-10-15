"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { AdminStats } from "@/lib/types";
import { KennzahlenKarte } from "@/components/admin/kennzahlen-karte";
import { Letzte30TageSection } from "@/components/admin/letzte-30-tage-section";
import { Button } from "@/components/ui/button";

export default function AdminStatistikPage() {
  const t = useT();
  const [stats, setStats] = useState<AdminStats | null>(null);
  // Fehlertext statt ewigem "Lädt…" - die Seite hat sonst nichts zu zeigen.
  const [fehler, setFehler] = useState<string | null>(null);
  const [versuch, setVersuch] = useState(0);

  useEffect(() => {
    let aktuell = true;
    api
      .get<AdminStats>("/api/admin/stats")
      .then((daten) => {
        if (aktuell) setStats(daten);
      })
      .catch((err) => {
        if (aktuell) setFehler(err instanceof ApiError ? err.message : t("Statistik konnte nicht geladen werden."));
      });
    return () => {
      aktuell = false;
    };
    // t bewusst nicht in der Liste: nur für den Fehlerfall, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [versuch]);

  function erneut() {
    setFehler(null);
    setVersuch((v) => v + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Statistik")}</h1>
      {fehler ? (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{fehler}</p>
          <Button variant="outline" className="h-11 self-start" onClick={erneut}>
            {t("Erneut versuchen")}
          </Button>
        </div>
      ) : stats === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : (
        <>
          <KennzahlenKarte stats={stats} />
          <Letzte30TageSection stats={stats} />
        </>
      )}
    </div>
  );
}

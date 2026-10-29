"use client";

import { useEffect, useState } from "react";
import { BarChart3 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { AdminUsage } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { SectionHeading } from "@/components/ui/section-heading";
import { NutzungAktivitaetSection } from "@/components/admin/nutzung-aktivitaet-section";
import { NutzungBindungSection } from "@/components/admin/nutzung-bindung-section";
import { NutzungFunktionenSection } from "@/components/admin/nutzung-funktionen-section";
import { NutzungModuleSection } from "@/components/admin/nutzung-module-section";
import { NutzungSportartenSection } from "@/components/admin/nutzung-sportarten-section";
import { NutzungWannSection } from "@/components/admin/nutzung-wann-section";

/**
 * "Wie und was wird genutzt" unter den Kennzahlen der Statistik-Seite.
 *
 * Lädt für sich (eigener Aufruf, eigener Fehler): Scheitert er oder dauert er,
 * bleiben die Kennzahlen darüber stehen. Nur Zählungen aus vorhandenen Daten
 * (siehe AdminUsageService) - nichts im Browser gemessen.
 */
export function NutzungBereich() {
  const t = useT();
  const [nutzung, setNutzung] = useState<AdminUsage | null>(null);
  const [fehler, setFehler] = useState<string | null>(null);
  const [versuch, setVersuch] = useState(0);

  useEffect(() => {
    let aktuell = true;
    api
      .get<AdminUsage>("/api/admin/stats/usage")
      .then((daten) => {
        if (aktuell) setNutzung(daten);
      })
      .catch((err) => {
        if (aktuell) setFehler(err instanceof ApiError ? err.message : t("Nutzung konnte nicht geladen werden."));
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
    <section className="flex flex-col gap-4">
      <SectionHeading icon={BarChart3} title={t("Nutzung")} />
      {fehler ? (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{fehler}</p>
          <Button variant="outline" className="h-11 self-start" onClick={erneut}>
            {t("Erneut versuchen")}
          </Button>
        </div>
      ) : nutzung === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : (
        <>
          <NutzungAktivitaetSection activity={nutzung.activity} />
          <NutzungFunktionenSection features={nutzung.features} />
          <NutzungModuleSection module={nutzung.disabledModules} gesamt={nutzung.activity.totalAccounts} />
          <NutzungBindungSection retention={nutzung.retention} gesamt={nutzung.activity.totalAccounts} />
          <NutzungWannSection timing={nutzung.timing} />
          <NutzungSportartenSection sports={nutzung.sports} />
        </>
      )}
    </section>
  );
}

"use client";

import { Button } from "@/components/ui/button";
import { useT } from "@/lib/i18n";

/**
 * Was die Gruppenseiten zeigen, solange die Gruppe nicht da ist (useGroupDetail): "Lädt…", der
 * Satz für eine Gruppe, die es nicht gibt oder die nicht zu deinen gehört, oder - nach einem
 * Netz- oder Serverfehler - die Meldung mit "Erneut versuchen".
 */
export function GruppeNichtGeladen({
  fehlgeschlagen,
  ladefehler,
  onNochmal,
}: {
  fehlgeschlagen: boolean;
  ladefehler: boolean;
  onNochmal: () => void;
}) {
  const t = useT();
  if (ladefehler) {
    return (
      <div className="flex flex-wrap items-center gap-3">
        <p className="min-w-0 break-words text-muted-foreground">{t("Gruppe konnte nicht geladen werden.")}</p>
        <Button type="button" size="sm" variant="outline" className="coarse:min-h-11" onClick={onNochmal}>
          {t("Erneut versuchen")}
        </Button>
      </div>
    );
  }
  if (!fehlgeschlagen) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true">
        <span className="sr-only">{t("Lädt…")}</span>
        <div className="h-12 w-2/3 rounded-xl bg-card motion-safe:animate-pulse" />
        <div className="h-36 rounded-xl bg-card motion-safe:animate-pulse" />
      </div>
    );
  }
  return (
    <p className="break-words text-muted-foreground">
      {t("Diese Gruppe kannst du nicht öffnen: Es gibt sie nicht mehr, oder sie gehört nicht zu deinen Gruppen.")}
    </p>
  );
}

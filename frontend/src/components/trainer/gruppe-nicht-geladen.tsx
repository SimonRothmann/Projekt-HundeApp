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
  return (
    <p className="break-words text-muted-foreground">
      {fehlgeschlagen ? t("Diese Gruppe kannst du nicht öffnen: Es gibt sie nicht mehr, oder sie gehört nicht zu deinen Gruppen.") : t("Lädt…")}
    </p>
  );
}

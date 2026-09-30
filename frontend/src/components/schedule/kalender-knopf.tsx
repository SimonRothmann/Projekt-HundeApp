"use client";

import { CalendarPlus } from "lucide-react";
import { toast } from "sonner";
import type { GroupTrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { inKalenderUebergeben, kannInKalender } from "@/lib/kalender";
import { useT } from "@/lib/i18n";

/**
 * "Zum Kalender": legt den Termin in den Kalender des Geräts. Nur für
 * geplante Termine, die noch bevorstehen - ein abgesagter oder vergangener
 * gehört nicht hinein; der Knopf fehlt dann ganz, statt ins Leere zu führen.
 *
 * Die Datei enthält nur Gruppe, Zeit, Ort und Dauer - keine Namen
 * (siehe lib/kalender.ts).
 */
export function KalenderKnopf({ termin, className }: { termin: GroupTrainingSession; className?: string }) {
  const t = useT();
  if (!kannInKalender(termin)) return null;

  async function uebergeben() {
    try {
      const ergebnis = await inKalenderUebergeben({
        id: termin.id,
        startsAt: termin.startsAt,
        durationMinutes: termin.durationMinutes,
        titel: t("{gruppe}: Gruppentraining", { gruppe: termin.groupName }),
        ort: termin.location,
        beschreibung: t("Gruppentraining mit Dogity. Dauer: {minuten} Min.", { minuten: termin.durationMinutes }),
      });
      if (ergebnis === "geladen") toast.success(t("Kalenderdatei heruntergeladen. Öffne sie, um den Termin zu übernehmen."));
    } catch {
      toast.error(t("Der Termin konnte nicht an den Kalender übergeben werden."));
    }
  }

  return (
    <Button type="button" size="sm" variant="outline" className={className} onClick={uebergeben}>
      <CalendarPlus className="size-4" />
      {t("Zum Kalender")}
    </Button>
  );
}

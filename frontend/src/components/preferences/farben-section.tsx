"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { usePreferences } from "@/lib/preferences-context";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { SchalterZeile } from "@/components/ui/switch";
import { ListenGruppe } from "@/components/profile/einstellungs-liste";
import { Palette } from "lucide-react";
import { toast } from "sonner";
import { useT } from "@/lib/i18n";

/** "Farbsichere Darstellung" (lib/farbsicher.ts): Eine Einstellung je Konto, gespeichert wie die Schriftgröße. */
export function FarbenSection() {
  const { preferences, reload } = usePreferences();
  const [speichert, setSpeichert] = useState(false);
  const t = useT();

  async function umschalten(an: boolean) {
    setSpeichert(true);
    try {
      await api.put("/api/preferences/color-safe", { enabled: an });
      await reload();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einstellung konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Palette className="size-5" />
          {t("Farben")}
        </CardTitle>
        <CardDescription>
          {t("Für Rot-Grün-Schwäche: Ampeln auf Karte, Auswertung und Verlauf in Blau, Gelb und Rot statt Grün, Orange und Rot, Erfolg in Blau statt Grün.")}
        </CardDescription>
      </CardHeader>

      <CardContent className="flex min-w-0 flex-col gap-3">
        <ListenGruppe>
          <SchalterZeile
            an={preferences.colorSafe}
            disabled={speichert}
            onChange={(an) => void umschalten(an)}
            titel={t("Farbsichere Darstellung")}
          />
        </ListenGruppe>

        <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{t("Gilt für dein Konto auf jedem Gerät.")}</p>
      </CardContent>
    </Card>
  );
}

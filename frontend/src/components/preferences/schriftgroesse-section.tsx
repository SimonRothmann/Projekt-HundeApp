"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { usePreferences } from "@/lib/preferences-context";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Type } from "lucide-react";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import {
  bestimmeSchriftgroesse,
  SCHRIFTGROESSEN,
  VORGABE_SCHRIFT,
  type Schriftgroesse,
} from "@/lib/schriftgroesse";

/**
 * Beschriftung und Vorschaugröße je Stufe.
 *
 * Die Vorschau ist kein Zierrat: Wer die Schrift größer braucht, kann die
 * Beschriftung der Stufe schlecht lesen, mit der er noch nicht arbeitet.
 * Jede Schaltfläche zeigt darum ihre eigene Größe.
 */
const STUFEN: Record<Schriftgroesse, { label: string; vorschau: string }> = {
  normal: { label: uebersetzbar("Normal"), vorschau: "text-sm" },
  gross: { label: uebersetzbar("Groß"), vorschau: "text-base" },
  "sehr-gross": { label: uebersetzbar("Sehr groß"), vorschau: "text-lg" },
};

export function SchriftgroesseSection() {
  const { preferences, reload } = usePreferences();
  const [speichert, setSpeichert] = useState(false);
  const t = useT();

  const gewaehlt = bestimmeSchriftgroesse(preferences.fontScale);

  async function waehlen(stufe: Schriftgroesse) {
    setSpeichert(true);
    try {
      // Leer statt "normal" für die Vorgabe: So bleibt in der Datenbank
      // erkennbar, wer nie etwas eingestellt hat.
      await api.put("/api/preferences/font-scale", {
        fontScale: stufe === VORGABE_SCHRIFT ? null : stufe,
      });
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
          <Type className="size-5" />
          {t("Schriftgröße")}
        </CardTitle>
        <CardDescription>
          {t("Gilt für die ganze App. Knöpfe und Abstände wachsen mit, nicht nur die Buchstaben.")}
        </CardDescription>
      </CardHeader>

      <CardContent className="flex min-w-0 flex-col gap-3">
        <div className="flex flex-wrap gap-1.5">
          {SCHRIFTGROESSEN.map((stufe) => (
            <button
              key={stufe}
              type="button"
              aria-pressed={gewaehlt === stufe}
              disabled={speichert}
              onClick={() => void waehlen(stufe)}
              className={cn(
                "inline-flex min-h-11 items-center rounded-full border px-4 transition-colors disabled:opacity-60",
                STUFEN[stufe].vorschau,
                gewaehlt === stufe
                  ? "border-primary bg-primary/10 font-medium text-foreground"
                  : "border-border text-muted-foreground hover:border-foreground/30 hover:text-foreground",
              )}
            >
              {t(STUFEN[stufe].label)}
            </button>
          ))}
        </div>

        <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
          {t("Die Einstellung gehört zum Konto und gilt auf jedem Gerät. Hat der Browser bereits eine größere Schrift eingestellt, bleibt dieser Vorsprung erhalten.")}
        </p>
      </CardContent>
    </Card>
  );
}

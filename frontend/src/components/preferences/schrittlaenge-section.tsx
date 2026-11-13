"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { usePreferences } from "@/lib/preferences-context";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Footprints } from "lucide-react";
import { toast } from "sonner";
import { useT } from "@/lib/i18n";
import { gueltigeSchrittlaenge, SCHRITT_MAX_CM, SCHRITT_MIN_CM, STANDARD_SCHRITT_CM } from "@/lib/schritte";

/**
 * Die Schrittlänge, mit der Dogity die Länge einer Fährte in Schritte umrechnet - Prüfungsordnungen rechnen
 * in Schritten. Gehört zur Person und gilt auf jedem Gerät. Leer = Standard; erlaubt sind 40 bis 120 cm
 * (der Server prüft es noch einmal).
 */
export function SchrittlaengeSection() {
  const t = useT();
  const { preferences, reload } = usePreferences();
  const [speichert, setSpeichert] = useState(false);
  // Was gerade im Feld steht, solange es vom gespeicherten Wert abweicht; sonst null.
  const [entwurf, setEntwurf] = useState<string | null>(null);

  const gespeichert = preferences.stepLengthCm;
  const text = entwurf ?? (gespeichert != null ? String(gespeichert) : "");
  const zahl = text.trim() === "" ? null : Number(text);
  const fehler = zahl != null && !gueltigeSchrittlaenge(zahl);

  async function speichern(wert: number | null) {
    setSpeichert(true);
    try {
      await api.put("/api/preferences/step-length", { stepLengthCm: wert });
      await reload();
      setEntwurf(null);
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
          <Footprints className="size-5" />
          {t("Schrittlänge")}
        </CardTitle>
        <CardDescription>
          {t("Für die Umrechnung der Fährtenlänge in Schritte – Prüfungsordnungen rechnen in Schritten.")}
        </CardDescription>
      </CardHeader>

      <CardContent className="flex min-w-0 flex-col gap-3">
        <div className="flex min-w-0 flex-col gap-1.5">
          <Label htmlFor="schrittlaenge" className="text-sm text-muted-foreground">
            {t("Schrittlänge (cm)")}
          </Label>
          <div className="flex flex-wrap items-center gap-2">
            <Input
              id="schrittlaenge"
              type="number"
              inputMode="numeric"
              min={SCHRITT_MIN_CM}
              max={SCHRITT_MAX_CM}
              step={1}
              placeholder={String(STANDARD_SCHRITT_CM)}
              value={text}
              aria-invalid={fehler}
              onChange={(e) => setEntwurf(e.target.value)}
              className="w-28 flex-none coarse:h-11"
            />
            <Button
              type="button"
              size="sm"
              className="coarse:min-h-11"
              disabled={speichert || entwurf === null || fehler}
              onClick={() => void speichern(zahl)}
            >
              {t("Speichern")}
            </Button>
            {gespeichert != null && entwurf === null && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                className="coarse:min-h-11"
                disabled={speichert}
                onClick={() => void speichern(null)}
              >
                {t("Standard")}
              </Button>
            )}
          </div>
          {fehler && (
            <p className="text-xs text-destructive">
              {t("Die Schrittlänge muss zwischen {min} und {max} cm liegen.", { min: SCHRITT_MIN_CM, max: SCHRITT_MAX_CM })}
            </p>
          )}
        </div>

        <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
          {t("Ohne Angabe rechnet Dogity mit {cm} cm.", { cm: STANDARD_SCHRITT_CM })}
        </p>
      </CardContent>
    </Card>
  );
}

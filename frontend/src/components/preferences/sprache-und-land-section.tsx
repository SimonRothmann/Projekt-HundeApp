"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Country } from "@/lib/types";
import { usePreferences } from "@/lib/preferences-context";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Globe, Languages } from "lucide-react";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import { useSprache, useT } from "@/lib/i18n";
import { SPRACHE_NAME, SPRACHEN } from "@/lib/i18n/sprachen";
import { landName, VORGABE_LAND } from "@/lib/i18n/laender";

/**
 * Sprache und Geltungsbereich - zwei Einstellungen, bewusst nebeneinander
 * und bewusst nicht voneinander abgeleitet.
 *
 * Die Versuchung wäre, aus der Sprache das Land zu schließen. Sie führt in
 * die Irre: Wer in Deutschland trainiert und die App auf Englisch nutzt,
 * braucht weiterhin die deutschen Prüfungsordnungen - die BH bleibt die BH.
 * Und wer in Österreich lebt, spricht dieselbe Sprache, hat aber einen
 * anderen Verband.
 *
 * Deshalb der gemeinsame Kasten mit zwei getrennten Reglern: Die Nähe
 * erklärt den Zusammenhang, die Trennung verhindert den Kurzschluss.
 */
export function SpracheUndLandSection() {
  const { preferences, reload } = usePreferences();
  const sprache = useSprache();
  const t = useT();
  const [laender, setLaender] = useState<Country[] | null>(null);
  const [speichert, setSpeichert] = useState(false);

  useEffect(() => {
    // Externe Quelle (REST). Fällt sie aus, bleibt die Länderauswahl weg -
    // die Sprachauswahl darüber funktioniert trotzdem.
    api
      .get<Country[]>("/api/sports/countries")
      .then(setLaender)
      .catch(() => setLaender([]));
  }, []);

  async function speichern(aktion: () => Promise<unknown>) {
    setSpeichert(true);
    try {
      await aktion();
      await reload();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einstellung konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  const gewaehltesLand = preferences.country ?? VORGABE_LAND;
  const land = laender?.find((l) => l.code === gewaehltesLand);
  const leer = land !== undefined && land.regulationCount === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Languages className="size-5" />
          {t("Sprache & Geltungsbereich")}
        </CardTitle>
        <CardDescription>
          {t("Die Oberfläche hat eine Sprache, der Prüfungskatalog einen Geltungsbereich. Beides wird getrennt gewählt.")}
        </CardDescription>
      </CardHeader>

      <CardContent className="flex flex-col gap-5">
        <div className="flex flex-col gap-2">
          <p className="text-sm font-medium">{t("Sprache der Oberfläche")}</p>
          <div className="flex flex-wrap gap-1.5">
            {SPRACHEN.map((code) => (
              <Wahl
                key={code}
                aktiv={sprache === code}
                disabled={speichert}
                onClick={() => void speichern(() => api.put("/api/preferences/locale", { locale: code }))}
              >
                {/* Der Sprachname steht in seiner eigenen Sprache. "German"
                    hilft niemandem, der nur Deutsch liest. */}
                {SPRACHE_NAME[code]}
              </Wahl>
            ))}
          </div>
          <p className="text-xs text-muted-foreground">
            {t("Prüfungsordnungen und Sachkundefragen bleiben in ihrer Ursprungssprache - eine übersetzte Prüfungsfrage wäre für die Prüfung wertlos.")}
          </p>
        </div>

        <div className="flex flex-col gap-2 border-t pt-4">
          <p className="flex items-center gap-2 text-sm font-medium">
            <Globe className="size-4 text-muted-foreground" />
            {t("Geltungsbereich der Prüfungsordnungen")}
          </p>

          {laender === null ? (
            <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
          ) : laender.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("Die Länderliste konnte nicht geladen werden.")}</p>
          ) : (
            <>
              <div className="flex flex-wrap gap-1.5">
                {laender.map((l) => (
                  <Wahl
                    key={l.code}
                    aktiv={l.code === gewaehltesLand}
                    disabled={speichert}
                    onClick={() => void speichern(() => api.put("/api/preferences/country", { country: l.code }))}
                  >
                    {landName(l.code, sprache)}
                    {l.regulationCount === 0 && (
                      <span className="ml-1.5 text-xs opacity-60">{t("leer")}</span>
                    )}
                  </Wahl>
                ))}
              </div>

              {/* Ein leeres Land ist ein Merkmal, kein Defekt - aber nur,
                  wenn man es ausspricht. Ohne diesen Satz stünde jemand vor
                  einem Bildschirm, der kaputt aussieht. */}
              {leer ? (
                <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
                  {t("Für dieses Land sind noch keine Prüfungsordnungen hinterlegt. Tagebuch, Fährte, Trainingsplanung und Verein funktionieren davon unabhängig vollständig - nur der Prüfungskatalog bleibt leer.")}
                </p>
              ) : (
                <p className="text-xs text-muted-foreground">
                  {t("Es werden die Prüfungsordnungen dieses Landes angeboten, dazu die international gültigen.")}
                </p>
              )}
            </>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * Derselbe Knopf wie bei Modulen und Sportarten (condition-picker-Muster):
 * gedrückter Zustand über aria-pressed und Randfarbe, Mindesthöhe für grobe
 * Zeiger.
 */
function Wahl({
  aktiv,
  disabled,
  onClick,
  children,
}: {
  aktiv: boolean;
  disabled?: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      aria-pressed={aktiv}
      disabled={disabled}
      onClick={onClick}
      className={cn(
        "inline-flex min-h-9 items-center rounded-full border px-3 text-sm transition-colors disabled:opacity-60",
        aktiv
          ? "border-primary bg-primary/10 font-medium text-foreground"
          : "border-border text-muted-foreground hover:border-foreground/30 hover:text-foreground",
      )}
    >
      {children}
    </button>
  );
}

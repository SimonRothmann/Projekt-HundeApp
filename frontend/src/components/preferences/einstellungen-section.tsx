"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Sport } from "@/lib/types";
import { usePreferences } from "@/lib/preferences-context";
import { useAuth } from "@/lib/auth-context";
import { Card, CardContent, CardDescription, CardHeader } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { SchalterZeile } from "@/components/ui/switch";
import { ListenGruppe } from "@/components/profile/einstellungs-liste";
import { Check } from "lucide-react";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
import { sichtbareModule } from "@/lib/profil";

/**
 * Module und Sportarten ein- und ausblenden.
 *
 * Zwei Abschnitte, weil dahinter zwei verschiedene Modelle stehen (siehe
 * docs/VERBAENDE_SPRACHEN_MODULE.md): Module werden ABGEWÄHLT - so erscheint
 * ein künftig hinzukommendes Modul bei allen von selbst, ohne dass jemand
 * etwas anfassen muss (Texte und Zählung: lib/profil.ts). Sportarten werden
 * AUSGEWÄHLT - die Aussage ist "ich mache genau das"; nichts ausgewählt heißt
 * "alle".
 *
 * Module sind eine Liste mit Schaltern; Sportarten sind Chips nach dem Muster
 * der Verfassungsauswahl (condition-picker): gedrückter Zustand über
 * aria-pressed und Randfarbe, Mindesthöhe für grobe Zeiger.
 */
function Umschalter({
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
      disabled={disabled}
      aria-pressed={aktiv}
      onClick={onClick}
      className={cn(
        "flex max-w-full shrink-0 items-center gap-1.5 rounded-full border px-3 py-1.5 text-left text-sm transition-colors [overflow-wrap:anywhere] coarse:min-h-11 disabled:opacity-50",
        aktiv
          ? "border-primary bg-primary/15 text-primary-text"
          : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
      )}
    >
      {aktiv && <Check className="size-4 shrink-0" />}
      {children}
    </button>
  );
}

/** Hook-Teil, den beide Abschnitte teilen: speichern, Einstellungen neu laden, Fehler melden. */
function useSpeichern() {
  const { reload } = usePreferences();
  const [speichert, setSpeichert] = useState(false);
  const t = useT();

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

  return { speichert, speichern };
}

export function FunktionenSection() {
  const { preferences } = usePreferences();
  const { isTrainer } = useAuth();
  const { speichert, speichern } = useSpeichern();
  const t = useT();

  function modulUmschalten(key: string, anJetzt: boolean) {
    // Gespeichert wird die ABWAHL: Wer einschaltet, entfernt den Schlüssel.
    const abgewaehlt = anJetzt
      ? preferences.disabledModules.filter((k) => k !== key)
      : [...preferences.disabledModules, key];
    void speichern(() => api.put("/api/preferences/modules", { disabledModules: abgewaehlt }));
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted-foreground">
        {t("Was du nicht brauchst, kannst du ausblenden. Alles ist von Haus aus eingeschaltet.")}
      </p>
      <ListenGruppe>
        {sichtbareModule(isTrainer).map((m) => (
          <SchalterZeile
            key={m.key}
            an={!preferences.disabledModules.includes(m.key)}
            disabled={speichert}
            onChange={(an) => modulUmschalten(m.key, an)}
            titel={t(m.titel)}
            beschreibung={t(m.beschreibung)}
          />
        ))}
      </ListenGruppe>
    </div>
  );
}

export function SportartenSection() {
  const { preferences } = usePreferences();
  const { speichert, speichern } = useSpeichern();
  const [sports, setSports] = useState<Sport[] | null>(null);
  const t = useT();

  useEffect(() => {
    api
      .get<Sport[]>("/api/sports")
      .then(setSports)
      .catch(() => setSports([]));
  }, []);

  function sportartUmschalten(sportId: string, ausgewaehlt: boolean) {
    const ids = ausgewaehlt
      ? preferences.sportIds.filter((id) => id !== sportId)
      : [...preferences.sportIds, sportId];
    void speichern(() => api.put("/api/preferences/sports", { sportIds: ids }));
  }

  const alleSportarten = preferences.sportIds.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardDescription>
          {alleSportarten
            ? t("Zurzeit werden dir alle Sportarten angeboten. Wähle aus, was du machst – dann zeigt das Tagebuch nur noch diese und Freitext.")
            : t("Im Tagebuch werden dir nur die ausgewählten Sportarten angeboten, dazu immer Freitext.")}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {sports === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <>
            <div className="flex flex-wrap gap-1.5">
              {sports.map((s) => {
                const gewaehlt = preferences.sportIds.includes(s.id);
                return (
                  <Umschalter
                    key={s.id}
                    aktiv={gewaehlt}
                    disabled={speichert}
                    onClick={() => sportartUmschalten(s.id, gewaehlt)}
                  >
                    {s.name}
                  </Umschalter>
                );
              })}
            </div>
            {!alleSportarten && (
              <Button
                variant="ghost"
                size="sm"
                className="self-start coarse:min-h-11"
                disabled={speichert}
                onClick={() => void speichern(() => api.put("/api/preferences/sports", { sportIds: [] }))}
              >
                {t("Auswahl aufheben (alle anzeigen)")}
              </Button>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

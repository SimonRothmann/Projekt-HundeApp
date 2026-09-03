"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { MODULE, type Sport } from "@/lib/types";
import { usePreferences } from "@/lib/preferences-context";
import { useAuth } from "@/lib/auth-context";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Check, SlidersHorizontal, Trophy } from "lucide-react";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { SpracheUndLandSection } from "@/components/preferences/sprache-und-land-section";
/**
 * Module und Sportarten ein- und ausblenden.
 *
 * Zwei Abschnitte, weil dahinter zwei verschiedene Modelle stehen (siehe
 * docs/VERBAENDE_SPRACHEN_MODULE.md): Module werden ABGEWÄHLT - so erscheint
 * ein künftig hinzukommendes Modul bei allen von selbst, ohne dass jemand
 * etwas anfassen muss. Sportarten werden AUSGEWÄHLT - die Aussage ist "ich
 * mache genau das"; nichts ausgewählt heißt "alle".
 *
 * Die Knöpfe folgen dem Muster der Verfassungsauswahl (condition-picker):
 * gedrückter Zustand über aria-pressed und Randfarbe, Mindesthöhe für grobe
 * Zeiger. Ein eigener Schalter-Baustein wäre ein neues Element im
 * Baukasten, für das es keinen zweiten Anwendungsfall gibt.
 */
const MODUL_TEXTE: { key: string; titel: string; beschreibung: string; nurTrainer?: boolean }[] = [
  {
    key: MODULE.faehrte,
    titel: uebersetzbar("Fährte & GPS"),
    beschreibung: uebersetzbar("Fährten aufzeichnen, ablaufen und auswerten."),
  },
  {
    key: MODULE.sachkunde,
    titel: uebersetzbar("Sachkunde"),
    beschreibung: uebersetzbar("Fragentrainer zur Begleithundeprüfung (SWHV, deutschsprachig)."),
  },
  {
    key: MODULE.gruppentraining,
    titel: uebersetzbar("Gruppentraining"),
    beschreibung: uebersetzbar("Einheiten und Terminplanung für Trainingsgruppen."),
    // Nur Trainer:innen sehen diesen Bereich überhaupt. Allen anderen einen
    // Schalter für etwas anzubieten, das sie nie hatten, verwirrt nur.
    nurTrainer: true,
  },
  { key: MODULE.wetter, titel: uebersetzbar("Wetter"), beschreibung: uebersetzbar("Temperatur und Wetter zum Training.") },
  { key: MODULE.statistik, titel: uebersetzbar("Statistik"), beschreibung: uebersetzbar("Auswertungen über Trainings und Verfassung.") },
];

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
        "flex shrink-0 items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm transition-colors coarse:min-h-10 disabled:opacity-50",
        aktiv
          ? "border-primary bg-primary/15 text-primary"
          : "border-border/60 text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
      )}
    >
      {aktiv && <Check className="size-4" />}
      {children}
    </button>
  );
}

export function EinstellungenSection() {
  const { preferences, reload } = usePreferences();
  const { isTrainer } = useAuth();
  const [sports, setSports] = useState<Sport[] | null>(null);
  const [speichert, setSpeichert] = useState(false);
  const t = useT();

  useEffect(() => {
    api
      .get<Sport[]>("/api/sports")
      .then(setSports)
      .catch(() => setSports([]));
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

  function modulUmschalten(key: string, anJetzt: boolean) {
    // Gespeichert wird die ABWAHL: Wer einschaltet, entfernt den Schlüssel.
    const abgewaehlt = anJetzt
      ? [...preferences.disabledModules, key]
      : preferences.disabledModules.filter((k) => k !== key);
    void speichern(() => api.put("/api/preferences/modules", { disabledModules: abgewaehlt }));
  }

  function sportartUmschalten(sportId: string, ausgewaehlt: boolean) {
    const ids = ausgewaehlt
      ? preferences.sportIds.filter((id) => id !== sportId)
      : [...preferences.sportIds, sportId];
    void speichern(() => api.put("/api/preferences/sports", { sportIds: ids }));
  }

  const alleSportarten = preferences.sportIds.length === 0;

  return (
    <>
      {/* Sprache und Geltungsbereich stehen zuoberst: Sie bestimmen, wie
          alles Weitere aussieht und was darin überhaupt vorkommt. */}
      <SpracheUndLandSection />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <SlidersHorizontal className="size-5" />
            {t("Funktionen")}
          </CardTitle>
          <CardDescription>
            {t("Was du nicht brauchst, kannst du ausblenden. Alles ist von Haus aus eingeschaltet.")}
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {MODUL_TEXTE.filter((m) => !m.nurTrainer || isTrainer).map((m) => {
            const an = !preferences.disabledModules.includes(m.key);
            return (
              <div key={m.key} className="flex flex-wrap items-center justify-between gap-2">
                <div className="min-w-[11rem] flex-1">
                  <p className="text-sm font-medium">{t(m.titel)}</p>
                  <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{t(m.beschreibung)}</p>
                </div>
                <Umschalter aktiv={an} disabled={speichert} onClick={() => modulUmschalten(m.key, an)}>
                  {an ? t("An") : t("Aus")}
                </Umschalter>
              </div>
            );
          })}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Trophy className="size-5" />
            {t("Meine Sportarten")}
          </CardTitle>
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
                  className="self-start"
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
    </>
  );
}

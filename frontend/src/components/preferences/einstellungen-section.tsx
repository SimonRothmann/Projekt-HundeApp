"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Sport } from "@/lib/types";
import { usePreferences } from "@/lib/preferences-context";
import { useAuth } from "@/lib/auth-context";
import { Card, CardContent, CardDescription, CardHeader } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { SchalterZeile } from "@/components/ui/switch";
import { ListenGruppe } from "@/components/profile/einstellungs-liste";
import { Check } from "lucide-react";
import { toast } from "sonner";

import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { sichtbareModule } from "@/lib/profil";
import { BenachrichtigungenSection } from "@/components/preferences/benachrichtigungen-section";
import { ErinnerungenSection } from "@/components/preferences/erinnerungen-section";

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
 * Module sind eine Liste mit Schaltern; Sportarten sind Auswahl-Chips (ui/chip).
 */
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
      <BenachrichtigungenSection />
      <ErinnerungenSection />
    </div>
  );
}

export function SportartenSection() {
  const { preferences } = usePreferences();
  const { speichert, speichern } = useSpeichern();
  const [sports, setSports] = useState<Sport[] | null>(null);
  const [ladefehler, setLadefehler] = useState(false);
  const t = useT();
  const sprache = useSprache();

  function laden() {
    api
      .get<Sport[]>("/api/sports")
      .then(setSports)
      // Ein Fehler ist keine leere Liste: Die Karte sagte sonst "alle Sportarten", obwohl nur die
      // Liste fehlt.
      .catch(() => setLadefehler(true));
  }

  useEffect(laden, []);

  function sportartUmschalten(sportId: string, ausgewaehlt: boolean) {
    const ids = ausgewaehlt
      ? preferences.sportIds.filter((id) => id !== sportId)
      : [...preferences.sportIds, sportId];
    void speichern(() => api.put("/api/preferences/sports", { sportIds: ids }));
  }

  // Eine gespeicherte Auswahl, die keine der angebotenen Sportarten trifft (etwa nach
  // einem Länderwechsel), wirkt wie keine Auswahl - das Tagebuch bietet dann alle an.
  const alleSportarten = sports ? !sports.some((s) => preferences.sportIds.includes(s.id)) : preferences.sportIds.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardDescription>
          {ladefehler ? t("Sportarten konnten nicht geladen werden.") : alleSportarten
            ? t("Zurzeit werden dir alle Sportarten angeboten. Wähle aus, was du machst – dann zeigt das Tagebuch nur noch diese und Freitext.")
            : t("Im Tagebuch werden dir nur die ausgewählten Sportarten angeboten, dazu immer Freitext.")}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {ladefehler ? (
          <Button
            variant="outline"
            size="sm"
            className="self-start coarse:min-h-11"
            onClick={() => {
              setLadefehler(false);
              laden();
            }}
          >
            {t("Erneut versuchen")}
          </Button>
        ) : sports === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <>
            <div className="flex flex-wrap gap-1.5">
              {sports.map((s) => {
                const gewaehlt = preferences.sportIds.includes(s.id);
                return (
                  <Chip
                    key={s.id}
                    kompakt
                    gewaehlt={gewaehlt}
                    disabled={speichert}
                    onClick={() => sportartUmschalten(s.id, gewaehlt)}
                    className="shrink-0"
                  >
                    {gewaehlt && <Check className="size-4 shrink-0" />}
                    {katalogName(s, sprache)}
                  </Chip>
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

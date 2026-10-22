"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { PAUSE_GRUND, type Dog, type DogPause, type PauseGrund } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  pausenStand,
  pausenZeitraumText,
  PAUSE_GRUND_TEXT,
  pruefePause,
  vorgeschlagenesEnde,
  waehlbareGruende,
  type PausenEingabe,
} from "@/lib/pausen";
import { datumKurz, heuteIso } from "@/lib/pruefung";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { useSprache, useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * Die Trainingspause im Verwalten-Menü des Hundes: eintragen (verletzt, krank,
 * bei Hündinnen läufig), laufende Pause beenden und der Verlauf vergangener
 * Pausen zum Bearbeiten und Löschen. Nur für Besitzer:innen - die Hundeseite
 * zeigt die Zeile gar nicht erst, wenn man den Hund nur betreut.
 *
 * Eine Pause hält den Trainingsplan an (siehe docs/SMART_TRAINING_PLAN.md,
 * "Pausen"). Gesperrt wird nichts: Training eintragen geht weiter.
 */
export function PausenSection({ dog, onChanged }: { dog: Dog; onChanged: () => Promise<void> }) {
  const t = useT();
  const sprache = useSprache();
  const [pausen, setPausen] = useState<DogPause[] | null>(null);
  const [ladefehler, setLadefehler] = useState(false);
  // null = kein Formular; "neu" = neue Pause; sonst die Pause, die bearbeitet wird.
  const [formular, setFormular] = useState<"neu" | DogPause | null>(null);

  const laden = useCallback(async () => {
    try {
      setPausen(await api.get<DogPause[]>(`/api/dogs/${dog.id}/pausen`));
      setLadefehler(false);
    } catch {
      setLadefehler(true);
    }
  }, [dog.id]);

  useEffect(() => {
    // Datenabruf beim Aufklappen (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden]);

  // Nach jeder Änderung: Verlauf und Hundeseite (Band, Plan) neu laden.
  async function nachAenderung() {
    setFormular(null);
    await laden();
    await onChanged();
  }

  async function beenden(pause: DogPause) {
    try {
      await api.put(`/api/dogs/${dog.id}/pausen/${pause.id}/beenden`);
      toast.success(t("Pause beendet."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    }
  }

  async function loeschen(pause: DogPause) {
    if (!window.confirm(t("Diese Pause wirklich löschen?"))) return;
    try {
      await api.delete(`/api/dogs/${dog.id}/pausen/${pause.id}`);
      toast.success(t("Pause gelöscht."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    }
  }

  const heute = heuteIso();

  return (
    <div className="flex min-w-0 flex-col gap-3 rounded-lg border border-surface-border bg-surface p-3">
      <p className="text-sm text-muted-foreground">
        {t("Eine Pause hält den Trainingsplan an. Training eintragen geht trotzdem, zum Beispiel für leichtes Training oder Physio.")}
      </p>

      {formular === null ? (
        <Button type="button" variant="outline" className="min-h-11 w-full" onClick={() => setFormular("neu")}>
          <Plus className="size-4" aria-hidden />
          {t("Pause eintragen")}
        </Button>
      ) : (
        // key: Beim Wechsel zwischen "neu" und einer Pause beginnt das Formular von vorn.
        <PauseFormular
          key={formular === "neu" ? "neu" : formular.id}
          dog={dog}
          pause={formular === "neu" ? null : formular}
          onGespeichert={nachAenderung}
          onAbbrechen={() => setFormular(null)}
        />
      )}

      <div className="flex min-w-0 flex-col gap-2">
        <p className="text-sm font-medium">{t("Verlauf")}</p>
        {ladefehler && pausen === null ? (
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm text-muted-foreground">{t("Pausen konnten nicht geladen werden.")}</p>
            <Button type="button" size="sm" variant="outline" className="coarse:min-h-11" onClick={() => void laden()}>
              {t("Erneut versuchen")}
            </Button>
          </div>
        ) : pausen === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : pausen.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Noch keine Pausen eingetragen.")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {pausen.map((pause) => {
              const stand = pausenStand(pause, heute);
              return (
                <li key={pause.id} className="flex min-w-0 flex-col gap-2 rounded-md border border-surface-border p-2.5">
                  <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
                    <span className="font-medium">{pause.reason !== null ? t(PAUSE_GRUND_TEXT[pause.reason]) : ""}</span>
                    {stand === "laufend" && <Badge variant="secondary">{t("läuft")}</Badge>}
                    {stand === "geplant" && <Badge variant="outline">{t("geplant")}</Badge>}
                  </div>
                  <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
                    {pausenZeitraumText(t, pause, (iso) => datumKurz(iso, sprache))}
                  </p>
                  {pause.note && <p className="text-sm [overflow-wrap:anywhere]">{pause.note}</p>}
                  <div className="flex flex-wrap gap-2">
                    {stand !== "vorbei" && (
                      <Button type="button" size="sm" variant="outline" className="coarse:min-h-11" onClick={() => beenden(pause)}>
                        {stand === "laufend" ? t("Pause beenden") : t("Zurücknehmen")}
                      </Button>
                    )}
                    <Button type="button" size="sm" variant="ghost" className="coarse:min-h-11" onClick={() => setFormular(pause)}>
                      <Pencil className="size-3.5" aria-hidden />
                      {t("Bearbeiten")}
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      className="text-destructive hover:text-destructive coarse:min-h-11"
                      onClick={() => loeschen(pause)}
                    >
                      <Trash2 className="size-3.5" aria-hidden />
                      {t("Löschen")}
                    </Button>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}

/**
 * Das Formular für eine neue oder zu ändernde Pause: Grund (Läufigkeit nur bei
 * Hündinnen), Beginn, Ende (leer = bis auf Weiteres) und eine kurze Notiz. Bei
 * Läufigkeit steht als Ende zunächst Beginn + 21 Tage - ein Vorschlag, den die
 * Person ändern kann und der danach nicht wieder überschrieben wird.
 */
function PauseFormular({
  dog,
  pause,
  onGespeichert,
  onAbbrechen,
}: {
  dog: Dog;
  pause: DogPause | null;
  onGespeichert: () => Promise<void>;
  onAbbrechen: () => void;
}) {
  const t = useT();
  const [eingabe, setEingabe] = useState<PausenEingabe>(() => ({
    grund: pause?.reason ?? null,
    von: pause?.startDate ?? heuteIso(),
    bis: pause?.endDate ?? "",
    notiz: pause?.note ?? "",
  }));
  // Eine bestehende Pause hat ihr Ende schon; bei einer neuen gilt der Vorschlag, bis jemand es anfasst.
  const [bisVonHand, setBisVonHand] = useState(pause !== null);
  const [versucht, setVersucht] = useState(false);
  const [speichert, setSpeichert] = useState(false);

  const fehler = pruefePause(t, eingabe, dog.gender);

  function aendere(neu: Partial<PausenEingabe>) {
    setEingabe((vorher) => {
      const naechste = { ...vorher, ...neu };
      return neu.bis !== undefined
        ? naechste
        : { ...naechste, bis: vorgeschlagenesEnde(naechste.grund, naechste.von, naechste.bis, bisVonHand) };
    });
  }

  async function speichern() {
    setVersucht(true);
    if (fehler !== null) return;
    setSpeichert(true);
    try {
      const nutzlast = {
        reason: eingabe.grund,
        startDate: eingabe.von,
        endDate: eingabe.bis === "" ? null : eingabe.bis,
        note: eingabe.notiz.trim() === "" ? null : eingabe.notiz.trim(),
      };
      if (pause) await api.put(`/api/dogs/${dog.id}/pausen/${pause.id}`, nutzlast);
      else await api.post(`/api/dogs/${dog.id}/pausen`, nutzlast);
      toast.success(t("Pause gespeichert."));
      await onGespeichert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Pause konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  const gruende = waehlbareGruende(dog.gender);

  return (
    <div className="flex min-w-0 flex-col gap-4 rounded-md border border-primary/30 p-3">
      <p className="font-medium">{pause ? t("Pause bearbeiten") : t("Pause eintragen")}</p>

      <div className="flex min-w-0 flex-col gap-2">
        <Label>{t("Grund")}</Label>
        <div className={cn("grid gap-2", gruende.length === 3 ? "grid-cols-3" : "grid-cols-2")} role="group" aria-label={t("Grund")}>
          {gruende.map((grund: PauseGrund) => (
            <button
              key={grund}
              type="button"
              aria-pressed={eingabe.grund === grund}
              onClick={() => aendere({ grund })}
              className={cn(
                "flex min-h-11 min-w-0 items-center justify-center rounded-md border px-2 text-sm font-medium transition-colors",
                eingabe.grund === grund
                  ? "border-primary bg-primary/10 text-primary-text"
                  : "border-input text-muted-foreground hover:bg-muted",
              )}
            >
              <span className="min-w-0 truncate">{t(PAUSE_GRUND_TEXT[grund])}</span>
            </button>
          ))}
        </div>
      </div>

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`pause-von-${pause?.id ?? "neu"}`}>{t("Von")}</Label>
        <Input
          id={`pause-von-${pause?.id ?? "neu"}`}
          type="date"
          required
          value={eingabe.von}
          onChange={(e) => aendere({ von: e.target.value })}
          className="h-11 min-w-0"
        />
      </div>

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`pause-bis-${pause?.id ?? "neu"}`}>{t("Bis (optional)")}</Label>
        <Input
          id={`pause-bis-${pause?.id ?? "neu"}`}
          type="date"
          value={eingabe.bis}
          min={eingabe.von || undefined}
          onChange={(e) => {
            setBisVonHand(true);
            aendere({ bis: e.target.value });
          }}
          className="h-11 min-w-0"
        />
        <p className="text-xs text-muted-foreground">
          {eingabe.grund === PAUSE_GRUND.Laeufig && !bisVonHand
            ? t("Vorschlag: drei Wochen ab Beginn. Du kannst das Ende ändern oder leer lassen.")
            : t("Leer lassen heißt: bis auf Weiteres. Das Ende trägst du später nach oder beendest die Pause.")}
        </p>
      </div>

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`pause-notiz-${pause?.id ?? "neu"}`}>{t("Notiz (optional)")}</Label>
        <Input
          id={`pause-notiz-${pause?.id ?? "neu"}`}
          value={eingabe.notiz}
          maxLength={TEXTLAENGE.pausenNotiz}
          onChange={(e) => aendere({ notiz: e.target.value })}
          className="h-11 min-w-0"
        />
        <p className="text-xs text-muted-foreground">{t("Die Notiz sehen nur du und die Mitbesitzer:innen.")}</p>
      </div>

      {/* Ein noch leeres Formular bekommt keinen Vorwurf: erst nach dem Versuch zu speichern. */}
      {versucht && fehler && <p className="text-sm text-destructive">{fehler}</p>}

      <div className="flex flex-wrap gap-2">
        <Button type="button" className="min-h-11 flex-1" disabled={speichert} onClick={speichern}>
          {speichert ? t("Speichert…") : t("Speichern")}
        </Button>
        <Button type="button" variant="ghost" className="min-h-11" onClick={onAbbrechen}>
          {t("Abbrechen")}
        </Button>
      </div>
    </div>
  );
}

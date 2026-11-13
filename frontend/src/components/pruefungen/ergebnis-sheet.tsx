"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { hundName, pruefeTeamErgebnis, teamName } from "@/lib/pruefungen";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { PRUEFUNG_ERGEBNIS, TEAM_ART, type PruefungErgebnis, type PruefungTeam, type VereinsPruefung } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Das Ergebnis eines Teams: bestanden, nicht bestanden oder nicht angetreten, dazu Punkte (wenn die
 * Ordnung welche hat) und eine kurze Bemerkung. Bei einem Mitglied-Team steht "bestanden" sofort als
 * Leistung beim Hund, ohne Rückfrage - die Familie bekommt dazu eine Nachricht. Mit "Zurücksetzen"
 * nimmt die Prüfungsleitung ein Ergebnis zurück.
 */
export function ErgebnisSheet({
  clubId,
  pruefung,
  team,
  hoechstens,
  onOpenChange,
  onGespeichert,
}: {
  clubId: string;
  pruefung: VereinsPruefung;
  /** Das Team, dessen Ergebnis bearbeitet wird; null = Fenster zu. */
  team: PruefungTeam | null;
  hoechstens: number | null | undefined;
  onOpenChange: (offen: boolean) => void;
  onGespeichert: () => Promise<void>;
}) {
  const t = useT();
  return (
    <Sheet open={team !== null} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{t("Ergebnis eintragen")}</SheetTitle>
          {team && (
            <SheetDescription className="[overflow-wrap:anywhere]">
              {t("{fuehrer} mit {hund}", { fuehrer: teamName(t, team), hund: hundName(team) })}
            </SheetDescription>
          )}
        </SheetHeader>
        {team && <Formular key={team.id} clubId={clubId} pruefung={pruefung} team={team} hoechstens={hoechstens} onGespeichert={onGespeichert} />}
      </BottomSheetContent>
    </Sheet>
  );
}

function Formular({
  clubId,
  pruefung,
  team,
  hoechstens,
  onGespeichert,
}: {
  clubId: string;
  pruefung: VereinsPruefung;
  team: PruefungTeam;
  hoechstens: number | null | undefined;
  onGespeichert: () => Promise<void>;
}) {
  const t = useT();
  const [art, setArt] = useState<PruefungErgebnis>(
    team.result === PRUEFUNG_ERGEBNIS.Offen ? PRUEFUNG_ERGEBNIS.Bestanden : team.result,
  );
  const [punkte, setPunkte] = useState(team.score != null ? String(team.score) : "");
  const [notiz, setNotiz] = useState(team.resultNote ?? "");
  const [speichert, setSpeichert] = useState(false);

  const fehler = pruefeTeamErgebnis(t, art, { punkte, notiz }, pruefung.date, hoechstens);
  const zeigePunkte = art !== PRUEFUNG_ERGEBNIS.NichtAngetreten && (hoechstens != null || punkte !== "");
  const hatErgebnis = team.result !== PRUEFUNG_ERGEBNIS.Offen;

  async function speichern(neu: PruefungErgebnis) {
    if (neu !== PRUEFUNG_ERGEBNIS.Offen && fehler) return;
    setSpeichert(true);
    try {
      const mitPunkten = neu === PRUEFUNG_ERGEBNIS.Bestanden || neu === PRUEFUNG_ERGEBNIS.NichtBestanden;
      const zahl = punkte.trim();
      await api.put(`/api/clubs/${clubId}/exams/${pruefung.id}/entries/${team.id}/result`, {
        result: neu,
        score: mitPunkten && zahl !== "" ? Number(zahl) : null,
        note: neu === PRUEFUNG_ERGEBNIS.Offen ? null : notiz.trim() || null,
      });
      toast.success(neu === PRUEFUNG_ERGEBNIS.Offen ? t("Ergebnis zurückgesetzt.") : t("Ergebnis gespeichert."));
      await onGespeichert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ergebnis konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      <div className="grid grid-cols-1 gap-2 min-[400px]:grid-cols-3" role="group" aria-label={t("Ergebnis")}>
        {[
          { wert: PRUEFUNG_ERGEBNIS.Bestanden, label: t("Bestanden") },
          { wert: PRUEFUNG_ERGEBNIS.NichtBestanden, label: t("Nicht bestanden") },
          { wert: PRUEFUNG_ERGEBNIS.NichtAngetreten, label: t("Nicht angetreten") },
        ].map(({ wert, label }) => (
          <Chip key={wert} gewaehlt={art === wert} onClick={() => setArt(wert)} className="justify-center text-center">
            {label}
          </Chip>
        ))}
      </div>

      {zeigePunkte && (
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor="team-punkte">{t("Punkte")}</Label>
          <div className="flex items-center gap-2">
            <Input
              id="team-punkte"
              type="number"
              inputMode="numeric"
              min={0}
              max={hoechstens ?? undefined}
              value={punkte}
              onChange={(e) => setPunkte(e.target.value)}
              className="w-28 min-w-0"
            />
            {hoechstens != null && <span className="text-sm text-muted-foreground">{t("von {max}", { max: hoechstens })}</span>}
            <span className="text-xs text-muted-foreground">{t("(optional)")}</span>
          </div>
        </div>
      )}
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="team-notiz">{t("Bemerkung")}</Label>
        <Input
          id="team-notiz"
          value={notiz}
          maxLength={TEXTLAENGE.pruefungsNotiz}
          placeholder={t("(optional)")}
          onChange={(e) => setNotiz(e.target.value)}
        />
      </div>

      {team.kind === TEAM_ART.Mitglied && (
        <p className="text-xs text-muted-foreground">
          {t("Bei „Bestanden“ steht das Ergebnis sofort als Leistung beim Hund. Die Familie bekommt zu jedem Ergebnis eine Nachricht.")}
        </p>
      )}
      {fehler && <p className="text-sm text-destructive">{fehler}</p>}

      <Button disabled={speichert || fehler !== null} onClick={() => speichern(art)} className="coarse:min-h-11">
        {speichert ? t("Speichert…") : t("Ergebnis speichern")}
      </Button>
      {hatErgebnis && (
        <Button variant="ghost" disabled={speichert} onClick={() => speichern(PRUEFUNG_ERGEBNIS.Offen)} className="coarse:min-h-11">
          {t("Ergebnis zurücksetzen")}
        </Button>
      )}
    </div>
  );
}

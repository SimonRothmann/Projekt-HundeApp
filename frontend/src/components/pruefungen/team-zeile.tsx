"use client";

import { ChevronDown, ChevronRight, ChevronUp, Trash2 } from "lucide-react";
import { useT } from "@/lib/i18n";
import { chipText } from "@/lib/pruefungsreife";
import { ergebnisText, hundName, teamName } from "@/lib/pruefungen";
import { PRUEFUNG_ERGEBNIS, TEAM_ART, type PruefungTeam } from "@/lib/types";
import { cn } from "@/lib/utils";
import { AMPEL } from "@/components/dogs/pruefungsreife-karte";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

const ERGEBNIS_KLASSEN: Record<number, string> = {
  [PRUEFUNG_ERGEBNIS.Bestanden]: "border-emerald-600/40 bg-emerald-500/10 text-emerald-800 dark:text-emerald-300",
  [PRUEFUNG_ERGEBNIS.NichtBestanden]: "border-red-600/40 bg-red-500/10 text-red-800 dark:text-red-300",
  [PRUEFUNG_ERGEBNIS.NichtAngetreten]: "border-surface-border bg-surface text-muted-foreground",
  [PRUEFUNG_ERGEBNIS.Offen]: "border-input text-foreground",
};

/**
 * Eine Zeile der Meldeliste: Startplatz, Hundeführer:in und Hund (Gäste sind markiert), darunter die
 * Prüfungsreife (nur Mitglied-Teams ohne Ergebnis) und das Ergebnis als antippbare Chips, zuletzt die
 * Knöpfe für Reihenfolge und Austragen. Gerechnet wird nichts - alles kommt vom Server.
 */
export function TeamZeile({
  team,
  erster,
  letzter,
  hoechstens,
  gesperrt,
  onReife,
  onErgebnis,
  onVerschieben,
  onAustragen,
}: {
  team: PruefungTeam;
  erster: boolean;
  letzter: boolean;
  hoechstens: number | null | undefined;
  /** Eine Aktion läuft: Die Knöpfe warten. */
  gesperrt: boolean;
  onReife: () => void;
  onErgebnis: () => void;
  onVerschieben: (richtung: -1 | 1) => void;
  onAustragen: () => void;
}) {
  const t = useT();
  const mitReife = team.kind === TEAM_ART.Mitglied && team.result === PRUEFUNG_ERGEBNIS.Offen;
  const ampel = team.reife ? AMPEL[team.reife.ampel] : null;

  return (
    <li className="flex min-w-0 flex-col gap-2 py-3 first:pt-0 last:pb-0">
      <div className="flex min-w-0 items-start gap-2">
        <span className="mt-0.5 w-7 shrink-0 text-right text-sm font-semibold tabular-nums text-muted-foreground">{team.startNumber}.</span>
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <p className="min-w-0 font-medium [overflow-wrap:anywhere]">
            {teamName(t, team)} · {hundName(team)}
            {team.kind === TEAM_ART.Gast && (
              <Badge variant="outline" className="ml-2 align-middle">
                {t("Gast")}
              </Badge>
            )}
          </p>
          {team.kind === TEAM_ART.Gast && team.info && <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{team.info}</p>}
          {team.resultNote && <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{team.resultNote}</p>}
        </div>
      </div>

      <div className="flex min-w-0 flex-wrap gap-2 pl-9">
        {mitReife &&
          (team.reife && ampel ? (
            <button
              type="button"
              onClick={onReife}
              aria-haspopup="dialog"
              className={cn(
                "flex min-h-11 min-w-0 items-center gap-2 rounded-md border px-3 py-1.5 text-left text-sm font-medium transition-colors hover:brightness-95",
                ampel.klassen,
              )}
            >
              <ampel.symbol className="size-4 shrink-0" aria-hidden />
              <span className="min-w-0 [overflow-wrap:anywhere]">{chipText(t, team.reife)}</span>
              <ChevronRight className="size-4 shrink-0" aria-hidden />
            </button>
          ) : (
            <span className="inline-flex min-h-11 items-center rounded-md border border-dashed px-3 text-sm text-muted-foreground">
              {t("Kein Ziel in Dogity")}
            </span>
          ))}
        <button
          type="button"
          onClick={onErgebnis}
          aria-haspopup="dialog"
          className={cn(
            "flex min-h-11 min-w-0 items-center gap-2 rounded-md border px-3 py-1.5 text-left text-sm font-medium transition-colors hover:brightness-95",
            ERGEBNIS_KLASSEN[team.result],
          )}
        >
          <span className="min-w-0 [overflow-wrap:anywhere]">
            {team.result === PRUEFUNG_ERGEBNIS.Offen ? t("Ergebnis eintragen") : ergebnisText(t, team, hoechstens)}
          </span>
          <ChevronRight className="size-4 shrink-0" aria-hidden />
        </button>
      </div>

      <div className="flex flex-wrap items-center gap-1 pl-9">
        <Button
          size="sm"
          variant="ghost"
          disabled={gesperrt || erster}
          onClick={() => onVerschieben(-1)}
          aria-label={t("Nach oben")}
          className="coarse:min-h-11 coarse:min-w-11"
        >
          <ChevronUp className="size-4" />
        </Button>
        <Button
          size="sm"
          variant="ghost"
          disabled={gesperrt || letzter}
          onClick={() => onVerschieben(1)}
          aria-label={t("Nach unten")}
          className="coarse:min-h-11 coarse:min-w-11"
        >
          <ChevronDown className="size-4" />
        </Button>
        <Button
          size="sm"
          variant="ghost"
          disabled={gesperrt}
          onClick={onAustragen}
          className="text-destructive hover:text-destructive coarse:min-h-11"
        >
          <Trash2 className="size-3.5" />
          {t("Austragen")}
        </Button>
      </div>
    </li>
  );
}

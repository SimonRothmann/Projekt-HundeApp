"use client";

import { useState } from "react";
import { Clock, Crosshair, Ellipsis, Pencil, Timer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Chip } from "@/components/dogs/eintragen-chip";
import { DAUER_MAX, DAUER_VORSCHLAEGE } from "@/lib/eintragen";
import { useT } from "@/lib/i18n";

/**
 * Der Fuß des Eintragen-Fensters, der unten stehen bleibt: die Kontextzeile
 * mit Zeit und Ort, Dauer, "Mehr" und der große Speichern-Knopf.
 *
 * Alles Vorbelegte ist hier sichtbar und mit einem Tipp änderbar - nichts
 * wird gespeichert, das man nicht gesehen hat. Der Speichern-Knopf sitzt ganz
 * unten und über die volle Breite: der Daumen-Bereich einer einhändigen Bedienung.
 */
export function EintragenFuss({
  dauer,
  onDauer,
  kontext,
  hatOrt,
  standortLaeuft,
  onOrtBearbeiten,
  onStandort,
  mehrOffen,
  onMehr,
  anzahl,
  speichert,
  onSpeichern,
}: {
  dauer: number;
  onDauer: (minuten: number) => void;
  /** "Heute 16:40 · Hundeplatz" */
  kontext: string;
  hatOrt: boolean;
  standortLaeuft: boolean;
  onOrtBearbeiten: () => void;
  onStandort: () => void;
  mehrOffen: boolean;
  onMehr: () => void;
  anzahl: number;
  speichert: boolean;
  onSpeichern: () => void;
}) {
  const t = useT();
  const [dauerOffen, setDauerOffen] = useState(false);

  // Drei Zeilen statt einer gedrängten: Bei 390 px bliebe neben Dauer und "Mehr"
  // für den Speichern-Knopf kaum Platz, und "Speichern · 3 Übungen" würde
  // abgeschnitten. Der Ort steht oben und gibt dem Namen die volle Breite;
  // der Knopf "Standort verwenden" erscheint nur, wo noch keiner steht - und
  // dann ist der Kontext kurz.
  return (
    <div className="flex shrink-0 flex-col gap-1 border-t border-surface-border bg-popover px-4 pt-2 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={onOrtBearbeiten}
          aria-label={`${kontext} · ${t("Ort & Uhrzeit ändern")}`}
          className="flex min-h-11 min-w-0 flex-1 items-center gap-2 rounded-lg text-left text-sm"
        >
          <Clock className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          <span className="min-w-0 flex-1 truncate">{kontext}</span>
          <Pencil className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
        </button>
        {!hatOrt && (
          <Button type="button" variant="outline" className="min-h-11 shrink-0 px-3" disabled={standortLaeuft} onClick={onStandort}>
            <Crosshair className="size-4" />
            {standortLaeuft ? t("Ermittle…") : t("Standort verwenden")}
          </Button>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Chip
          aria-expanded={dauerOffen}
          aria-label={`${t("Dauer in Minuten")}: ${Number.isFinite(dauer) ? dauer : "-"}`}
          onClick={() => setDauerOffen((offen) => !offen)}
        >
          <Timer className="size-4" aria-hidden />
          {Number.isFinite(dauer) ? t("{minuten} Min.", { minuten: dauer }) : "-"}
        </Chip>
        <Chip aria-expanded={mehrOffen} onClick={onMehr}>
          <Ellipsis className="size-4" aria-hidden />
          {t("Mehr")}
        </Chip>
      </div>

      {dauerOffen && (
        <div className="flex flex-wrap items-center gap-1.5 py-1" role="group" aria-label={t("Dauer in Minuten")}>
          {DAUER_VORSCHLAEGE.map((minuten) => (
            <Chip key={minuten} gewaehlt={dauer === minuten} className="min-w-11 justify-center" onClick={() => onDauer(minuten)}>
              {minuten}
            </Chip>
          ))}
          <Input
            type="number"
            inputMode="numeric"
            min={1}
            max={DAUER_MAX}
            aria-label={t("Dauer in Minuten")}
            value={Number.isFinite(dauer) ? dauer : ""}
            onChange={(e) => onDauer(e.target.value === "" ? NaN : Number(e.target.value))}
            className="h-11 w-20"
          />
          <span className="text-sm text-muted-foreground">{t("Min.")}</span>
        </div>
      )}

      <Button type="button" disabled={speichert} onClick={onSpeichern} className="mt-1 h-12 w-full text-base font-semibold">
        <span className="min-w-0 truncate">
          {speichert
            ? t("Wird gespeichert…")
            : anzahl === 0
              ? t("Speichern")
              : anzahl === 1
                ? t("Speichern · 1 Übung")
                : t("Speichern · {anzahl} Übungen", { anzahl })}
        </span>
      </Button>
    </div>
  );
}

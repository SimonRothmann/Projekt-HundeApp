"use client";

import { useId } from "react";
import type { GpsTrack, GpsWalkRun } from "@/lib/types";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ANZEIGEVERHALTEN, anzeigeverhaltenLabel, stundenText, type TrailAngabenVorLegen } from "@/lib/mantrailing";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { cn } from "@/lib/utils";

import { useSprache, useT } from "@/lib/i18n";

/**
 * Die Angaben, die nur ein Mantrailing-Trail hat (Paket M1): vor dem Legen
 * Doppelblind, Negativ-Trail und Geruchsartikel (am Trail, weil sie für jeden
 * Suchversuch gelten), nach dem Suchen Person gefunden, Anzeigeverhalten und
 * Trailalter (am Lauf bzw. am Trail, siehe lib/mantrailing.ts, trailAenderungen).
 */

/** Ein Antippen-Knopf wie die Untergründe beim Fährtelegen: Aussehen eines Chips, Trefferfläche 44 px. */
function Wahl({ aktiv, onClick, children }: { aktiv: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={aktiv}
      onClick={onClick}
      className={cn(
        "rounded-full border px-3 py-1.5 text-sm transition-colors coarse:min-h-11",
        aktiv
          ? "border-primary bg-primary/15 text-primary-text"
          : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
      )}
    >
      {children}
    </button>
  );
}

/** Vor dem Legen: Doppelblind, Negativ-Trail, Geruchsartikel. */
export function TrailLegenFormular({
  werte,
  onChange,
}: {
  werte: TrailAngabenVorLegen;
  onChange: (werte: TrailAngabenVorLegen) => void;
}) {
  const t = useT();
  const id = useId();

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-2">
        <span id={`${id}-art`} className="text-sm font-medium">
          {t("Trail")}{" "}
          <span className="font-normal text-muted-foreground">· {t("optional")}</span>
        </span>
        <div role="group" aria-labelledby={`${id}-art`} className="flex flex-wrap gap-1.5">
          <Wahl aktiv={werte.doubleBlind} onClick={() => onChange({ ...werte, doubleBlind: !werte.doubleBlind })}>
            {t("Doppelblind")}
          </Wahl>
          <Wahl aktiv={werte.negativeTrail} onClick={() => onChange({ ...werte, negativeTrail: !werte.negativeTrail })}>
            {t("Negativ-Trail")}
          </Wahl>
        </div>
        <p className="text-xs text-muted-foreground">
          {t("Doppelblind: Das Team kennt den Verlauf nicht. Negativ-Trail: Am Ende wartet niemand.")}
        </p>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`${id}-artikel`} className="text-sm font-medium">
          {t("Geruchsartikel (optional)")}
        </Label>
        <Input
          id={`${id}-artikel`}
          value={werte.scentArticle}
          maxLength={TEXTLAENGE.geruchsartikel}
          placeholder={t("z. B. Kapuzenpulli")}
          onChange={(e) => onChange({ ...werte, scentArticle: e.target.value })}
          className="h-11"
        />
      </div>
    </div>
  );
}

/** Nach dem Suchen: Person gefunden (bzw. Negativ korrekt angezeigt), Anzeigeverhalten, Trailalter in Stunden. */
export function TrailErgebnisFormular({
  negativ,
  gefunden,
  anzeige,
  alterText,
  alterVorgeschlagen,
  onGefunden,
  onAnzeige,
  onAlter,
}: {
  negativ: boolean;
  gefunden: boolean | null;
  anzeige: number | null;
  alterText: string;
  /** Das Alter stammt aus der Zeit zwischen Legen und Suchen, nicht aus einer Eingabe. */
  alterVorgeschlagen: boolean;
  onGefunden: (wert: boolean | null) => void;
  onAnzeige: (wert: number | null) => void;
  onAlter: (text: string) => void;
}) {
  const t = useT();
  const id = useId();
  const frage = negativ ? t("Negativ korrekt angezeigt?") : t("Person gefunden?");

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-2">
        <span id={`${id}-gefunden`} className="text-sm text-muted-foreground">
          {frage}
        </span>
        <div role="group" aria-labelledby={`${id}-gefunden`} className="flex flex-wrap gap-1.5">
          <Wahl aktiv={gefunden === true} onClick={() => onGefunden(gefunden === true ? null : true)}>
            {t("Ja")}
          </Wahl>
          <Wahl aktiv={gefunden === false} onClick={() => onGefunden(gefunden === false ? null : false)}>
            {t("Nein")}
          </Wahl>
        </div>
      </div>

      {/* Ein Anzeigeverhalten gibt es nur, wo eine Person gefunden wurde. */}
      {gefunden === true && !negativ && (
        <div className="flex flex-col gap-2">
          <span id={`${id}-anzeige`} className="text-sm text-muted-foreground">
            {t("Anzeigeverhalten")}
          </span>
          <div role="group" aria-labelledby={`${id}-anzeige`} className="flex flex-wrap gap-1.5">
            {ANZEIGEVERHALTEN.map((a) => (
              <Wahl key={a.value} aktiv={anzeige === a.value} onClick={() => onAnzeige(anzeige === a.value ? null : a.value)}>
                {t(a.label)}
              </Wahl>
            ))}
          </div>
        </div>
      )}

      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`${id}-alter`} className="text-sm text-muted-foreground">
          {t("Trailalter in Stunden")}
        </Label>
        <Input
          id={`${id}-alter`}
          value={alterText}
          inputMode="decimal"
          autoComplete="off"
          placeholder={t("z. B. 1,5")}
          onChange={(e) => onAlter(e.target.value)}
          className="h-11"
        />
        {alterVorgeschlagen && alterText !== "" && (
          <p className="text-xs text-muted-foreground">{t("Vorgeschlagen aus der Zeit zwischen Legen und Suchen.")}</p>
        )}
      </div>
    </div>
  );
}

const Angabe = ({ children }: { children: React.ReactNode }) => (
  <span className="rounded-md border border-input px-2 py-0.5 text-xs text-foreground [overflow-wrap:anywhere]">{children}</span>
);

/** Die Angaben zum Trail, schreibgeschützt: Alter, Doppelblind, Negativ-Trail, Geruchsartikel. Leer, wenn es nichts anzuzeigen gibt. */
export function TrailAngabenZeile({ track }: { track: Pick<GpsTrack, "ageMinutes" | "doubleBlind" | "negativeTrail" | "scentArticle"> }) {
  const t = useT();
  const sprache = useSprache();
  const alter = stundenText(track.ageMinutes, sprache);
  if (!alter && !track.doubleBlind && !track.negativeTrail && !track.scentArticle) return null;

  return (
    <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
      {alter && <Angabe>{t("Trailalter {stunden} Std.", { stunden: alter })}</Angabe>}
      {track.doubleBlind && <Angabe>{t("Doppelblind")}</Angabe>}
      {track.negativeTrail && <Angabe>{t("Negativ-Trail")}</Angabe>}
      {track.scentArticle && <Angabe>{t("Geruchsartikel: {artikel}", { artikel: track.scentArticle })}</Angabe>}
    </div>
  );
}

/** Das Ergebnis eines Laufs, schreibgeschützt. Leer, solange nichts eingetragen ist. */
export function TrailErgebnisZeile({ negativ, run }: { negativ: boolean; run: Pick<GpsWalkRun, "personFound" | "indication"> }) {
  const t = useT();
  if (run.personFound == null) return null;

  const anzeige = anzeigeverhaltenLabel(run.indication);
  const gefunden = negativ
    ? run.personFound
      ? t("Negativ korrekt angezeigt")
      : t("Negativ nicht korrekt angezeigt")
    : run.personFound
      ? t("Person gefunden")
      : t("Person nicht gefunden");

  return (
    <div className="flex flex-wrap items-center gap-1.5 text-xs">
      <span className={cn("font-medium", run.personFound ? "text-emerald-600 dark:text-emerald-500" : "text-destructive")}>{gefunden}</span>
      {anzeige && <Angabe>{t("Anzeige: {verhalten}", { verhalten: t(anzeige) })}</Angabe>}
    </div>
  );
}

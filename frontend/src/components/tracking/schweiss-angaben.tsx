"use client";

import { Chip } from "@/components/ui/chip";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useT } from "@/lib/i18n";
import {
  MAX_SCHWEISS_ML,
  SCHWEISS_LEITFAEDEN,
  SCHWEISS_VERFAHREN,
  SCHWEISS_WILDARTEN,
  schweissLeitfadenZeile,
  STUECK_VERHALTEN,
  stueckVerhaltenLabel,
  type SchweissLeitfaden,
} from "@/lib/schweiss";
import type { GpsTrack, GpsWalkRun } from "@/lib/types";
import { cn } from "@/lib/utils";
import { mitRueckrufenDurchgefallen } from "@/lib/rueckruf";

const KEIN_LEITFADEN = "keiner";

/**
 * Die Angaben vor dem Legen einer Schweißfährte: Leitfaden (die Prüfung, auf die gelegt wird), Verfahren,
 * Wildart und Schweißmenge. Nichts davon ist Pflicht. Die Auswahl geschieht durch Antippen (wie beim
 * Untergrund), nur die Menge wird getippt - vor dem Start, nicht während des Legens.
 */
export function SchweissLegenFormular({
  leitfaden,
  onLeitfaden,
  verfahren,
  onVerfahren,
  wildart,
  onWildart,
  mlText,
  onMl,
  mlGueltig,
}: {
  leitfaden: SchweissLeitfaden | null;
  onLeitfaden: (id: string | null) => void;
  verfahren: string | null;
  onVerfahren: (verfahren: string | null) => void;
  wildart: string | null;
  onWildart: (wildart: string | null) => void;
  mlText: string;
  onMl: (text: string) => void;
  mlGueltig: boolean;
}) {
  const t = useT();

  return (
    <>
      <div className="flex flex-col gap-2">
        <span id="schweiss-leitfaden" className="text-sm font-medium">
          {t("Leitfaden (optional)")}
        </span>
        <Select value={leitfaden?.id ?? KEIN_LEITFADEN} onValueChange={(v) => onLeitfaden(v && v !== KEIN_LEITFADEN ? v : null)}>
          <SelectTrigger aria-labelledby="schweiss-leitfaden">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={KEIN_LEITFADEN}>{t("Keiner")}</SelectItem>
            {SCHWEISS_LEITFAEDEN.map((l) => (
              <SelectItem key={l.id} value={l.id}>
                {t(l.name)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {leitfaden && <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{schweissLeitfadenZeile(leitfaden, t)}</p>}
      </div>

      <div className="flex flex-col gap-2">
        <span id="schweiss-verfahren" className="text-sm font-medium">
          {t("Verfahren (optional)")}
        </span>
        <div role="group" aria-labelledby="schweiss-verfahren" className="flex flex-wrap gap-1.5">
          {SCHWEISS_VERFAHREN.map((v) => (
            <Chip key={v} kompakt gewaehlt={verfahren === v} onClick={() => onVerfahren(verfahren === v ? null : v)}>
              {t(v)}
            </Chip>
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <span id="schweiss-wildart" className="text-sm font-medium">
          {t("Wildart (optional)")}
        </span>
        <div role="group" aria-labelledby="schweiss-wildart" className="flex flex-wrap gap-1.5">
          {SCHWEISS_WILDARTEN.map((w) => (
            <Chip key={w} kompakt gewaehlt={wildart === w} onClick={() => onWildart(wildart === w ? null : w)}>
              {t(w)}
            </Chip>
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="schweiss-ml" className="text-sm font-medium">
          {t("Schweißmenge in ml (optional)")}
        </Label>
        <Input
          id="schweiss-ml"
          value={mlText}
          inputMode="numeric"
          autoComplete="off"
          maxLength={4}
          placeholder={t("z. B. 100")}
          onChange={(e) => onMl(e.target.value.replace(/\D/g, ""))}
          aria-invalid={!mlGueltig}
          className="h-11"
        />
        {!mlGueltig && <p className="text-xs text-destructive">{t("Höchstens {max} ml.", { max: MAX_SCHWEISS_ML })}</p>}
      </div>
    </>
  );
}

const Angabe = ({ children }: { children: React.ReactNode }) => (
  <span className="rounded-md border border-input px-2 py-0.5 text-xs text-foreground [overflow-wrap:anywhere]">{children}</span>
);

/** Die Angaben zur Schweißfährte, schreibgeschützt: Verfahren, Wildart, Menge, Stehzeit. Leer, wenn es nichts anzuzeigen gibt. */
export function SchweissAngabenZeile({ track }: { track: Pick<GpsTrack, "verfahren" | "wildart" | "schweissMl" | "stehzeitStunden"> }) {
  const t = useT();
  if (!track.verfahren && !track.wildart && track.schweissMl == null && track.stehzeitStunden == null) return null;

  return (
    <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
      {track.verfahren && <Angabe>{t(track.verfahren)}</Angabe>}
      {track.wildart && <Angabe>{t(track.wildart)}</Angabe>}
      {track.schweissMl != null && <Angabe>{t("{n} ml Schweiß", { n: track.schweissMl })}</Angabe>}
      {track.stehzeitStunden != null && <Angabe>{t("Stehzeit {n} Std.", { n: track.stehzeitStunden })}</Angabe>}
    </div>
  );
}

/**
 * Das Ergebnis am Stück (Paket SF2): Zum Stück gekommen ja/nein und, wenn ja, Totverbellen oder Totverweisen.
 * `gpsEnde`: ob das Gespann das Ende der Legung erreichte - nur ein Hinweis neben der Angabe der Hundeführer:in.
 */
export function SchweissStueckFormular({
  gefunden,
  verhalten,
  gpsEnde,
  onGefunden,
  onVerhalten,
}: {
  gefunden: boolean | null;
  verhalten: number | null;
  gpsEnde?: boolean | null;
  onGefunden: (gefunden: boolean | null) => void;
  onVerhalten: (verhalten: number | null) => void;
}) {
  const t = useT();
  return (
    <div className="flex min-w-0 flex-col gap-3">
      <div className="flex flex-col gap-2">
        <span className="flex flex-wrap items-baseline gap-x-2 text-sm font-medium">
          {t("Zum Stück gekommen")}
          {gpsEnde != null && (
            <span className="text-xs font-normal text-muted-foreground">{gpsEnde ? t("GPS: Ende erreicht") : t("GPS: Ende nicht erreicht")}</span>
          )}
        </span>
        <div className="flex flex-wrap gap-1.5">
          <Chip kompakt gewaehlt={gefunden === true} onClick={() => onGefunden(gefunden === true ? null : true)}>
            {t("Ja")}
          </Chip>
          <Chip kompakt gewaehlt={gefunden === false} onClick={() => onGefunden(gefunden === false ? null : false)}>
            {t("Nein")}
          </Chip>
        </div>
      </div>
      {gefunden === true && (
        <div className="flex flex-col gap-2">
          <span className="text-sm text-muted-foreground">{t("Verhalten am Stück")}</span>
          <div className="flex flex-wrap gap-1.5">
            {STUECK_VERHALTEN.map((v) => (
              <Chip kompakt key={v.value} gewaehlt={verhalten === v.value} onClick={() => onVerhalten(verhalten === v.value ? null : v.value)}>
                {t(v.label)}
              </Chip>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

/** Das Ergebnis am Stück, schreibgeschützt. Leer, solange nichts eingetragen ist. */
export function SchweissStueckZeile({ run }: { run: Pick<GpsWalkRun, "personFound" | "indication"> }) {
  const t = useT();
  if (run.personFound == null) return null;
  const verhalten = stueckVerhaltenLabel(run.indication);
  return (
    <div className="flex flex-wrap items-center gap-1.5 text-xs">
      <span className={cn("font-medium", run.personFound ? "text-erfolg-text" : "text-destructive")}>
        {run.personFound ? t("Zum Stück gekommen") : t("Nicht zum Stück gekommen")}
      </span>
      {verhalten && <Angabe>{t(verhalten)}</Angabe>}
    </div>
  );
}

/** Die Rückrufe eines Ablaufs: Zahl, bei mehr als zwei der Hinweis auf "nicht bestanden". Nichts, wenn sie nicht berechnet sind. */
export function RueckrufZeile({ anzahl, className }: { anzahl: number | null | undefined; className?: string }) {
  const t = useT();
  if (anzahl == null) return null;
  return (
    <span className={cn("min-w-0 [overflow-wrap:anywhere]", className)}>
      {anzahl === 0 ? t("keine Rückrufe") : t("Rückrufe: {n}", { n: anzahl })}
      {mitRueckrufenDurchgefallen(anzahl) && <span className="text-destructive"> · {t("nicht bestanden (mehr als zwei Rückrufe)")}</span>}
    </span>
  );
}

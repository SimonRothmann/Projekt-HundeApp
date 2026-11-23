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
  type SchweissLeitfaden,
} from "@/lib/schweiss";
import type { GpsTrack } from "@/lib/types";

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

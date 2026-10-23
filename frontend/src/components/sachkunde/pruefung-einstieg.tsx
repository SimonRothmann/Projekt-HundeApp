"use client";

import { ClipboardCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth-context";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import type { QuizCatalog } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useErgebnisWort, useVerlauf } from "@/components/sachkunde/pruefung-verlauf";

/**
 * Der Einstieg in die Übungsprüfung auf der Katalogseite, neben den
 * Lernmodi: Kurzinfo und - angemeldet - das letzte Ergebnis. Ohne Anmeldung
 * nutzbar, nur das Ergebnis wird dann nicht gespeichert.
 */
export function PruefungEinstieg({ catalog, onOeffnen }: { catalog: QuizCatalog; onOeffnen: () => void }) {
  const t = useT();
  const ort = ortsformat(useSprache());
  const { user } = useAuth();
  const wort = useErgebnisWort();
  const verlauf = useVerlauf(catalog.code, user !== null);
  const info = catalog.exam;
  const letztes = verlauf?.attempts[0] ?? null;

  if (!info) return null;

  return (
    <section className="min-w-0 rounded-xl border border-primary/30 bg-primary/5 p-4 sm:p-5">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <ClipboardCheck className="size-4 shrink-0 text-primary-text" />
        <h2 className="text-base font-bold tracking-tight">{t("Übungsprüfung")}</h2>
        <Badge variant="outline" className="text-xs">
          {t("Nur zur Übung")}
        </Badge>
      </div>

      <p className="mt-1.5 text-sm text-muted-foreground">
        {t("{n} Fragen · bestanden ab {grenze} von {max} Punkten", {
          n: info.questionCount,
          grenze: info.passPoints,
          max: info.maxPoints,
        })}
      </p>

      {letztes && (
        <p className="mt-2 min-w-0 text-sm [overflow-wrap:anywhere]">
          <span className="text-muted-foreground">{t("Letztes Ergebnis:")} </span>
          <span className="font-medium tabular-nums">
            {t("{punkte} von {max} Punkten", { punkte: letztes.points, max: letztes.maxPoints })}
          </span>
          {" · "}
          <span
            className={cn(
              "font-medium",
              letztes.passed ? "text-emerald-600 dark:text-emerald-400" : "text-destructive",
            )}
          >
            {wort(letztes.passed)}
          </span>
          <span className="text-xs text-muted-foreground tabular-nums">
            {" · "}
            {new Date(letztes.submittedAt).toLocaleDateString(ort, { day: "2-digit", month: "2-digit", year: "numeric" })}
          </span>
        </p>
      )}

      <Button className="mt-3" size="sm" onClick={onOeffnen}>
        {t("Zur Übungsprüfung")}
      </Button>
    </section>
  );
}

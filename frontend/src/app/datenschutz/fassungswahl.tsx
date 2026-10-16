"use client";

import { useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { useSprache, useT } from "@/lib/i18n";

/**
 * Wählt zwischen der deutschen und der englischen Datenschutzerklärung.
 *
 * Die Seite wird auf dem Server immer mit der deutschen Fassung gerendert
 * (Rechtstext und Suchmaschine) - die Oberflächensprache steht erst im
 * Browser fest. Erst dort zeigt diese Komponente bei englischer Oberfläche
 * die Übersetzung. Beide Fassungen kommen als fertige Bausteine von der
 * Server-Seite herein; so wandert kein Rechtstext in das Client-Bundle.
 *
 * Der Wechsel zur anderen Fassung gilt nur für diese Ansicht. Er ist
 * bewusst keine Spracheinstellung und wird nirgends gespeichert: Wer den
 * deutschen Text lesen will, hat damit nicht die Oberfläche umgestellt.
 */
export function Fassungswahl({ deutsch, englisch }: { deutsch: ReactNode; englisch: ReactNode }) {
  const sprache = useSprache();
  const t = useT();
  const [deutschGewaehlt, setDeutschGewaehlt] = useState(false);

  if (sprache !== "en") return <>{deutsch}</>;

  const zeigeEnglisch = !deutschGewaehlt;

  return (
    <>
      <div className="mt-8 flex flex-col items-start gap-3 rounded-lg border border-border bg-muted/40 p-4 text-sm sm:flex-row sm:items-center sm:justify-between">
        <p className="min-w-0 break-words text-foreground">
          {t("Das ist eine Übersetzung zu deiner Orientierung. Rechtlich verbindlich ist die deutsche Fassung.")}
        </p>
        <Button
          variant="outline"
          className="h-auto min-h-8 max-w-full shrink whitespace-normal py-1.5 text-center"
          onClick={() => setDeutschGewaehlt(zeigeEnglisch)}
        >
          {zeigeEnglisch ? t("Deutsche Fassung anzeigen") : t("Englische Fassung anzeigen")}
        </Button>
      </div>
      {/* lang am Text, weil <html lang> der Oberfläche folgt (en) - sonst läse der Screenreader den deutschen Rechtstext englisch */}
      {zeigeEnglisch ? englisch : <div lang="de">{deutsch}</div>}
    </>
  );
}

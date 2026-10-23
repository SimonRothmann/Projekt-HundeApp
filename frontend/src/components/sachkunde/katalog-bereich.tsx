"use client";

import { useEffect, useMemo, useState } from "react";
import { api } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { leseStand, type PruefungsStand } from "@/lib/sachkunde-pruefung";
import type { QuizCatalog, QuizExamInfo, QuizMode } from "@/lib/types";
import { PruefungEinstieg } from "@/components/sachkunde/pruefung-einstieg";
import { QuizTrainer } from "@/components/sachkunde/quiz-trainer";
import { Uebungspruefung } from "@/components/sachkunde/uebungspruefung";

/**
 * Der Übungsbereich einer Katalogseite: der Fragentrainer und - wo der Katalog
 * eine hat - die Übungsprüfung daneben. Gemeinsam für die öffentliche Seite
 * (/sachkunde/[code]) und die App-Route (/lernen/[code]).
 *
 * Eine laufende Prüfung (aus dem Browserspeicher) öffnet sich beim Laden der
 * Seite wieder von selbst - so übersteht sie ein Neuladen.
 *
 * Die Katalogseiten werden gebaut und 24 Stunden zwischengespeichert
 * (lib/public-sachkunde.ts). Stammt der Katalog aus einer Antwort, die das Feld
 * `exam` noch gar nicht kannte (fehlt es ganz, anders als `null` = "keine
 * Übungsprüfung"), fragt die Seite es selbst nach - sonst bliebe die
 * Übungsprüfung nach dem Ausrollen bis zu einen Tag unsichtbar, je nachdem, ob
 * Backend oder Frontend zuerst kam.
 */
export function KatalogBereich({ catalog }: { catalog: QuizCatalog }) {
  const t = useT();

  const [exam, setExam] = useState<QuizExamInfo | null | undefined>(catalog.exam);
  // Fest "keine Übungsprüfung": nichts zu klären, der Trainer kann sofort erscheinen.
  const [bereit, setBereit] = useState(catalog.exam === null);
  const [laufende, setLaufende] = useState<PruefungsStand | null>(null);
  const [inPruefung, setInPruefung] = useState(false);
  const [modus, setModus] = useState<QuizMode>("learn");
  // Neuer Schlüssel = der Trainer baut sich mit dem gewünschten Anfangsmodus neu auf.
  const [trainerRunde, setTrainerRunde] = useState(0);

  useEffect(() => {
    let aktuell = true;

    async function klaeren() {
      let info = catalog.exam;
      if (info === undefined) {
        try {
          const alle = await api.get<QuizCatalog[]>("/api/sachkunde/catalogs");
          info = alle.find((k) => k.code === catalog.code)?.exam ?? null;
        } catch {
          // Ohne Antwort gibt es eben keinen Einstieg - der Trainer funktioniert unabhängig davon.
          info = null;
        }
      }
      if (!aktuell) return;

      setExam(info);
      if (info) {
        // Der Browserspeicher ist beim serverseitigen Rendern nicht lesbar -
        // also erst hier, nach dem Aufbau im Browser.
        const stand = leseStand(catalog.code);
        setLaufende(stand);
        setInPruefung(stand !== null);
      }
      setBereit(true);
    }

    void klaeren();
    return () => {
      aktuell = false;
    };
  }, [catalog.code, catalog.exam]);

  const katalog = useMemo(() => ({ ...catalog, exam: exam ?? null }), [catalog, exam]);

  if (!bereit) {
    return <p className="py-10 text-sm text-muted-foreground">{t("Lädt…")}</p>;
  }

  if (inPruefung && katalog.exam) {
    return (
      <Uebungspruefung
        catalog={katalog}
        anfangsStand={laufende}
        onVerlassen={(fehlerspeicher) => {
          setLaufende(null);
          setModus(fehlerspeicher ? "mistakes" : "learn");
          setTrainerRunde((n) => n + 1);
          setInPruefung(false);
        }}
      />
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-4">
      {katalog.exam && <PruefungEinstieg catalog={katalog} onOeffnen={() => setInPruefung(true)} />}
      <QuizTrainer key={trainerRunde} catalog={katalog} anfangsModus={modus} />
    </div>
  );
}

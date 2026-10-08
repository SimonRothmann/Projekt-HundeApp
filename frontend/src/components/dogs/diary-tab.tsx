"use client";

import { useState } from "react";
import type { TrainingSession } from "@/lib/types";
import { SessionHistory } from "@/components/dogs/session-history";
import { FaehrtenTrend } from "@/components/tracking/faehrten-trend";
import { useT } from "@/lib/i18n";
import { useFaehrtenStats } from "@/lib/use-faehrten-stats";
import { geltenderFilter, nachTagen, verfuegbareFilter, type TagesFilter } from "@/lib/tagebuch";
import { cn } from "@/lib/utils";

/**
 * Der Reiter "Tagebuch": der Fährten-Verlauf, die Filter und das kompakte
 * Tagebuch. Training eintragen läuft über das Eintragen-Fenster der Hundeseite.
 */
export function DiaryTab({
  dogId,
  dogName,
  isOwner,
  sessions,
  faehrteAn,
  faehrtenStand,
  onChanged,
  onLoadOlder,
  fokusEintrag,
  gesehenBeiStart,
  onGesehen,
}: {
  dogId: string;
  dogName: string;
  isOwner: boolean;
  sessions: TrainingSession[] | null;
  /** Fährte-Modul an UND der Hund läuft Fährte: dann Verlauf und Auswertung in den Zeilen. */
  faehrteAn: boolean;
  /** Ändert sich nach jeder neuen Fährte oder jedem Ablauf; lädt die Auswertung neu. */
  faehrtenStand: number;
  onChanged: () => Promise<void>;
  onLoadOlder: (() => Promise<void>) | null;
  /** Der Eintrag, auf den ein Link zeigt - null, solange die Seite noch nicht fertig geladen ist. */
  fokusEintrag: string | null;
  gesehenBeiStart: ReadonlySet<string>;
  onGesehen: (kennung: string) => void;
}) {
  const t = useT();
  const [gewaehlt, setGewaehlt] = useState<TagesFilter>("alle");
  // Springt ein Link (oder "Neues Feedback") zu einem Eintrag, zeigt die Liste
  // wieder alle Tage: Der Tag könnte vom Filter ausgeblendet sein, und das
  // Hinscrollen liefe ins Leere. (Zustand während des Renderns angleichen.)
  const [fokusStand, setFokusStand] = useState(fokusEintrag);
  if (fokusEintrag !== fokusStand) {
    setFokusStand(fokusEintrag);
    if (fokusEintrag) setGewaehlt("alle");
  }
  const { stats, bereit } = useFaehrtenStats(dogId, faehrteAn, faehrtenStand);

  const angeboten = verfuegbareFilter(Array.from(nachTagen(sessions).values()));
  const filter = geltenderFilter(gewaehlt, angeboten);
  const filterLabel: Record<TagesFilter, string> = {
    alle: t("Alle"),
    faehrten: t("Fährten"),
    feedback: t("Mit Feedback"),
  };

  return (
    <div className="flex flex-col gap-4">
      {/* Der Verlauf gehört zur Fährtenarbeit, deshalb nur mit denselben
          Bedingungen wie das Fährtelegen. Erst ab drei ausgewerteten Abläufen:
          bei einem oder zwei Balken sagt ein Verlauf nichts. */}
      {faehrteAn && (
        <FaehrtenTrend
          dogId={dogId}
          mindestens={3}
          hoechstens={10}
          nachAbweichung
          className="rounded-lg border border-surface-border bg-surface p-3"
          stats={stats}
        />
      )}

      {angeboten.length > 0 && (
        <div role="group" aria-label={t("Tage filtern")} className="flex flex-wrap gap-1.5">
          {angeboten.map((art) => (
            <button
              key={art}
              type="button"
              aria-pressed={filter === art}
              onClick={() => setGewaehlt(art)}
              className={cn(
                "rounded-full border px-3 py-1.5 text-sm transition-colors coarse:min-h-11",
                filter === art
                  ? "border-primary bg-primary/15 text-primary-text"
                  : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
              )}
            >
              {filterLabel[art]}
            </button>
          ))}
        </div>
      )}

      <SessionHistory
        sessions={sessions}
        dogName={dogName}
        isOwner={isOwner}
        onChanged={onChanged}
        onLoadOlder={onLoadOlder}
        // Erst fokussieren, wenn alles geladen ist: Der Verlauf darüber erscheint
        // erst mit seinen Daten und würde den Eintrag sonst nach dem Hinscrollen
        // wieder wegschieben.
        fokusEintrag={bereit ? fokusEintrag : null}
        filter={filter}
        laeufe={stats?.runs ?? null}
        gesehenBeiStart={gesehenBeiStart}
        onGesehen={onGesehen}
      />
    </div>
  );
}

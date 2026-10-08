"use client";

import { useEffect, useRef, useState } from "react";
import type { DogTrackRun, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ChevronDown, ChevronRight, History } from "lucide-react";
import { SessionDayCard } from "@/components/dogs/session-day-card";
import { SessionDayRow } from "@/components/dogs/session-day-row";

import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { feedbackKennung, istNeuesFeedback, markiereGesehen } from "@/lib/feedback-gesehen";
import { nachTagen, tagPasstZuFilter, type TagesFilter } from "@/lib/tagebuch";

// Monatsschlüssel im Format "2026-07" für die Gruppierung; toLocaleDateString
// mit month:"long" liefert die Anzeige-Version ("Juli 2026").
function monthKey(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, "0")}`;
}
function monthLabel(iso: string, ort: string): string {
  return new Date(iso).toLocaleDateString(ort, { month: "long", year: "numeric" });
}

/**
 * Trainingstagebuch: pro TRAININGSTAG eine Zeile (nicht pro Einheit), die
 * beim Antippen zur vollen Tageskarte aufklappt - alle an einem Tag erfassten
 * Übungen, Fährten und Kommentare in einem Feld. Neue Einträge desselben Tages
 * hängt das Backend ohnehin an die bestehende Einheit an (siehe TrainingService
 * "Tages-Zusammenfassung"); Alt-Daten mit mehreren Einheiten pro Tag werden hier
 * clientseitig zusammengeführt.
 * Monats-Accordion bleibt (neuester Monat automatisch aufgeklappt).
 *
 * Aufgeklappt ist nur, was die Person aufgeklappt hat - und der Tag, auf den ein
 * Link zeigt (?eintrag=). Ein Tag, der zu ist, kostet nur seine Zeile.
 *
 * onLoadOlder: lädt die komplette Historie nach (initial sind nur die
 * letzten 3 Monate geladen) - null, wenn bereits alles geladen ist.
 */
export function SessionHistory({
  sessions,
  isOwner,
  onChanged,
  onLoadOlder,
  fokusEintrag = null,
  dogName,
  filter,
  laeufe,
  gesehenBeiStart,
  onGesehen,
}: {
  sessions: TrainingSession[] | null;
  isOwner: boolean;
  /** Für das Fährtenbild zum Teilen (Schalter "Hundename zeigen"). */
  dogName?: string;
  onChanged: () => Promise<void>;
  onLoadOlder: (() => Promise<void>) | null;
  /**
   * Der Eintrag, auf den ein Link zeigt (?eintrag=): sein Monat und sein Tag
   * klappen auf, die Seite scrollt hin und hebt ihn kurz hervor. Eine Id, die es
   * in dieser Liste nicht gibt, bewirkt nichts.
   */
  fokusEintrag?: string | null;
  /** Welche Tage die Liste zeigt (die Chips über dem Tagebuch). */
  filter: TagesFilter;
  /** Ausgewertete Fährten-Abläufe des Hundes - für das Kurzbild einer Fährte; null = noch nicht geladen. */
  laeufe: readonly DogTrackRun[] | null;
  /**
   * Welches Feedback beim Öffnen der Seite schon gesehen war. Eingefroren (von
   * der Seite), damit "Neu" während dieses Besuchs stehen bleibt, obwohl es im
   * Speicher längst als gesehen gilt - beim nächsten Besuch ist es weg.
   */
  gesehenBeiStart: ReadonlySet<string>;
  /** Meldet ein Feedback, das durch Aufklappen als gesehen gilt (Kennung wie in feedback-gesehen.ts). */
  onGesehen?: (kennung: string) => void;
}) {
  const t = useT();
  const ort = ortsformat(useSprache());
  // null = Voreinstellung (neuester Monat, plus der des Fokus-Eintrags) - erst
  // der erste Tipp auf einen Monat legt die Auswahl fest. Ein leeres Set als
  // "Voreinstellung" ließ den einzigen offenen Monat nie wieder zuklappen.
  // Mit den Tagen genauso: Voreinstellung ist der Tag des Fokus-Eintrags, sonst keiner.
  const [openMonths, setOpenMonths] = useState<Set<string> | null>(null);
  const [openDays, setOpenDays] = useState<Set<string> | null>(null);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const gescrolltFuer = useRef<string | null>(null);

  // Zeigt ein neuer Link auf einen anderen Eintrag, gilt wieder die
  // Voreinstellung: Sonst bliebe der Tag zu, wenn die Person ihn vorher selbst
  // zugeklappt hat. (Zustand während des Renderns angleichen, nicht im Effekt -
  // so gibt es keinen Frame mit dem alten Stand.)
  const [fokusStand, setFokusStand] = useState(fokusEintrag);
  if (fokusEintrag !== fokusStand) {
    setFokusStand(fokusEintrag);
    setOpenMonths(null);
    setOpenDays(null);
  }

  // Erst nach Monat, darin nach Tag gruppieren. Sessions kommen nach Datum
  // absteigend vom Backend, die Gruppen erben diese Reihenfolge.
  const liste = sessions ?? [];
  const fokusSession = fokusEintrag ? (liste.find((s) => s.id === fokusEintrag) ?? null) : null;
  const monthGroups = new Map<string, Map<string, TrainingSession[]>>();
  for (const [date, daySessions] of nachTagen(liste)) {
    if (!tagPasstZuFilter(filter, daySessions)) continue;
    const mKey = monthKey(date);
    const days = monthGroups.get(mKey) ?? new Map<string, TrainingSession[]>();
    days.set(date, daySessions);
    monthGroups.set(mKey, days);
  }
  const orderedKeys = Array.from(monthGroups.keys());
  const effectiveOpen =
    openMonths ??
    new Set([...orderedKeys.slice(0, 1), ...(fokusSession ? [monthKey(fokusSession.date)] : [])]);
  const effectiveDays = openDays ?? new Set(fokusSession ? [fokusSession.date] : []);

  // Feedback gilt als gesehen, sobald sein Tag aufgeklappt ist - nicht schon,
  // wenn nur die Zeile zu sehen ist. Nur für Besitzer:innen: die Trainer:in,
  // die es schrieb, muss es nicht "sehen".
  const offeneTage = [...effectiveDays].sort().join(",");
  useEffect(() => {
    if (!isOwner || !sessions) return;
    const offen = new Set(offeneTage.split(",").filter(Boolean));
    for (const s of sessions) {
      if (s.trainerFeedback && s.feedbackAt && offen.has(s.date)) {
        markiereGesehen(s.id, s.feedbackAt);
        onGesehen?.(feedbackKennung(s.id, s.feedbackAt));
      }
    }
  }, [isOwner, sessions, offeneTage, onGesehen]);

  // Hinscrollen, sobald der Eintrag auf der Seite steht - einmal je Eintrag.
  // Ohne Feedback-Block (Eintrag ohne Feedback) bleibt die Karte des Tages das Ziel.
  useEffect(() => {
    if (!fokusSession || gescrolltFuer.current === fokusSession.id) return;
    const ziel =
      document.getElementById(`eintrag-${fokusSession.id}`) ?? document.getElementById(`tag-${fokusSession.date}`);
    if (!ziel) return;
    gescrolltFuer.current = fokusSession.id;
    // Ohne Animation: Eine weiche Bewegung bricht ab, sobald etwas darüber
    // nachlädt und das Layout verschiebt.
    ziel.scrollIntoView({ behavior: "auto", block: "center" });
  }, [fokusSession]);

  async function handleLoadOlder() {
    if (!onLoadOlder) return;
    setLoadingOlder(true);
    try {
      await onLoadOlder();
    } finally {
      setLoadingOlder(false);
    }
  }

  if (sessions === null) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  if (sessions.length === 0) {
    return (
      <Card>
        <CardContent className="py-10 text-center text-muted-foreground">
{t("Noch keine Trainingseinheiten erfasst.")}
        </CardContent>
      </Card>
    );
  }

  function toggleMonth(key: string) {
    const next = new Set(effectiveOpen);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    setOpenMonths(next);
  }

  function toggleDay(datum: string) {
    const next = new Set(effectiveDays);
    if (next.has(datum)) next.delete(datum);
    else next.add(datum);
    setOpenDays(next);
  }

  return (
    <div className="flex flex-col gap-3">
      {orderedKeys.map((mKey) => {
        const days = monthGroups.get(mKey)!;
        const isOpen = effectiveOpen.has(mKey);
        const firstDate = days.keys().next().value as string;
        const dayCount = days.size;
        return (
          // Der Monat ist die oberste Ebene der Liste und bekommt deshalb eine
          // eigene Fläche: eingefärbte Kopfzeile, ruhiger Grund darunter. So
          // ist auf einen Blick zu sehen, welche Trainingstage zusammengehören.
          <div key={mKey} className="overflow-hidden rounded-xl border border-border">
            <button
              type="button"
              onClick={() => toggleMonth(mKey)}
              aria-expanded={isOpen}
              className="flex w-full items-center justify-between gap-2 bg-secondary px-3 py-3 text-left transition-colors hover:bg-secondary/70 coarse:min-h-11"
            >
              <span className="flex min-w-0 items-center gap-2 font-heading font-semibold tracking-tight capitalize">
                {isOpen ? (
                  <ChevronDown className="size-4 shrink-0 text-primary-text" />
                ) : (
                  <ChevronRight className="size-4 shrink-0 text-primary-text" />
                )}
                <span className="truncate">{monthLabel(firstDate, ort)}</span>
              </span>
              {/* Die nackte Zahl ließ offen, was sie zählt. */}
              <Badge variant="secondary" className="shrink-0">
                {dayCount === 1 ? t("1 Tag") : t("{anzahl} Tage", { anzahl: dayCount })}
              </Badge>
            </button>
            {isOpen && (
              // Im Dark Mode ohne eigenen Grund: dort gilt "höher = heller"
              // (Material 3, Apple HIG) - die Trainingstage heben sich durch
              // ihre hellere Kartenfläche ab. Ein abgedunkelter Grund darunter
              // war genau das "Dunkelblau auf Dunkelgrau auf Schwarz".
              <div className="flex flex-col gap-2 border-t border-border bg-muted/40 p-3 dark:bg-transparent">
                {Array.from(days.entries()).map(([date, daySessions]) => {
                  const tagOffen = effectiveDays.has(date);
                  return (
                    <SessionDayRow
                      key={date}
                      tag={daySessions}
                      offen={tagOffen}
                      neu={isOwner && daySessions.some((s) => istNeuesFeedback(s, gesehenBeiStart))}
                      laeufe={laeufe}
                      onToggle={() => toggleDay(date)}
                    >
                      <SessionDayCard
                        tag={daySessions}
                        isOwner={isOwner}
                        dogName={dogName}
                        fokusId={fokusSession?.id ?? null}
                        gesehenBeiStart={gesehenBeiStart}
                        onChanged={onChanged}
                      />
                    </SessionDayRow>
                  );
                })}
              </div>
            )}
          </div>
        );
      })}
      {onLoadOlder && (
        <Button variant="outline" size="sm" className="self-center" onClick={handleLoadOlder} disabled={loadingOlder}>
          <History className="size-4" />
          {loadingOlder ? t("Lädt…") : t("Ältere Trainings anzeigen")}
        </Button>
      )}
    </div>
  );
}

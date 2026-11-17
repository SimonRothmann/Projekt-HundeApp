"use client";

import { useState } from "react";
import { CalendarDays, ChevronDown, MapPin } from "lucide-react";
import type { GroupTrainingCategory, GroupTrainingSession } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { KalenderKnopf } from "@/components/schedule/kalender-knopf";
import { AntwortKnoepfe, useTerminAntworten } from "@/components/schedule/termin-antwort";
import { kannAntworten } from "@/lib/termin-zusage";
import { terminUebersicht } from "@/lib/startseite";
import { cn } from "@/lib/utils";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { uebersetzbar } from "@/lib/i18n/sprachen";

const categoryLabel: Record<GroupTrainingCategory, string> = { 0: uebersetzbar("Welpen"), 1: uebersetzbar("Junghunde"), 2: uebersetzbar("Basis") };

const tagUndUhrzeit = (iso: string, ort: string) =>
  new Date(iso).toLocaleString(ort, { weekday: "short", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/** "Di 18:00" - für die kompakte Zeile, wenn schon geantwortet ist. */
function wochentagUndUhrzeit(iso: string, ort: string): string {
  const d = new Date(iso);
  const tag = d.toLocaleDateString(ort, { weekday: "short" }).replace(/\.$/, "");
  return `${tag} ${d.toLocaleTimeString(ort, { hour: "2-digit", minute: "2-digit" })}`;
}

/** "Sa 28.11." - für die schmale Zeile, wenn der nächste Termin weiter als eine Woche entfernt ist. */
function tagUndDatum(iso: string, ort: string): string {
  return new Date(iso).toLocaleDateString(ort, { weekday: "short", day: "2-digit", month: "2-digit" });
}

/** Die Übungen des Termins als eine Zeile ("Fußarbeit · Sitz · Platz"). */
const planZeile = (s: GroupTrainingSession) => s.items.map((i) => (i.exercise ? i.exercise.title : i.freeText)).join(" · ");

/**
 * Der nächste Gruppentermin der eigenen Gruppen (siehe
 * docs/GROUP_TRAINING_SCHEDULE.md) - nur, wenn innerhalb der nächsten 7 Tage
 * einer ansteht. Viele Vereine regeln Termine anders; ohne Termin erscheint
 * nichts, keine leere und keine werbende Karte. Liegt der nächste Termin
 * weiter weg, bleibt nur eine schmale Zeile "Nächster Termin: Sa 28.11. ·
 * Gruppe", die die Liste der kommenden Termine aufklappt - damit auch dann
 * jemand zusagen kann, ohne dass die Startseite dafür Platz hergibt.
 *
 * Offen: Tag und Uhrzeit, Gruppe, Ort, der Plan als eine Zeile und die beiden
 * Antwortknöpfe. Beantwortet: nur eine Zeile - "ändern" klappt die Knöpfe
 * wieder auf. Weitere kommende Termine (auch abgesagte, gekennzeichnet) stehen
 * einklappbar darunter; der frühere eigene Block "Nächste Gruppentrainings"
 * entfällt.
 *
 * Die Termine lädt die Startseite zusammen mit ihren übrigen Daten, damit sie
 * in einem Zug erscheint statt Abschnitt für Abschnitt.
 */
export function TerminKarte({ sessions }: { sessions: GroupTrainingSession[] }) {
  const t = useT();
  const ort = ortsformat(useSprache());
  const { angezeigt, antworten, sendet } = useTerminAntworten(sessions);
  const [aendern, setAendern] = useState(false);
  const [weitereOffen, setWeitereOffen] = useState(false);

  const uebersicht = terminUebersicht(sessions);
  if (!uebersicht.naechster && !uebersicht.fern) return null;

  const weitere = uebersicht.weitere.map(angezeigt);
  const weitereListe = (
    <div className="mt-1 flex flex-col gap-2">
      {weitere.map((s) => (
        <TerminZeile key={s.id} termin={s} ort={ort} sendet={sendet === s.id} onAntwort={(kommt) => antworten(s, kommt)} />
      ))}
    </div>
  );

  if (!uebersicht.naechster) {
    return (
      <div>
        <button
          type="button"
          aria-expanded={weitereOffen}
          onClick={() => setWeitereOffen((offen) => !offen)}
          className="flex min-h-9 w-full items-center justify-between gap-2 rounded-md px-1 text-left text-sm text-muted-foreground coarse:min-h-11"
        >
          <span className="min-w-0 [overflow-wrap:anywhere]">
            {t("Nächster Termin: {datum} · {gruppe}", { datum: tagUndDatum(uebersicht.fern!.startsAt, ort), gruppe: uebersicht.fern!.groupName })}
          </span>
          <ChevronDown className={cn("size-4 shrink-0 transition-transform", weitereOffen && "rotate-180")} aria-hidden />
        </button>
        {weitereOffen && weitereListe}
      </div>
    );
  }

  const termin = angezeigt(uebersicht.naechster);
  const beantwortet = termin.myResponse !== null;
  const knoepfeSichtbar = kannAntworten(termin) && (!beantwortet || aendern);
  const plan = planZeile(termin);

  async function antwortenUndZuklappen(s: GroupTrainingSession, kommt: boolean) {
    setAendern(false);
    await antworten(s, kommt);
  }

  return (
    <Card size="sm">
      <CardContent className="flex flex-col gap-2">
        {beantwortet ? (
          <div className="flex items-center gap-2">
            <CalendarDays className="size-4 shrink-0 text-primary-text" aria-hidden />
            <p className="min-w-0 flex-1 text-sm [overflow-wrap:anywhere]">
              <span className="font-medium">
                {wochentagUndUhrzeit(termin.startsAt, ort)} · {termin.groupName}
              </span>
              {" · "}
              <span className={termin.myResponse ? "text-primary-text" : "text-muted-foreground"}>
                {termin.myResponse ? t("Du kommst") : t("Du kommst nicht")}
              </span>
            </p>
            {termin.myResponse && <KalenderKnopf termin={termin} kompakt />}
            <button
              type="button"
              aria-expanded={aendern}
              onClick={() => setAendern((offen) => !offen)}
              className="-my-1 shrink-0 rounded-md px-2 py-1.5 text-sm font-medium text-primary-text underline-offset-4 hover:underline coarse:min-h-11"
            >
              {t("ändern")}
            </button>
          </div>
        ) : (
          <>
            <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
              <p className="flex min-w-0 items-center gap-2 font-medium [overflow-wrap:anywhere]">
                <CalendarDays className="size-4 shrink-0 text-primary-text" aria-hidden />
                <span className="min-w-0">
                  {tagUndUhrzeit(termin.startsAt, ort)} · {termin.groupName}
                </span>
              </p>
            </div>
          </>
        )}

        {/* Ort und Plan bleiben auch nach der Antwort stehen: Wer zugesagt hat, will wissen, wohin. */}
        {termin.location && (
          <p className="flex items-center gap-1 text-xs text-muted-foreground [overflow-wrap:anywhere]">
            <MapPin className="size-3 shrink-0" aria-hidden />
            {termin.location}
          </p>
        )}
        {plan && <p className="line-clamp-1 text-xs text-muted-foreground [overflow-wrap:anywhere]">{plan}</p>}

        {knoepfeSichtbar && (
          <div className="flex flex-wrap items-center gap-2">
            <AntwortKnoepfe termin={termin} gesperrt={sendet === termin.id} onAntwort={(kommt) => antwortenUndZuklappen(termin, kommt)} />
            {!beantwortet && <KalenderKnopf termin={termin} kompakt className="ml-auto" />}
          </div>
        )}

        {weitere.length > 0 && (
          <div className="border-t pt-1">
            <button
              type="button"
              aria-expanded={weitereOffen}
              onClick={() => setWeitereOffen((offen) => !offen)}
              className="flex min-h-9 w-full items-center justify-between gap-2 text-left text-sm text-muted-foreground coarse:min-h-11"
            >
              {t("Weitere Termine ({n})", { n: weitere.length })}
              <ChevronDown className={cn("size-4 shrink-0 transition-transform", weitereOffen && "rotate-180")} aria-hidden />
            </button>
            {weitereOffen && weitereListe}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * Ein weiterer Termin in der aufgeklappten Liste - wie früher im Block
 * "Nächste Gruppentrainings". Abgesagt: kein Abdunkeln des ganzen Termins (das
 * drückte auch die Schrift unter die Lesbarkeitsgrenze), sondern Titel
 * durchgestrichen und gedämpft, dazu das Abzeichen in voller Deckkraft.
 */
function TerminZeile({
  termin: s,
  ort,
  sendet,
  onAntwort,
}: {
  termin: GroupTrainingSession;
  ort: string;
  sendet: boolean;
  onAntwort: (kommt: boolean) => void;
}) {
  const t = useT();
  const plan = planZeile(s);

  return (
    <div className="rounded-md border p-2.5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className={cn("min-w-0 text-sm font-medium [overflow-wrap:anywhere]", s.status === 1 && "text-muted-foreground line-through")}>
          {tagUndUhrzeit(s.startsAt, ort)} · {s.groupName}
        </span>
        <span className="flex shrink-0 items-center gap-1">
          <Badge variant="secondary">{t(categoryLabel[s.category])}</Badge>
          {s.status === 1 && <Badge variant="destructive">{t("Abgesagt")}</Badge>}
        </span>
      </div>
      {s.location && (
        <p className="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground [overflow-wrap:anywhere]">
          <MapPin className="size-3 shrink-0" aria-hidden />
          {s.location}
        </p>
      )}
      {plan && <p className="mt-0.5 text-xs text-muted-foreground [overflow-wrap:anywhere]">{plan}</p>}
      {s.status === 0 && (
        <div className="mt-2 flex flex-wrap items-center gap-2">
          {kannAntworten(s) && <AntwortKnoepfe termin={s} gesperrt={sendet} onAntwort={onAntwort} />}
          <span className="text-xs text-muted-foreground">
            {s.attendingCount === 1 ? t("1 Zusage") : t("{n} Zusagen", { n: s.attendingCount })}
          </span>
          <KalenderKnopf termin={s} className="ml-auto" />
        </div>
      )}
    </div>
  );
}

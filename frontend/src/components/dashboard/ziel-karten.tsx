"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Check, ChevronRight, GraduationCap, Plus, Target } from "lucide-react";
import type { Dog, TrainingPlanItem } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";
import { ZielAbschliessen } from "@/components/dogs/ziel-abschliessen";
import { pruefungsName } from "@/lib/pruefung";
import type { AbgelaufenesZiel, ZielKartenEintrag } from "@/lib/startseite";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/** Wie viele offene Wochenübungen eine Zielkarte auf der Startseite zeigt, bevor „+n weitere“ kommt. */
const MAX_UEBUNGEN = 3;

const ZEILE =
  "flex min-h-11 min-w-0 items-center gap-2 rounded-lg border border-surface-border bg-surface px-3 text-left text-sm transition-colors hover:border-primary/40";

const LINK =
  "inline-flex min-h-11 items-center gap-0.5 rounded-md text-sm text-primary-text underline-offset-4 hover:underline";

/**
 * Je Hund mit laufendem Ziel EINE Karte: Prüfung und Restzeit, der Stand der
 * Woche und darunter die offenen Wochenübungen. Ein Tipp auf eine Übung öffnet
 * das Eintragen-Fenster mit dieser Übung schon gewählt.
 *
 * Sie führt, was früher zwei Blöcke waren - die Zielkarte oben und "Diese
 * Woche" weiter unten, beide mit demselben Hund, demselben Ziel und demselben
 * Link zum Plan. Wer alles erledigt hat, sieht den Stand der Woche mit Haken
 * statt einer leeren Liste.
 *
 * Bei einer Begleithundeprüfung steht (mit aktivem Sachkunde-Modul) eine kleine
 * Zeile "Sachkunde üben" in der Karte.
 */
export function ZielKarten({
  eintraege,
  sachkundeAn,
  onEintragen,
}: {
  eintraege: ZielKartenEintrag[];
  sachkundeAn: boolean;
  onEintragen: (dogId: string, item: TrainingPlanItem) => void;
}) {
  if (eintraege.length === 0) return null;

  return (
    <section className="flex flex-col gap-3">
      {eintraege.map((eintrag) => (
        <ZielKarte key={eintrag.goal.id} eintrag={eintrag} sachkundeAn={sachkundeAn} onEintragen={onEintragen} />
      ))}
    </section>
  );
}

function ZielKarte({
  eintrag,
  sachkundeAn,
  onEintragen,
}: {
  eintrag: ZielKartenEintrag;
  sachkundeAn: boolean;
  onEintragen: (dogId: string, item: TrainingPlanItem) => void;
}) {
  const t = useT();
  const [alleZeigen, setAlleZeigen] = useState(false);
  const { hund, goal, tage, fortschritt, offen, bh, weitereZiele } = eintrag;
  // Höchstens drei Übungen, je eine Zeile: Mit fünf zweizeiligen Kästen pro
  // Hund war die Startseite schon bei einem Ziel wieder zwei Bildschirme lang.
  const sichtbar = alleZeigen ? offen : offen.slice(0, MAX_UEBUNGEN);
  const versteckt = offen.length - sichtbar.length;
  const alles = fortschritt !== null && fortschritt.erledigt >= fortschritt.geplant;

  return (
    <Card size="sm">
      <CardContent className="flex flex-col gap-2">
        <p className="flex items-start gap-1.5 font-semibold [overflow-wrap:anywhere]">
          <Target className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
          <span className="min-w-0">
            {hund.name} · {pruefungsName(goal)} ·{" "}
            <span className="font-medium text-muted-foreground">
              {tage === 0 ? t("heute") : tage === 1 ? t("noch 1 Tag") : t("noch {n} Tage", { n: tage })}
            </span>
          </span>
        </p>

        {fortschritt && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            {alles && <Check className="size-4 shrink-0 text-emerald-600 dark:text-emerald-500" aria-hidden />}
            <span className="shrink-0">
              {t("Woche {woche} · {erledigt}/{geplant}", {
                woche: fortschritt.woche,
                erledigt: fortschritt.erledigt,
                geplant: fortschritt.geplant,
              })}
            </span>
            <span className="flex min-w-0 flex-1 gap-1" aria-hidden>
              {Array.from({ length: fortschritt.geplant }, (_, i) => (
                <span
                  key={i}
                  className={cn("h-1.5 min-w-0 flex-1 rounded-full", i < fortschritt.erledigt ? "bg-primary" : "bg-primary/15")}
                />
              ))}
            </span>
          </div>
        )}

        {sichtbar.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => onEintragen(hund.id, item)}
            aria-haspopup="dialog"
            aria-label={`${item.exerciseName ?? item.freeTextLabel} · ${t("{erledigt}/{ziel}× erledigt", { erledigt: item.completedCount, ziel: item.repetitionsTarget })} · ${t("Eintragen")}`}
            className="flex min-h-11 w-full items-center gap-3 rounded-lg border border-surface-border bg-surface px-3 py-1.5 text-left transition-colors hover:border-primary/40"
          >
            <span className="min-w-0 flex-1 truncate font-medium">{item.exerciseName ?? item.freeTextLabel}</span>
            <span className="shrink-0 text-sm tabular-nums text-muted-foreground">
              {item.completedCount}/{item.repetitionsTarget}
            </span>
            <Plus className="size-4 shrink-0 text-primary-text" aria-hidden />
          </button>
        ))}

        <div className="flex flex-wrap items-center gap-x-4">
          {(versteckt > 0 || (alleZeigen && offen.length > MAX_UEBUNGEN)) && (
            <button type="button" onClick={() => setAlleZeigen((a) => !a)} className={LINK}>
              {alleZeigen ? t("Weniger anzeigen") : t("+{n} weitere", { n: versteckt })}
            </button>
          )}
          <Link href={`/dogs/${hund.id}#trainingsplan`} className={LINK}>
            {t("Ganzer Plan")}
            <ChevronRight className="size-4" aria-hidden />
          </Link>
          {weitereZiele > 0 && (
            <Link href={`/dogs/${hund.id}#trainingsplan`} className={LINK}>
              {t("Weitere Ziele ({n})", { n: weitereZiele })}
              <ChevronRight className="size-4" aria-hidden />
            </Link>
          )}
          {bh && sachkundeAn && (
            <Link href="/sachkunde" className={LINK}>
              <GraduationCap className="size-4" aria-hidden />
              {t("Sachkunde üben")}
              <ChevronRight className="size-4" aria-hidden />
            </Link>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * Ziele, deren Prüfungstag vorbei ist und die auf ihr Ergebnis warten: eine
 * Zeile je Ziel. Ein Tipp öffnet den Dialog "Wie lief die Prüfung?" gleich hier
 * auf der Startseite; nach dem Speichern lädt sie neu.
 *
 * Für ein Folgeziel nach "bestanden" geht es zur Hundeseite: Das Formular zum
 * Anlegen sitzt dort bei den Zielen.
 */
export function AbgelaufeneZiele({
  eintraege,
  onChanged,
}: {
  eintraege: AbgelaufenesZiel[];
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const router = useRouter();
  if (eintraege.length === 0) return null;

  return (
    <section className="flex flex-col gap-2">
      {eintraege.map(({ hund, goal }) => (
        <ZielAbschliessen
          key={goal.id}
          goal={goal}
          hundName={hund.name}
          onChanged={onChanged}
          onFolgeziel={() => router.push(`/dogs/${hund.id}#trainingsplan`)}
          ausloeser={(oeffnen) => (
            <button type="button" onClick={oeffnen} className={ZEILE}>
              <Target className="size-4 shrink-0 text-primary-text" aria-hidden />
              <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">
                <span className="font-medium">
                  {hund.name} · {pruefungsName(goal)}
                </span>{" "}
                <span className="text-muted-foreground">· {t("Ergebnis fehlt")}</span>
              </span>
              <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
            </button>
          )}
        />
      ))}
    </section>
  );
}

/**
 * Hunde ohne Ziel: je eine schmale Zeile "Bello · Prüfungsziel setzen" statt
 * der früheren großen Werbekarte. Führt dorthin, wo das Ziel angelegt wird.
 */
export function OhneZielZeilen({ hunde }: { hunde: Dog[] }) {
  const t = useT();
  if (hunde.length === 0) return null;

  return (
    <section className="flex flex-col gap-2">
      {hunde.map((hund) => (
        <Link key={hund.id} href={`/dogs/${hund.id}#trainingsplan`} className={ZEILE}>
          <Target className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">
            <span className="font-medium">{hund.name}</span> <span className="text-muted-foreground">· {t("Prüfungsziel setzen")}</span>
          </span>
          <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        </Link>
      ))}
    </section>
  );
}

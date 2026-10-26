"use client";

import { useEffect, useState, type ComponentType } from "react";
import Link from "next/link";
import { ChevronRight, CircleCheck, CircleDashed, CirclePause, CircleX, TriangleAlert } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { pruefungsName } from "@/lib/pruefung";
import { usePreferences } from "@/lib/preferences-context";
import {
  AMPEL_WORT,
  REGELN,
  ZUSTAND_WORT,
  ampelSatz,
  chipText,
  grundText,
  teileUebungen,
  theorieLink,
  theorieText,
  zeitText,
} from "@/lib/pruefungsreife";
import { MODULE, REIFE_AMPEL, REIFE_ZUSTAND } from "@/lib/types";
import type { Goal, Pruefungsreife, PruefungsreifeUebung, ReifeAmpel, ReifeZustand } from "@/lib/types";
import { ZieldatumHinweis } from "@/components/dogs/zieldatum-hinweis";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";

type Symbol = ComponentType<{ className?: string; "aria-hidden"?: boolean }>;

// Jede Ampelfarbe hat auch eine eigene Form (Haken, Warndreieck, Kreuz) und ein Wort:
// Nichts hängt allein an der Farbe.
const AMPEL: Record<ReifeAmpel, { symbol: Symbol; klassen: string }> = {
  [REIFE_AMPEL.Gruen]: {
    symbol: CircleCheck,
    klassen: "border-emerald-600/40 bg-emerald-500/10 text-emerald-800 dark:text-emerald-300",
  },
  [REIFE_AMPEL.Gelb]: {
    symbol: TriangleAlert,
    klassen: "border-amber-500/40 bg-amber-500/10 text-amber-800 dark:text-amber-300",
  },
  [REIFE_AMPEL.Rot]: {
    symbol: CircleX,
    klassen: "border-red-600/40 bg-red-500/10 text-red-800 dark:text-red-300",
  },
};

const ZUSTAND: Record<ReifeZustand, { symbol: Symbol; klassen: string }> = {
  [REIFE_ZUSTAND.Sicher]: { symbol: CircleCheck, klassen: "text-emerald-700 dark:text-emerald-400" },
  [REIFE_ZUSTAND.Wackelig]: { symbol: TriangleAlert, klassen: "text-amber-700 dark:text-amber-400" },
  [REIFE_ZUSTAND.Offen]: { symbol: CircleDashed, klassen: "text-muted-foreground" },
};

/**
 * Die Prüfungsreife eines Ziels: ein Chip mit Ampel und Zahlen ("Prüfungsreife:
 * gelb · 2 offen, 3 wackelig") und dahinter, per Tipp, die ausführliche Ansicht
 * als Fenster von unten. Sie sitzt auf der Zielkarte der Startseite und in der
 * Planansicht der Hundeseite - dort sehen sie auch betreuende Trainer:innen
 * (dann ohne Theoriezeile, die liefert der Server ihnen nicht aus).
 *
 * Der Chip kommt mit den Zielen (Goal.pruefungsreife), die Einzelheiten werden
 * erst beim Öffnen geladen. Es ist eine Faustregel aus dem Tagebuch, kein Urteil
 * der Richter:in; das sagt die Ansicht an der Stelle, an der es darauf ankommt.
 */
export function PruefungsreifeKarte({ goal, onChanged }: { goal: Goal; onChanged: () => Promise<void> }) {
  const t = useT();
  const sprache = useSprache();
  const [offen, setOffen] = useState(false);
  const kurz = goal.pruefungsreife;

  if (!kurz || goal.status !== 0) return null;

  const { symbol: Zeichen, klassen } = AMPEL[kurz.ampel];

  return (
    <>
      <button
        type="button"
        onClick={() => setOffen(true)}
        aria-haspopup="dialog"
        className={cn(
          "flex min-h-11 w-full min-w-0 items-center gap-2 rounded-md border px-3 py-2 text-left text-sm font-medium transition-colors hover:brightness-95",
          klassen,
        )}
      >
        <Zeichen className="size-4 shrink-0" aria-hidden />
        <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">{chipText(t, kurz)}</span>
        <ChevronRight className="size-4 shrink-0" aria-hidden />
      </button>

      {/* Neben dem Chip und nicht erst im Fenster: Gerade wenn es knapp ist, soll der Hinweis ohne Tipp sichtbar sein.
          Hat die Pause die Zeit schon zu knapp gemacht, steht der Pausenhinweis der Karte selbst da - nicht doppelt. */}
      {kurz.zieldatumVerschieben && !goal.zieldatumZuKnapp && (
        <ZieldatumHinweis goal={goal} onChanged={onChanged} anlass="pruefungsreife" />
      )}

      <Sheet open={offen} onOpenChange={setOffen}>
        <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto pb-[max(1rem,env(safe-area-inset-bottom))]">
          <SheetHeader>
            <SheetTitle>{t("Prüfungsreife")}</SheetTitle>
            <SheetDescription className="[overflow-wrap:anywhere]">{pruefungsName(goal, sprache)}</SheetDescription>
          </SheetHeader>
          {offen && <Ansicht goal={goal} />}
        </SheetContent>
      </Sheet>
    </>
  );
}

/** Die ausführliche Ansicht; lädt bei jedem Öffnen frisch, weil sich der Stand mit jedem Training ändert. */
function Ansicht({ goal }: { goal: Goal }) {
  const t = useT();
  const sprache = useSprache();
  const [reife, setReife] = useState<Pruefungsreife | null>(null);
  const [fehler, setFehler] = useState<string | null>(null);

  useEffect(() => {
    let aktuell = true;
    api
      .get<Pruefungsreife>(`/api/goals/${goal.id}/readiness`)
      .then((antwort) => {
        if (aktuell) setReife(antwort);
      })
      .catch((err) => {
        if (aktuell) setFehler(err instanceof ApiError ? err.message : t("Die Prüfungsreife konnte nicht geladen werden."));
      });
    return () => {
      aktuell = false;
    };
    // Nur beim Öffnen laden; ein Sprachwechsel soll keinen neuen Abruf auslösen.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [goal.id]);

  if (fehler) return <p className="px-4 pb-4 text-sm text-destructive">{fehler}</p>;
  if (!reife) return <p className="px-4 pb-4 text-sm text-muted-foreground">{t("Lädt…")}</p>;

  const { symbol: Zeichen, klassen } = AMPEL[reife.ampel];
  const { fehlend, sicher } = teileUebungen(reife.uebungen);

  return (
    <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      <div className={cn("flex min-w-0 items-start gap-3 rounded-lg border px-3 py-3", klassen)}>
        <Zeichen className="mt-0.5 size-8 shrink-0" aria-hidden />
        <div className="flex min-w-0 flex-col gap-0.5">
          <p className="text-lg font-semibold">{t(AMPEL_WORT[reife.ampel])}</p>
          <p className="[overflow-wrap:anywhere]">{ampelSatz(t, reife)}</p>
        </div>
      </div>

      <div className="flex min-w-0 flex-col gap-1 text-sm">
        <p className="font-medium [overflow-wrap:anywhere]">{zeitText(t, reife)}</p>
        {reife.pausiert && (
          <p className="flex min-w-0 items-start gap-2 text-amber-800 dark:text-amber-300">
            <CirclePause className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span className="min-w-0 [overflow-wrap:anywhere]">
              {t("Der Hund pausiert gerade. Die Pausentage zählen nicht als Trainingszeit und lassen eine Übung nicht verfallen.")}
            </span>
          </p>
        )}
      </div>

      {fehlend.length > 0 && (
        <UebungsListe titel={t("Das fehlt noch")} uebungen={fehlend} sprache={sprache} />
      )}
      {sicher.length > 0 && <UebungsListe titel={t("Das sitzt")} uebungen={sicher} sprache={sprache} />}

      {reife.theorie && (
        <section className="flex min-w-0 flex-col gap-1.5">
          <h3 className="text-sm font-semibold">{t("Theorie (Sachkunde)")}</h3>
          <TheorieZeile theorie={reife.theorie} />
        </section>
      )}

      <Berechnung mitTheorie={reife.theorie !== null} />
    </div>
  );
}

function UebungsListe({
  titel,
  uebungen,
  sprache,
}: {
  titel: string;
  uebungen: PruefungsreifeUebung[];
  sprache: ReturnType<typeof useSprache>;
}) {
  const t = useT();
  return (
    <section className="flex min-w-0 flex-col gap-1.5">
      <h3 className="text-sm font-semibold">
        {titel} <span className="font-normal text-muted-foreground">({uebungen.length})</span>
      </h3>
      <ul className="flex flex-col gap-1.5">
        {uebungen.map((u) => {
          const { symbol: Zeichen, klassen } = ZUSTAND[u.zustand];
          return (
            <li
              key={u.exerciseId}
              className="flex min-w-0 items-start gap-2 rounded-lg border border-surface-border bg-surface px-3 py-2 text-sm"
            >
              <Zeichen className={cn("mt-0.5 size-4 shrink-0", klassen)} aria-hidden />
              <div className="flex min-w-0 flex-1 flex-col">
                <span className="font-medium [overflow-wrap:anywhere]">
                  {katalogName({ name: u.name, nameEn: u.nameEn }, sprache)}
                  {!u.isMandatory && <span className="font-normal text-muted-foreground"> · {t("Wahlübung")}</span>}
                </span>
                <span className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
                  <span className={cn("font-medium", klassen)}>{t(ZUSTAND_WORT[u.zustand])}</span> · {grundText(t, u, sprache)}
                </span>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function TheorieZeile({ theorie }: { theorie: NonNullable<Pruefungsreife["theorie"]> }) {
  const t = useT();
  const { moduleEnabled } = usePreferences();
  const { symbol: Zeichen, klassen } = ZUSTAND[theorie.zustand];
  const link = theorieLink(t, theorie);

  return (
    <div className="flex min-w-0 items-start gap-2 rounded-lg border border-surface-border bg-surface px-3 py-2 text-sm">
      <Zeichen className={cn("mt-0.5 size-4 shrink-0", klassen)} aria-hidden />
      <div className="flex min-w-0 flex-1 flex-col">
        <span className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
          <span className={cn("font-medium", klassen)}>{t(ZUSTAND_WORT[theorie.zustand])}</span> · {theorieText(t, theorie)}
        </span>
        {/* Ohne das Sachkunde-Modul in den Einstellungen gibt es die Seite nicht - dann kein Link ins Leere. */}
        {moduleEnabled(MODULE.sachkunde) && (
          <Link
            href={link.href}
            className="mt-1 inline-flex min-h-11 w-fit items-center gap-0.5 rounded-md text-primary-text underline-offset-4 hover:underline"
          >
            {link.text}
            <ChevronRight className="size-4" aria-hidden />
          </Link>
        )}
      </div>
    </div>
  );
}

/** "Wie wird das berechnet?" - die Regeln in Klartext, damit niemand an ein Urteil glaubt, das keines ist. */
function Berechnung({ mitTheorie }: { mitTheorie: boolean }) {
  const t = useT();
  return (
    <details className="group min-w-0 rounded-lg border border-surface-border px-3 py-2 text-sm">
      <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-2 font-medium [&::-webkit-details-marker]:hidden">
        {t("Wie wird das berechnet?")}
        <ChevronRight className="size-4 shrink-0 transition-transform group-open:rotate-90" aria-hidden />
      </summary>
      <div className="flex min-w-0 flex-col gap-2 pb-2 text-muted-foreground [overflow-wrap:anywhere]">
        <p>{t("Das ist eine Faustregel aus eurem Trainingstagebuch, kein Urteil der Richter:in. Am Prüfungstag zählt, was ihr zeigt.")}</p>
        <p>
          {t(
            "Sicher: Die Übung ist wiederholt gut gelaufen, ohne zwischendurch abzurutschen, die Bewertungen liegen im Schnitt bei {note} oder besser, und das letzte Training ist höchstens {tage} Tage her. Tage, an denen der Hund pausiert hat, zählen dabei nicht mit.",
            { note: REGELN.sicherBewertung, tage: REGELN.sicherTage },
          )}
        </p>
        <p>{t("Wackelig: Die Übung wurde trainiert, sitzt aber noch nicht sicher – sie ist zu selten gut gelaufen, schwach bewertet oder zu lange her.")}</p>
        <p>{t("Offen: Die Übung wurde noch nie trainiert.")}</p>
        <p>
          {t(
            "Die Ampel zählt die Übungen nach ihren Höchstpunkten; ohne Punkte zählt jede gleich. Wahlübungen zählen nur halb.",
          )}
        </p>
        <p>
          {t(
            "Grün: keine Pflichtübung ist offen und mindestens {gruen} % sind sicher. Rot: eine Pflichtübung ist noch offen oder weniger als {rot} % sind sicher. Dazwischen steht die Ampel auf gelb.",
            { gruen: REGELN.gruenProzent, rot: REGELN.rotProzent },
          )}
        </p>
        {mitTheorie && (
          <p>{t("Bei der Begleithundeprüfung gehört die Theorie dazu: Sie ist sicher, wenn du die letzten beiden Übungsprüfungen bestanden hast. Nur du siehst diese Zeile, nicht deine Trainer:in.")}</p>
        )}
        <p>
          {t(
            "Steht die Ampel auf rot und es sind höchstens {wochen} Wochen bis zur Prüfung, fragt Dogity, ob das Zieldatum verschoben werden soll. Verschoben wird nie von selbst.",
            { wochen: REGELN.verschiebenWochen },
          )}
        </p>
      </div>
    </details>
  );
}

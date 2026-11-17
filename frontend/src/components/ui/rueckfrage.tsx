"use client";

import { createContext, useCallback, useContext, useEffect, useId, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useT } from "@/lib/i18n";
import { eingabePasst } from "@/lib/rueckfrage";

export type RueckfrageOptionen = {
  titel: string;
  text?: string;
  /** Beschriftung des bestätigenden Knopfs - die Handlung ("Löschen"), nicht "OK". */
  bestaetigen: string;
  abbrechen?: string;
  /** Unumkehrbares: der bestätigende Knopf wird rot. */
  gefaehrlich?: boolean;
  /** Bestätigen wird erst aktiv, wenn `muss` genau eingetippt ist. */
  eingabe?: { label: string; muss: string };
};

type Rueckfrage = (optionen: RueckfrageOptionen) => Promise<boolean>;

const RueckfrageContext = createContext<Rueckfrage | undefined>(undefined);

/**
 * Die eine Rückfrage der App - ersetzt die Browser-Dialoge, die in der
 * installierten App die Adresse zeigen und nicht zum Rest passen.
 * `await rueckfrage({...})` liefert true bei Bestätigen, false bei Abbrechen,
 * Escape oder Tipp neben das Fenster.
 */
export function RueckfrageProvider({ children }: { children: ReactNode }) {
  const [offen, setOffen] = useState<{ optionen: RueckfrageOptionen; entscheide: (ja: boolean) => void } | null>(null);

  const rueckfrage = useCallback<Rueckfrage>(
    (optionen) =>
      new Promise<boolean>((resolve) => {
        // Eine noch offene Frage gilt als abgebrochen, statt ewig zu hängen.
        setOffen((alt) => {
          alt?.entscheide(false);
          return { optionen, entscheide: resolve };
        });
      }),
    [],
  );

  const schliesse = useCallback((ja: boolean) => {
    setOffen((aktuell) => {
      aktuell?.entscheide(ja);
      return null;
    });
  }, []);

  return (
    <RueckfrageContext.Provider value={rueckfrage}>
      {children}
      {offen && <RueckfrageFenster optionen={offen.optionen} onEntscheidung={schliesse} />}
    </RueckfrageContext.Provider>
  );
}

export function useRueckfrage(): Rueckfrage {
  const wert = useContext(RueckfrageContext);
  if (!wert) throw new Error("useRueckfrage braucht den RueckfrageProvider.");
  return wert;
}

const ZEIGER_EREIGNISSE = ["pointerdown", "mousedown", "touchstart"] as const;

function RueckfrageFenster({
  optionen,
  onEntscheidung,
}: {
  optionen: RueckfrageOptionen;
  onEntscheidung: (ja: boolean) => void;
}) {
  const t = useT();
  const titelId = useId();
  const textId = useId();
  const eingabeId = useId();
  const fensterRef = useRef<HTMLDivElement>(null);
  const abbrechenRef = useRef<HTMLButtonElement>(null);
  const [eingabe, setEingabe] = useState("");
  const darfBestaetigen = !optionen.eingabe || eingabePasst(eingabe, optionen.eingabe.muss);
  // Der Escape-Hörer unten lebt länger als ein Render - er ruft die jeweils
  // aktuelle Funktion über diese Ref auf.
  const entscheidungRef = useRef(onEntscheidung);
  useEffect(() => {
    entscheidungRef.current = onEntscheidung;
  });

  // Fokus: auf "Abbrechen" (sicherer Standard), danach zurück dorthin, wo er war.
  useEffect(() => {
    const davor = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    abbrechenRef.current?.focus();
    return () => {
      if (davor?.isConnected) davor.focus();
    };
  }, []);

  // Escape, Tab-Falle und Tipp ins Fenster laufen als Hörer im Fangmodus am
  // window: Ein Sheet darunter (Base UI) lauscht am document auf dieselben
  // Ereignisse und würde sich sonst mitschließen oder den Fokus zurückholen.
  useEffect(() => {
    function imFenster(ziel: EventTarget | null) {
      return ziel instanceof Node && !!fensterRef.current?.contains(ziel);
    }
    function beiTaste(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.stopImmediatePropagation();
        e.preventDefault();
        entscheidungRef.current(false);
        return;
      }
      if (e.key !== "Tab" || !fensterRef.current) return;
      e.stopImmediatePropagation();
      const fokussierbar = Array.from(
        fensterRef.current.querySelectorAll<HTMLElement>("button:not([disabled]), input:not([disabled])"),
      );
      if (fokussierbar.length === 0) return;
      const erstes = fokussierbar[0];
      const letztes = fokussierbar[fokussierbar.length - 1];
      const aktiv = document.activeElement;
      if (!imFenster(aktiv) || (e.shiftKey && aktiv === erstes)) {
        e.preventDefault();
        (e.shiftKey ? letztes : erstes).focus();
      } else if (!e.shiftKey && aktiv === letztes) {
        e.preventDefault();
        erstes.focus();
      }
    }
    function beiZeiger(e: Event) {
      if (imFenster(e.target)) e.stopPropagation();
    }
    window.addEventListener("keydown", beiTaste, true);
    for (const art of ZEIGER_EREIGNISSE) window.addEventListener(art, beiZeiger, true);
    return () => {
      window.removeEventListener("keydown", beiTaste, true);
      for (const art of ZEIGER_EREIGNISSE) window.removeEventListener(art, beiZeiger, true);
    };
  }, []);

  return createPortal(
    // z-[1300]: über dem Aufnahme-Vollbild (z-50, Fangschicht z-[1100]) und
    // über offenen Sheets (z-50).
    <div className="fixed inset-0 z-[1300] flex items-end justify-center sm:items-center" data-slot="rueckfrage">
      <div
        className="absolute inset-0 bg-black/50 supports-backdrop-filter:backdrop-blur-md"
        onClick={() => onEntscheidung(false)}
        aria-hidden="true"
      />
      <div
        ref={fensterRef}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titelId}
        aria-describedby={optionen.text ? textId : undefined}
        className="relative flex w-full max-w-sm flex-col gap-3 rounded-t-xl border-t bg-popover p-4 pb-[max(1rem,env(safe-area-inset-bottom))] text-sm text-popover-foreground shadow-lg sm:rounded-xl sm:border"
      >
        <h2 id={titelId} className="font-heading text-base font-medium text-foreground [overflow-wrap:anywhere]">
          {optionen.titel}
        </h2>
        {optionen.text && (
          <p id={textId} className="text-muted-foreground [overflow-wrap:anywhere]">
            {optionen.text}
          </p>
        )}
        {optionen.eingabe && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (darfBestaetigen) onEntscheidung(true);
            }}
            className="flex flex-col gap-1.5"
          >
            <Label htmlFor={eingabeId} className="[overflow-wrap:anywhere]">
              {optionen.eingabe.label}
            </Label>
            <Input
              id={eingabeId}
              value={eingabe}
              onChange={(e) => setEingabe(e.target.value)}
              autoComplete="off"
              autoCapitalize="off"
              spellCheck={false}
            />
          </form>
        )}
        <div className="flex gap-2 pt-1">
          <Button ref={abbrechenRef} type="button" variant="outline" className="flex-1" onClick={() => onEntscheidung(false)}>
            {optionen.abbrechen ?? t("Abbrechen")}
          </Button>
          <Button
            type="button"
            variant={optionen.gefaehrlich ? "destructive" : "default"}
            className="flex-1"
            disabled={!darfBestaetigen}
            onClick={() => onEntscheidung(true)}
          >
            {optionen.bestaetigen}
          </Button>
        </div>
      </div>
    </div>,
    document.body,
  );
}

"use client";

import { createContext, useContext, useEffect, useMemo, type ReactNode } from "react";
import { EN } from "./en";
import { bestimmeSprache, VORGABE_SPRACHE, type Sprache } from "./sprachen";

/**
 * Übersetzung der Oberfläche.
 *
 * Der Schlüssel ist der deutsche Satz selbst, nicht eine erfundene Kennung
 * wie "dogs.title". Das ist eine bewusste Entscheidung:
 *
 * - Der Quelltext bleibt lesbar. Ein Aufruf mit "Meine Hunde" sagt beim
 *   Lesen, was dasteht; eine Kennung wie dogs.list.heading verlangt einen
 *   zweiten Blick in eine andere Datei.
 * - Der Rückfall ist von selbst richtig. Fehlt eine Übersetzung, erscheint
 *   der deutsche Satz - also genau das, was vorher dastand. Bei erfundenen
 *   Kennungen erschiene die Kennung selbst auf dem Bildschirm.
 * - Es gibt keine zweite Liste, die mit der ersten auseinanderlaufen kann.
 *
 * Der Preis: Wer den deutschen Text ändert, verliert die Übersetzung. Das
 * fällt aber nicht ins Leere - i18n.test.ts liest alle Aufrufe aus dem
 * Quelltext und meldet jeden, für den keine englische Fassung existiert.
 * Genau diese Prüfung könnte eine Typprüfung mit erfundenen Kennungen NICHT
 * leisten: Sie sähe nur, dass ein Schlüssel existiert, nicht dass er auch
 * übersetzt ist.
 */
type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

type I18nWert = {
  sprache: Sprache;
  t: Uebersetzer;
};

const I18nContext = createContext<I18nWert | undefined>(undefined);

/**
 * Setzt Platzhalter der Form {name} ein.
 *
 * Bewusst so schlicht: Zahlwörter, Geschlechter und Fälle löst diese App
 * über eigene Sätze ("1 Hund" / "{n} Hunde") und nicht über eine
 * Formatsprache, die man erst lernen muss.
 */
function einsetzen(vorlage: string, werte?: Record<string, string | number>): string {
  if (!werte) return vorlage;
  return vorlage.replace(/\{(\w+)\}/g, (treffer, name: string) =>
    name in werte ? String(werte[name]) : treffer,
  );
}

export function uebersetze(sprache: Sprache, text: string, werte?: Record<string, string | number>): string {
  const fassung = sprache === "de" ? text : (EN[text] ?? text);
  return einsetzen(fassung, werte);
}

export function I18nProvider({ sprache, children }: { sprache: Sprache; children: ReactNode }) {
  // Die Sprache gehört auch ins lang-Attribut: Sie steuert Silbentrennung,
  // die Aussprache durch Screenreader und das Übersetzungsangebot des
  // Browsers. Ohne das behauptete die Seite weiterhin, deutsch zu sein.
  useEffect(() => {
    document.documentElement.lang = sprache;
  }, [sprache]);

  const wert = useMemo<I18nWert>(
    () => ({ sprache, t: (text, werte) => uebersetze(sprache, text, werte) }),
    [sprache],
  );

  return <I18nContext.Provider value={wert}>{children}</I18nContext.Provider>;
}

/**
 * Außerhalb des Providers (Server-Rendern, öffentliche Seiten) gilt Deutsch.
 * Bewusst ohne Ausnahme: Ein Fehler wäre hier die schlechtere Antwort - die
 * Seite bliebe leer, obwohl der deutsche Text vollständig vorliegt.
 */
export function useT(): Uebersetzer {
  const context = useContext(I18nContext);
  return context?.t ?? ((text, werte) => uebersetze(VORGABE_SPRACHE, text, werte));
}

export function useSprache(): Sprache {
  return useContext(I18nContext)?.sprache ?? VORGABE_SPRACHE;
}

export { bestimmeSprache };
export type { Sprache };

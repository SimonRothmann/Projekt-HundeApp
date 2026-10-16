"use client";

import { createContext, Fragment, useContext, useEffect, useMemo, type ReactNode } from "react";
import { EN } from "./en";
import { aktuelleSprache, setzeAktuelleSprache } from "./aktuelle-sprache";
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

/**
 * Servermeldungen mit eingesetzten Werten ("... höchstens 500 Zeichen ...").
 *
 * Der Server schickt den fertigen deutschen Satz. Im Wörterbuch steht er mit
 * Platzhaltern ({hoechstens}); hier wird aus jedem solchen Eintrag ein
 * Suchmuster, das auf den fertigen Satz passt und die Werte herauszieht.
 *
 * Der Schlüssel muss genug festen Text haben: Ein Eintrag wie "{name}" oder
 * "{n} Hunde" träfe sonst jede beliebige Meldung.
 */
type Vorlage = { muster: RegExp; namen: string[]; englisch: string };
let vorlagen: Vorlage[] | null = null;
const MIN_FESTTEXT = 12;

function ladeVorlagen(): Vorlage[] {
  vorlagen ??= Object.keys(EN)
    .filter((schluessel) => /\{\w+\}/.test(schluessel) && schluessel.replace(/\{\w+\}/g, "").length >= MIN_FESTTEXT)
    // Der längere feste Text zuerst: Er ist spezifischer.
    .sort((a, b) => b.replace(/\{\w+\}/g, "").length - a.replace(/\{\w+\}/g, "").length)
    .map((schluessel) => {
      const namen: string[] = [];
      const muster = schluessel
        .split(/(\{\w+\})/)
        .map((teil) => {
          const name = /^\{(\w+)\}$/.exec(teil)?.[1];
          if (name === undefined) return teil.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
          namen.push(name);
          return "(.+?)";
        })
        .join("");
      return { muster: new RegExp(`^${muster}$`, "s"), namen, englisch: EN[schluessel] };
    });
  return vorlagen;
}

/**
 * Übersetzt eine Fehlermeldung des Servers (siehe ApiError in lib/api.ts).
 *
 * Erst der Satz als Ganzes, dann die Platzhalter-Fassungen. Eingesetzte Werte
 * werden ihrerseits nachgeschlagen: "Die Notiz ist zu lang (höchstens 500
 * Zeichen)." trägt als Wert "Die Notiz" ein Wort, das übersetzt gehört. Was
 * sich nicht übersetzen lässt, bleibt Deutsch - der Rückfall wie überall.
 */
export function uebersetzeMeldung(sprache: Sprache, meldung: string): string {
  if (sprache === "de") return meldung;
  if (Object.hasOwn(EN, meldung)) return EN[meldung];
  for (const { muster, namen, englisch } of ladeVorlagen()) {
    const treffer = muster.exec(meldung);
    if (!treffer) continue;
    const werte: Record<string, string> = {};
    namen.forEach((name, index) => {
      const wert = treffer[index + 1];
      werte[name] = Object.hasOwn(EN, wert) ? EN[wert] : wert;
    });
    return einsetzen(englisch, werte);
  }
  return meldung;
}

/**
 * Setzt Bausteine (Links, Fettdruck) in einen übersetzten Satz ein.
 *
 * Für Sätze, in denen mitten im Text ein Verweis steht: Die Wortstellung ist
 * je Sprache anders, ein Satz in drei Stücken ließe sich nicht übersetzen.
 * Stattdessen steht der Verweis als {name} im Satz, und hier wird an dieser
 * Stelle der Baustein eingesetzt. Unbekannte Platzhalter bleiben stehen.
 */
export function mitBausteinen(satz: string, bausteine: Record<string, ReactNode>): ReactNode {
  return satz.split(/(\{\w+\})/).map((teil, index) => {
    const name = /^\{(\w+)\}$/.exec(teil)?.[1];
    return name !== undefined && name in bausteine ? <Fragment key={index}>{bausteine[name]}</Fragment> : teil;
  });
}

/**
 * Übersetzt in die Sprache, die der Provider zuletzt gemeldet hat.
 *
 * Für Code ohne Hook: Fehler aus reinen lib-Funktionen, ApiError. Wer in einer
 * Komponente ist, nimmt useT() - das zieht beim Sprachwechsel von selbst nach.
 * Hier wird erst beim Aufruf übersetzt, also gilt die Sprache dieses Moments.
 */
export function uebersetzeAktuell(text: string, werte?: Record<string, string | number>): string {
  return uebersetze(aktuelleSprache(), text, werte);
}

export function I18nProvider({ sprache, children }: { sprache: Sprache; children: ReactNode }) {
  // Die Sprache gehört auch ins lang-Attribut: Sie steuert Silbentrennung,
  // die Aussprache durch Screenreader und das Übersetzungsangebot des
  // Browsers. Ohne das behauptete die Seite weiterhin, deutsch zu sein.
  useEffect(() => {
    document.documentElement.lang = sprache;
    setzeAktuelleSprache(sprache);
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

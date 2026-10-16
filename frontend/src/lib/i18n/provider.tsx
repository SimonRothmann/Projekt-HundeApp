"use client";

import { useEffect, useState, type ReactNode } from "react";
import { usePreferences } from "@/lib/preferences-context";
import { I18nProvider } from "./index";
import { leseLokaleSprache, SPRACH_EREIGNIS } from "./lokale-sprache";
import { bestimmeSprache, istSprache, VORGABE_SPRACHE, type Sprache } from "./sprachen";

/**
 * Verbindet die gespeicherte Einstellung mit der Übersetzung.
 *
 * Die Gerätesprache wird erst im Browser gelesen und nicht schon beim
 * Server-Rendern. Das ist kein Versäumnis: navigator.languages gibt es auf
 * dem Server nicht, und würde man dort raten, unterschiede sich das
 * ausgelieferte HTML vom ersten Rendern im Browser - React meldet dann eine
 * Hydration-Abweichung und verwirft den Baum.
 *
 * Bis dahin gilt die Vorgabe. Wer Englisch eingestellt hat, sieht also einen
 * Wimpernschlag lang Deutsch - der Preis dafür, dass die Sprache nicht in
 * der Adresse steht (siehe sprachen.ts). Das gilt auch für die lokale Wahl
 * ohne Konto (lokale-sprache.ts): Das ausgelieferte HTML der öffentlichen
 * Seiten bleibt deutsch - so wird die Startseite gefunden -, und der Browser
 * wechselt nach dem Hydrieren auf die gewählte Sprache.
 *
 * Reihenfolge: Konto, dann lokale Wahl, dann Gerät, dann Deutsch.
 */
export function SprachProvider({ children }: { children: ReactNode }) {
  const { preferences } = usePreferences();
  const [geraetesprachen, setGeraetesprachen] = useState<readonly string[]>([]);
  const [lokaleWahl, setLokaleWahl] = useState<Sprache | null>(null);

  useEffect(() => {
    // Externe Quelle (Browser), erst nach dem Einhängen verfügbar.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setGeraetesprachen(navigator.languages ?? [navigator.language]);
    setLokaleWahl(leseLokaleSprache());

    // Der Umschalter meldet eine Wahl über ein Ereignis (gleiche Seite) bzw.
    // das storage-Ereignis (anderer Tab): So wirkt sie sofort und ohne
    // Neuladen, und mehrere offene Tabs bleiben einig.
    const beiWahl = (ereignis: Event) => {
      const wert = (ereignis as CustomEvent<unknown>).detail;
      setLokaleWahl(istSprache(wert) ? wert : leseLokaleSprache());
    };
    const beiSpeicher = () => setLokaleWahl(leseLokaleSprache());
    window.addEventListener(SPRACH_EREIGNIS, beiWahl);
    window.addEventListener("storage", beiSpeicher);
    return () => {
      window.removeEventListener(SPRACH_EREIGNIS, beiWahl);
      window.removeEventListener("storage", beiSpeicher);
    };
  }, []);

  const gewaehlt = preferences.locale ?? lokaleWahl;
  const sprache: Sprache = geraetesprachen.length === 0 && !gewaehlt
    ? VORGABE_SPRACHE
    : bestimmeSprache(gewaehlt, geraetesprachen);

  return <I18nProvider sprache={sprache}>{children}</I18nProvider>;
}

"use client";

import { useEffect, useState, type ReactNode } from "react";
import { usePreferences } from "@/lib/preferences-context";
import { I18nProvider } from "./index";
import { bestimmeSprache, VORGABE_SPRACHE, type Sprache } from "./sprachen";

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
 * der Adresse steht (siehe sprachen.ts).
 */
export function SprachProvider({ children }: { children: ReactNode }) {
  const { preferences } = usePreferences();
  const [geraetesprachen, setGeraetesprachen] = useState<readonly string[]>([]);

  useEffect(() => {
    // Externe Quelle (Browser), erst nach dem Einhängen verfügbar.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setGeraetesprachen(navigator.languages ?? [navigator.language]);
  }, []);

  const sprache: Sprache = geraetesprachen.length === 0 && !preferences.locale
    ? VORGABE_SPRACHE
    : bestimmeSprache(preferences.locale, geraetesprachen);

  return <I18nProvider sprache={sprache}>{children}</I18nProvider>;
}

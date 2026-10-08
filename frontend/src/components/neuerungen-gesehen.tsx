"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { merkeGelesen } from "@/lib/neuerungen-gelesen";

/**
 * Umschließt den Abschnitt "Neuerungen" im Profil: Sobald er zu mehr als der
 * Hälfte im Bild ist, gilt die laufende Fassung als gelesen und der Punkt am
 * Profil-Reiter verschwindet.
 *
 * Nicht schon beim Öffnen des Profils: Der Abschnitt steht am Ende der Seite,
 * und wer nur sein Passwort ändern wollte, hat die Neuerungen nicht gesehen.
 * Ohne IntersectionObserver (sehr alte Browser) zählt das Öffnen der Seite -
 * lieber ein Punkt zu wenig als einer, der nie verschwindet.
 */
export function NeuerungenGesehen({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const knoten = ref.current;
    if (!knoten) return;
    if (typeof IntersectionObserver === "undefined") {
      merkeGelesen();
      return;
    }
    const beobachter = new IntersectionObserver(
      (eintraege) => {
        if (eintraege.some((e) => e.isIntersecting)) {
          merkeGelesen();
          beobachter.disconnect();
        }
      },
      { threshold: 0.5 },
    );
    beobachter.observe(knoten);
    return () => beobachter.disconnect();
  }, []);

  return <div ref={ref}>{children}</div>;
}

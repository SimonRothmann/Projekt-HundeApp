"use client";

import { useCallback, useEffect, useState, useSyncExternalStore } from "react";
import { Wrench, X } from "lucide-react";
import { api } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import type { Wartungshinweis } from "@/lib/types";
import { merkeWeggeklickt, warWeggeklickt } from "@/lib/weggeklickt";
import {
  WARTUNG_ABRUF_MS,
  WARTUNG_ABRUF_WAEHREND_MS,
  bannerInhalt,
  wartungAbonnieren,
  wartungLaeuftJetzt,
  wegklickSchluessel,
} from "@/lib/wartung";

/**
 * Das Banner selbst: schmal, in der Warnfarbe der übrigen Hinweise. Ohne Daten und
 * ohne Abruf, damit der Adminbereich dieselbe Darstellung als Vorschau zeigt.
 */
export function WartungsBannerAnzeige({ text, onSchliessen }: { text: string; onSchliessen?: () => void }) {
  const t = useT();

  return (
    <div
      role="status"
      className="flex min-w-0 items-start gap-2.5 rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-800 dark:text-amber-300"
    >
      <Wrench className="mt-0.5 size-4 shrink-0" aria-hidden />
      <p className="min-w-0 flex-1 break-words">{text}</p>
      {onSchliessen && (
        // Das Ziel ist 44 px groß, ragt aber über den Rand hinaus, damit das Banner schmal bleibt.
        <button
          type="button"
          onClick={onSchliessen}
          aria-label={t("Hinweis schließen")}
          className="-my-2.5 -mr-2 flex size-11 shrink-0 items-center justify-center rounded-md transition-colors hover:bg-amber-500/15"
        >
          <X className="size-4" aria-hidden />
        </button>
      )}
    </div>
  );
}

/**
 * Der Wartungshinweis für App-Rahmen, Startseite und Anmeldung. Holt ihn beim Laden und
 * alle fünf Minuten (nur bei sichtbarem Tab); ein Fehler beim Abruf heißt "kein Banner".
 *
 * Zwei Quellen: der Hinweis aus der Datenbank (Ankündigung vorher, im Adminbereich
 * gesetzt) und der Zustand "Wartung läuft", den der API-Helfer meldet, wenn der Proxy
 * mitten in der Wartung antwortet. Die Ankündigung lässt sich wegklicken, die laufende
 * Wartung nicht.
 *
 * @param umgebung Klassen für den Abstand um das Banner; sie hängen an einem Rahmen, der
 *                 nur existiert, wenn es etwas anzuzeigen gibt.
 */
export function WartungsBanner({ umgebung }: { umgebung?: string }) {
  const sprache = useSprache();
  const [hinweis, setHinweis] = useState<Wartungshinweis | null>(null);
  // Der Stand des Hinweises, den diese Sitzung weggeklickt hat: gilt auch, wenn der Speicher des Browsers gesperrt ist.
  const [weg, setWeg] = useState<string | null>(null);
  const wartungErkannt = useSyncExternalStore(wartungAbonnieren, wartungLaeuftJetzt, () => false);

  const laden = useCallback(async () => {
    try {
      // 204 (nichts anzuzeigen) kommt als undefined zurück.
      const antwort = await api.get<Wartungshinweis | undefined>("/api/wartungshinweis");
      setHinweis(antwort ?? null);
    } catch {
      // Still: Ein Hinweis, der sich nicht laden lässt, ist kein Grund für eine Fehlermeldung.
      setHinweis(null);
    }
  }, []);

  useEffect(() => {
    // Der erste Abruf kommt aus dem Netz, nicht aus dem Rendern - das Setzen des Zustands
    // geschieht erst in der Antwort.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden]);

  useEffect(() => {
    // Solange die Wartung läuft, öfter nachsehen: Auf Startseite und Anmeldung setzt sonst
    // erst der nächste Abruf den Zustand zurück.
    const intervall = window.setInterval(
      () => {
        if (document.visibilityState === "visible") void laden();
      },
      wartungErkannt ? WARTUNG_ABRUF_WAEHREND_MS : WARTUNG_ABRUF_MS,
    );
    return () => window.clearInterval(intervall);
  }, [laden, wartungErkannt]);

  const inhalt = bannerInhalt(hinweis, wartungErkannt, sprache);
  if (!inhalt) return null;
  const weggeklickt = hinweis !== null && (weg === hinweis.geaendertAm || warWeggeklickt(wegklickSchluessel(hinweis.geaendertAm)));
  if (!inhalt.laeuft && weggeklickt) return null;

  function schliessen() {
    if (!hinweis) return;
    merkeWeggeklickt(wegklickSchluessel(hinweis.geaendertAm));
    setWeg(hinweis.geaendertAm);
  }

  return (
    <div className={umgebung}>
      <WartungsBannerAnzeige text={inhalt.text} onSchliessen={inhalt.laeuft ? undefined : schliessen} />
    </div>
  );
}

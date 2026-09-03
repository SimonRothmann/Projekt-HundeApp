"use client";

import { useEffect, useRef } from "react";

/**
 * Hält den Bildschirm wach, solange `aktiv` gilt.
 *
 * Eine Fährte zu legen dauert Minuten, in denen niemand das Display berührt.
 * Sperrt es sich, drosseln die meisten Geräte die Standortabfrage im
 * Hintergrund oder stellen sie ganz ein - die Aufzeichnung hat dann ein Loch
 * oder endet vorzeitig. Eine verlorene Fährte wiegt schwerer als der
 * Akkuverbrauch, deshalb die Entscheidung für den Wake Lock (siehe
 * docs/FAEHRTE_AUFZEICHNUNG.md).
 *
 * Der Fall, den man dabei leicht übersieht: Das System nimmt die Sperre von
 * sich aus zurück, sobald der Tab in den Hintergrund gerät - etwa weil ein
 * Anruf hereinkommt. Kommt der Nutzer danach zurück, ist der Wake Lock weg
 * und der Bildschirm schläft mitten in der Aufzeichnung ein. Deshalb wird
 * beim Sichtbarwerden erneut angefordert.
 */
export function useWakeLock(aktiv: boolean) {
  // Als Ref, damit der Effect nicht bei jedem Wechsel neu aufgesetzt wird.
  const sperreRef = useRef<WakeLockSentinel | null>(null);

  useEffect(() => {
    if (!aktiv) return;
    if (typeof navigator === "undefined" || !("wakeLock" in navigator)) return;

    let abgebrochen = false;

    async function anfordern() {
      try {
        const sperre = await navigator.wakeLock.request("screen");
        if (abgebrochen) {
          void sperre.release();
          return;
        }
        sperreRef.current = sperre;
      } catch {
        // Kein Grund für eine Fehlermeldung: Der Wake Lock ist Beiwerk. Er
        // scheitert regulär, wenn der Akku fast leer ist oder das Gerät ihn
        // nicht kennt - die Aufzeichnung läuft trotzdem.
      }
    }

    function beiSichtbarkeitswechsel() {
      if (document.visibilityState === "visible") void anfordern();
    }

    void anfordern();
    document.addEventListener("visibilitychange", beiSichtbarkeitswechsel);

    return () => {
      abgebrochen = true;
      document.removeEventListener("visibilitychange", beiSichtbarkeitswechsel);
      void sperreRef.current?.release();
      sperreRef.current = null;
    };
  }, [aktiv]);
}

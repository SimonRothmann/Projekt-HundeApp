"use client";

import { useEffect, useState } from "react";
import { listOwnQueuedRequests, nichtGleichzeitig, syncQueuedRequests } from "@/lib/offline-queue";
import { WifiOff } from "lucide-react";
import { toast } from "sonner";

import { uebersetzeAktuell, useT } from "@/lib/i18n";

const ABGLEICH_ABSTAND_MS = 30_000;

/**
 * Synchronisiert die IndexedDB-Warteschlange (Training/GPS offline erfasst),
 * sobald die Verbindung wieder verfügbar ist, und zeigt den Online/Offline-
 * Status an (siehe PRODUCT_REQUIREMENTS.md "Offline": später synchronisieren).
 *
 * Eingereiht wird auch bei 502/503, wenn der Server bei einem Update kurz
 * neu startet - dann bleibt die Verbindung bestehen und es kommt kein
 * "online"-Ereignis. Darum zusätzlich alle 30 Sekunden und beim Zurückkehren
 * in die App, solange etwas in der Warteschlange liegt.
 */
export function OfflineSyncListener() {
  // Die Meldungen im Effekt gehen über uebersetzeAktuell(): Der Effekt läuft
  // nur einmal und hielte sonst das t() des ersten Renderns - und das spricht
  // noch Deutsch, weil die Sprache erst nach dem Hydrieren feststeht.
  const [isOffline, setIsOffline] = useState(false);
  const t = useT();

  useEffect(() => {
    const trySync = nichtGleichzeitig(async () => {
      const count = await syncQueuedRequests(undefined, (item, error) => {
        // Dauerhaft aussichtsloser Eintrag (z.B. 404 auf ein inzwischen
        // gelöschtes Training) wurde aus der Queue verworfen - dem Nutzer
        // sagen, WAS verloren ging, statt still zu scheitern.
        toast.error(uebersetzeAktuell("„{label}“ konnte nicht synchronisiert werden und wurde verworfen: {fehler}", { label: item.label, fehler: error.message }), {
          duration: 10000,
        });
      });
      if (count > 0) {
        toast.success(
          count === 1
            ? uebersetzeAktuell("1 offline gespeicherte Eintragung synchronisiert.")
            : uebersetzeAktuell("{n} offline gespeicherte Eintragungen synchronisiert.", { n: count }),
        );
      }
    });

    // Der billige Blick in die Warteschlange; nur wenn etwas darin liegt,
    // lohnt der Abgleich (und damit eine Anfrage an den Server).
    async function nachschickenFallsOffen() {
      if (!navigator.onLine) return;
      try {
        if ((await listOwnQueuedRequests()).length > 0) await trySync();
      } catch {
        // Fehler beim Lesen oder Abgleichen: beim nächsten Auslöser erneut.
      }
    }

    function handleVisibility() {
      if (document.visibilityState === "visible") void nachschickenFallsOffen();
    }

    function handleOnline() {
      setIsOffline(false);
      trySync();
    }
    function handleOffline() {
      setIsOffline(true);
    }

    // Initialer Online/Offline-Status beim Mount (externe Quelle: navigator.onLine).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsOffline(!navigator.onLine);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    document.addEventListener("visibilitychange", handleVisibility);
    const intervall = window.setInterval(() => void nachschickenFallsOffen(), ABGLEICH_ABSTAND_MS);
    void trySync();

    return () => {
      window.clearInterval(intervall);
      document.removeEventListener("visibilitychange", handleVisibility);
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  if (!isOffline) return null;

  return (
    <div className="flex items-center justify-center gap-2 bg-amber-500/15 px-4 py-1.5 text-xs font-medium text-amber-700 dark:text-amber-400">
      <WifiOff className="size-3.5" />
{t("Offline – Änderungen werden lokal gespeichert und später synchronisiert.")}
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { syncQueuedRequests } from "@/lib/offline-queue";
import { WifiOff } from "lucide-react";
import { toast } from "sonner";

/**
 * Synchronisiert die IndexedDB-Warteschlange (Training/GPS offline erfasst),
 * sobald die Verbindung wieder verfügbar ist, und zeigt den Online/Offline-
 * Status an (siehe PRODUCT_REQUIREMENTS.md "Offline": "später synchronisieren").
 */
export function OfflineSyncListener() {
  const [isOffline, setIsOffline] = useState(false);

  useEffect(() => {
    async function trySync() {
      const count = await syncQueuedRequests(undefined, (item, error) => {
        // Dauerhaft aussichtsloser Eintrag (z.B. 404 auf ein inzwischen
        // gelöschtes Training) wurde aus der Queue verworfen - dem Nutzer
        // sagen, WAS verloren ging, statt still zu scheitern.
        toast.error(`"${item.label}" konnte nicht synchronisiert werden und wurde verworfen: ${error.message}`, {
          duration: 10000,
        });
      });
      if (count > 0) {
        toast.success(`${count} offline gespeicherte Eintragung${count > 1 ? "en" : ""} synchronisiert.`);
      }
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
    void trySync();

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  if (!isOffline) return null;

  return (
    <div className="flex items-center justify-center gap-2 bg-amber-500/15 px-4 py-1.5 text-xs font-medium text-amber-700 dark:text-amber-400">
      <WifiOff className="size-3.5" />
      Offline – Änderungen werden lokal gespeichert und später synchronisiert.
    </div>
  );
}

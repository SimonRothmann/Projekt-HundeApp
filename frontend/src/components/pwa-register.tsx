"use client";

import { useEffect } from "react";

export function PwaRegister() {
  useEffect(() => {
    // Nur in Production registrieren: in Development liefert der Service
    // Worker sonst veraltete, vom letzten Build gecachte Chunks aus und
    // kollidiert mit Turbopacks Hot-Reload - klassische Ursache für
    // "Seite lädt ewig"/hängende Reloads während der lokalen Entwicklung.
    if (process.env.NODE_ENV === "production" && "serviceWorker" in navigator) {
      navigator.serviceWorker.register("/sw.js").catch(() => {
        // Registrierung optional - App funktioniert auch ohne Service Worker,
        // dann eben ohne Offline-App-Shell.
      });
    }
  }, []);

  return null;
}

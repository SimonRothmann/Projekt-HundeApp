"use client";

import { useEffect } from "react";

export function PwaRegister() {
  useEffect(() => {
    if ("serviceWorker" in navigator) {
      navigator.serviceWorker.register("/sw.js").catch(() => {
        // Registrierung optional - App funktioniert auch ohne Service Worker,
        // dann eben ohne Offline-App-Shell.
      });
    }
  }, []);

  return null;
}

"use client";

import { useEffect } from "react";

export function PwaRegister() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) return;

    if (process.env.NODE_ENV === "production") {
      navigator.serviceWorker.register("/sw.js?env=production").catch(() => {
        // Registrierung optional - App funktioniert auch ohne Service Worker,
        // dann eben ohne Offline-App-Shell.
      });
      return;
    }

    // Im Dev-Modus NICHT registrieren (Versuch, das in einem früheren
    // Commit zu tun, hat einen reproduzierbaren Reload-Loop verursacht: der
    // Service Worker übernimmt per clients.claim() sofort auch bereits
    // offene Tabs und cacht dabei u.a. Next.js' RSC-/Datenabrufe bei der
    // Client-seitigen Navigation - das genügt offenbar, um den Next-Router
    // in eine Reload-Schleife zu schicken, die nur durch Löschen des
    // Cache-Storage/der Site-Daten endete. Echtes Offline-Testen läuft
    // stattdessen über den Produktions-Build, siehe README.md "Offline-
    // Verhalten testen" ("npm run build && npm run start:https").
    //
    // Einen ggf. aus einer früheren Version dieses Projekts noch aktiven
    // Worker zusätzlich aktiv entfernen - das bloße Auslassen von
    // register() entfernt ihn nicht, ein bereits installierter Worker
    // bliebe sonst bestehen und läge weiterhin veraltete, vom letzten
    // Build gecachte Antworten aus.
    navigator.serviceWorker.getRegistrations().then((registrations) => {
      registrations.forEach((registration) => registration.unregister());
    });
    if (window.caches) {
      caches.keys().then((keys) => keys.forEach((key) => caches.delete(key)));
    }
  }, []);

  return null;
}

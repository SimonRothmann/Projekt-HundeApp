"use client";

import { useEffect } from "react";

export function PwaRegister() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) return;

    if (process.env.NODE_ENV === "production") {
      navigator.serviceWorker.register("/sw.js").catch(() => {
        // Registrierung optional - App funktioniert auch ohne Service Worker,
        // dann eben ohne Offline-App-Shell.
      });
      return;
    }

    // Außerhalb von Production nicht nur nicht neu registrieren, sondern
    // einen ggf. aus einer früheren Version dieses Projekts noch aktiven
    // Worker aktiv entfernen. Ein bereits installierter Worker bleibt sonst
    // bestehen (das bloße Auslassen von register() entfernt ihn nicht) und
    // liefert weiterhin veraltete, vom letzten Build gecachte Antworten aus -
    // klassische Ursache für kaputt wirkende Seiten/hängende Reloads in der
    // lokalen Entwicklung, siehe TODO.md.
    navigator.serviceWorker.getRegistrations().then((registrations) => {
      registrations.forEach((registration) => registration.unregister());
    });
    if (window.caches) {
      caches.keys().then((keys) => keys.forEach((key) => caches.delete(key)));
    }
  }, []);

  return null;
}

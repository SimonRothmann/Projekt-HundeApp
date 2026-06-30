"use client";

import { useEffect } from "react";

export function PwaRegister() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) return;

    // Der Service Worker registriert sich jetzt auch im Dev-Modus (vorher
    // nur in Production) - Offline-Verhalten soll sich zwischen Dev und
    // Production nicht grundsätzlich unterscheiden, sonst lässt es sich in
    // der lokalen Entwicklung gar nicht sinnvoll testen. "?env=..." landet
    // laut Spec in self.location der Worker-Instanz und sagt sw.js, ob
    // /_next/-Build-Assets gecacht werden dürfen - im Dev-Modus würde das
    // veraltete Chunks ausliefern und Hot Reload kaputt machen, daher dort
    // bewusst ausgespart (siehe Kommentar in public/sw.js).
    const env = process.env.NODE_ENV === "production" ? "production" : "development";
    navigator.serviceWorker.register(`/sw.js?env=${env}`).catch(() => {
      // Registrierung optional - App funktioniert auch ohne Service Worker,
      // dann eben ohne Offline-App-Shell.
    });
  }, []);

  return null;
}

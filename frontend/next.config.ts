import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Next.js blockt seit v15 standardmäßig Cross-Origin-Zugriffe auf
  // Dev-Server-Assets (JS-Chunks, HMR) - ruft man die App vom Smartphone im
  // selben WLAN über die LAN-IP des Rechners auf, lädt die Seite zwar (das
  // initiale HTML wird nicht geblockt), aber React hydratisiert nie, weil
  // die JS-Chunks blockiert werden. Sichtbares Symptom: Formulare lösen
  // beim Absenden einen normalen Browser-Reload aus statt den
  // JS-Submit-Handler aufzurufen, Buttons reagieren nicht auf Klicks.
  // Wildcard deckt das gesamte Heimnetz-Subnetz ab, falls der Router per
  // DHCP eine andere Adresse im letzten Octet vergibt.
  allowedDevOrigins: ["192.168.1.*"],
};

export default nextConfig;

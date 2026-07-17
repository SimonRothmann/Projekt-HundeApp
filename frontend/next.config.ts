import type { NextConfig } from "next";

// Content-Security-Policy, vorerst im REPORT-ONLY-Modus (siehe TODO.md,
// Roadmap 4): der Browser blockiert noch nichts, meldet Verstöße aber an
// /csp-report (Next-Route-Handler, loggt in die Frontend-Container-Logs).
// Nach der Beobachtungswoche wird der Header-Name auf
// "Content-Security-Policy" umgestellt und die Policy damit scharf.
//
// Bewusste Entscheidungen:
// - script-src 'unsafe-inline': der Next.js App Router injiziert
//   Inline-Bootstrap-Skripte für die Hydration; eine Nonce-Infrastruktur
//   (Middleware + überall dynamisches Rendering) wäre dafür nötig und steht
//   in keinem Verhältnis. Externe Skript-Quellen bleiben trotzdem blockiert.
// - style-src 'unsafe-inline': Leaflet setzt style-Attribute auf Marker-/
//   Karten-Elemente.
// - img-src https:: Avatar-URLs sind bewusst freie https-Links (Profil ohne
//   Datei-Upload); deckt zugleich die OSM-Kartenkacheln ab. Bilder können
//   kein Skript ausführen, das Risiko ist begrenzt.
// - connect-src statisch mit BEIDEN API-Domains: NEXT_PUBLIC_API_URL ist
//   ein Docker-Build-ARG der Build-Stage und steht zur Laufzeit von
//   `next start` (liest diese Config beim Serverstart) nicht zur Verfügung.
const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob: https:",
  "font-src 'self' data:",
  "connect-src 'self' https://api.dogity.net https://api-test.dogity.net",
  "worker-src 'self'",
  "manifest-src 'self'",
  "frame-ancestors 'none'",
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "report-uri /csp-report",
].join("; ");

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

  async headers() {
    // Nur im Production-Build: der Dev-Server braucht 'unsafe-eval' und
    // WebSockets für HMR - eine Dev-Policy erzeugte nur Rausch-Reports.
    if (process.env.NODE_ENV !== "production") return [];
    return [
      {
        source: "/:path*",
        headers: [
          {
            key: "Content-Security-Policy-Report-Only",
            value: contentSecurityPolicy,
          },
        ],
      },
    ];
  },
};

export default nextConfig;

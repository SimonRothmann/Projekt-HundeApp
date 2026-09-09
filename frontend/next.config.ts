import type { NextConfig } from "next";

// Content-Security-Policy, seit 2026-09-09 DURCHSETZEND (vorher Report-Only).
//
// Die ursprünglich geplante "Beobachtungswoche" konnte nie zustande kommen:
// die Reports gehen nach stdout in die Frontend-Container-Logs, und jeder
// Deploy erzeugt den Container neu und wirft sie damit weg. Nach Wochen im
// Report-Only-Modus lagen null auswertbare Meldungen vor - nicht weil nichts
// verletzt wurde, sondern weil nichts überdauert hat.
//
// Statt weiter zu warten wurde der Nachweis aktiv geführt: Produktionsbau mit
// durchsetzendem Header, dann 17 Routen durchgefahren - alle öffentlichen
// Seiten, vollständiger Anmeldevorgang, Dashboard, Hundeseite mit vier
// Leaflet-Karten und geladenen Kacheln, Druckansicht, Trainerbereich. Null
// Verletzungen am /csp-report-Endpunkt, null in der Browserkonsole.
//
// report-uri bleibt bewusst bestehen: jetzt, wo blockiert statt nur gemeldet
// wird, ist es die einzige Spur, falls doch etwas hängenbleibt. Nach einem
// Deploy lohnt ein Blick (siehe docs/BETRIEB.md).
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
  // https: deckt weiterhin alle Kachelquellen ab (OSM, Esri-Luftbild) und
  // die frei wählbaren Avatar-Adressen. Bilder können kein Skript ausführen.
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
            key: "Content-Security-Policy",
            value: contentSecurityPolicy,
          },
        ],
      },
    ];
  },
};

export default nextConfig;

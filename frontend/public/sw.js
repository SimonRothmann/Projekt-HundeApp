const CACHE_NAME = "dogity-shell-v3";

// pwa-register.tsx registriert diese Datei mit "?env=development" bzw.
// "?env=production" im Pfad - der Query-String der Registrierungs-URL wird
// laut Spec Teil von self.location, eine separate Build-Variante dieser
// (unverarbeitet aus public/ ausgelieferten) Datei ist dafür nicht nötig.
// Grund für die Unterscheidung: Next.js' Dev-Modus serviert /_next/static/
// nicht inhaltsbasiert-stabil wie ein Produktions-Build, sondern über den
// Hot-Reload-Mechanismus mit sich änderndem Inhalt unter denselben Pfaden -
// cache-first wie im Production-Zweig würde dort veraltete Chunks ausliefern
// und Hot Reload kaputt machen. Damit sich Dev und Production trotzdem
// möglichst gleich verhalten (siehe Nutzer-Feedback "sonst macht Dev keinen
// Sinn"), bleibt im Dev-Modus nur dieser eine Unterschied; die eigentlich
// relevante Offline-Fähigkeit (Seite/Navigation übersteht Verbindungsverlust,
// Fallback auf /offline) ist in beiden Modi identisch aktiv.
const IS_DEV = new URLSearchParams(self.location.search).get("env") === "development";

self.addEventListener("install", () => {
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

// Next.js Build-Assets unter /_next/static/ sind inhaltsbasiert gehasht (der
// Dateiname ändert sich, wenn sich der Inhalt ändert) - dafür ist cache-first
// sicher und schnell, da eine alte URL niemals neuen Inhalt bekommt.
function isImmutableBuildAsset(url) {
  return url.pathname.startsWith("/_next/static/");
}

// API-Aufrufe an das Backend werden bewusst nicht gecacht - Schreibvorgänge
// laufen offline über die IndexedDB-Warteschlange (siehe lib/offline-queue.ts),
// nicht über den Service Worker.
self.addEventListener("fetch", (event) => {
  const { request } = event;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  // Im Dev-Modus alles unter /_next/ unangetastet lassen, nicht nur die
  // statischen Chunks (isImmutableBuildAsset unten) - darunter liegt u.a.
  // Turbopacks Hot-Reload-Endpoint (lang offene, gestreamte Verbindung).
  // Würde der Service Worker den abfangen (cache.put() liest den Body bis
  // zum Ende), bliebe das für eine absichtlich nie endende Stream-Antwort
  // hängen bzw. störte die HMR-Verbindung.
  if (IS_DEV && url.pathname.startsWith("/_next/") && !isImmutableBuildAsset(url)) return;

  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request)
        .then((response) => {
          const copy = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
          return response;
        })
        .catch(() =>
          caches
            .match(request)
            .then((cached) => cached || caches.match("/offline"))
            // Weder Seite noch /offline gecacht (z.B. allererster Aufruf ohne
            // Internet) - respondWith() darf nie mit undefined aufgelöst
            // werden, sonst "Failed to convert value to 'Response'".
            .then((response) => response || new Response("Offline", { status: 503, statusText: "Offline" })),
        ),
    );
    return;
  }

  if (isImmutableBuildAsset(url)) {
    // Im Dev-Modus unangetastet lassen (siehe IS_DEV-Kommentar oben) - kein
    // respondWith() heißt, der Browser behandelt den Request normal, ohne
    // Service-Worker-Eingriff.
    if (IS_DEV) return;

    event.respondWith(
      caches.match(request).then((cached) => {
        if (cached) return cached;
        return fetch(request).then((response) => {
          const copy = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
          return response;
        });
      }),
    );
    return;
  }

  // Alles andere (Icons, Manifest, Seiten-Daten/RSC-Payloads) bewusst
  // network-first statt cache-first: diese Dateien sind NICHT inhaltsbasiert
  // benannt, eine veraltete gecachte Antwort würde sonst auf unbestimmte Zeit
  // ausgeliefert, sobald sie einmal im Cache liegt (auch nach einem neuen
  // Deployment, solange CACHE_NAME unverändert bleibt) - das war die Ursache
  // für "App zeigt veraltete Inhalte, nur Browserdaten löschen hilft".
  // Cache dient hier nur als Offline-Fallback, nicht als primäre Quelle.
  event.respondWith(
    fetch(request)
      .then((response) => {
        const copy = response.clone();
        caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
        return response;
      })
      .catch(() => caches.match(request).then((cached) => cached || new Response("", { status: 503, statusText: "Offline" }))),
  );
});

const CACHE_NAME = "dogity-shell-v4";

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
// nicht über den Service Worker. Registriert wird diese Datei laut
// pwa-register.tsx nur bei NODE_ENV=production - ein Versuch, sie auch im
// Dev-Modus zu registrieren, hat einen reproduzierbaren Reload-Loop
// verursacht (clients.claim() übernimmt sofort offene Tabs und cacht dabei
// auch Next.js' RSC-/Datenabrufe bei der Client-Navigation, was den
// Next-Router aus dem Tritt brachte) - siehe TODO.md.
self.addEventListener("fetch", (event) => {
  const { request } = event;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

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

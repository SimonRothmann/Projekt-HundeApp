const CACHE_NAME = "canistrack-shell-v1";

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

// API-Aufrufe an das Backend werden bewusst nicht gecacht - Schreibvorgänge
// laufen offline über die IndexedDB-Warteschlange (siehe lib/offline-queue.ts),
// nicht über den Service Worker.
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

  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) return cached;
      return fetch(request)
        .then((response) => {
          const copy = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
          return response;
        })
        .catch(() => cached || new Response("", { status: 503, statusText: "Offline" }));
    }),
  );
});

const CACHE_NAME = "dogity-shell-v5";

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

// Die Dateien der Karte (public/karte/) ändern sich nie unter demselben Pfad:
// - /karte/maplibre-<Version>/ trägt die Version im Pfad (Worker der
//   Kartenbibliothek, siehe scripts/maplibre-worker.mjs),
// - Schriften und Symbole sind ausgelieferte Zeichensätze und Bildbögen.
// Draußen bei schlechtem Netz sollen sie nicht bei jedem Öffnen der Karte übers
// Netz gehen. Wer Schriften oder Symbole austauscht, erhöht CACHE_NAME - sonst
// bleibt die alte Fassung im Cache. Die Stil-JSONs (/karte/stil-*.json) gehören
// bewusst NICHT dazu, sie laufen wie alles Übrige network-first. Kacheln kommen
// von fremden Ursprüngen und werden unten ohnehin nicht angefasst.
function isImmutableKartenDatei(url) {
  return (
    url.pathname.startsWith("/karte/maplibre-") ||
    url.pathname.startsWith("/karte/schriften/") ||
    url.pathname.startsWith("/karte/symbole/")
  );
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
          // Nur erfolgreiche Antworten cachen - sonst würde z.B. eine 404/5xx
          // als Offline-Fallback konserviert.
          if (response.ok) {
            const copy = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
          }
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

  if (isImmutableBuildAsset(url) || isImmutableKartenDatei(url)) {
    event.respondWith(
      caches.match(request).then((cached) => {
        if (cached) return cached;
        return fetch(request).then((response) => {
          // Einen 404 für einen nicht mehr existierenden (alten) Chunk NICHT
          // cachen - sonst bliebe der Fehlzustand für diese URL bestehen.
          if (response.ok) {
            const copy = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
          }
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
        if (response.ok) {
          const copy = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
        }
        return response;
      })
      .catch(() => caches.match(request).then((cached) => cached || new Response("", { status: 503, statusText: "Offline" }))),
  );
});

// ---------------------------------------------------------------------------
// Push-Benachrichtigungen (Web Push, siehe lib/push.ts und
// Dogity.Application/Notifications/PushPayload.cs).
//
// Der Server schickt ein JSON im Format des "Declarative Web Push" (Safari
// 18.4+):
//   { "web_push": 8030,
//     "notification": { "title", "body", "navigate", "lang", "app_badge" } }
// Safari 18.4+ zeigt so eine Nachricht selbst an und öffnet beim Tippen die
// Adresse in "navigate" - ohne dass dieser Worker etwas tun muss. Alle anderen
// Browser (und ältere Safari-Fassungen) reichen dasselbe JSON an diesen
// Handler weiter, der die Benachrichtigung dann selbst baut. Ein Format für alle.
self.addEventListener("push", (event) => {
  // Der Browser hat die Nachricht bereits als Benachrichtigung vorbereitet
  // (Declarative Web Push): nicht ein zweites Mal anzeigen. Nur die Zahl am
  // App-Symbol setzen, falls mitgeschickt.
  const vorbereitet = Boolean(event.notification);

  let daten = null;
  try {
    daten = event.data ? event.data.json() : null;
  } catch {
    daten = null;
  }
  const inhalt = daten && typeof daten === "object" ? daten.notification : null;

  const aufgaben = [];

  if (inhalt && typeof inhalt.app_badge === "number" && self.navigator && "setAppBadge" in self.navigator) {
    // Fehlschlag (z. B. nicht unterstützt) ist kein Fehler.
    aufgaben.push(self.navigator.setAppBadge(inhalt.app_badge).catch(() => {}));
  }

  if (!vorbereitet) {
    // userVisibleOnly: Eine Push-Nachricht MUSS eine sichtbare Benachrichtigung
    // zur Folge haben, sonst zeigt der Browser eine eigene ("Die Seite wurde im
    // Hintergrund aktualisiert") oder entzieht die Erlaubnis. Deshalb auch bei
    // unlesbarem Inhalt eine allgemeine Meldung statt gar keiner.
    const titel = (inhalt && inhalt.title) || "Dogity";
    const text = (inhalt && inhalt.body) || "Du hast eine neue Benachrichtigung.";
    const ziel = inhalt && typeof inhalt.navigate === "string" ? inhalt.navigate : "/dashboard";
    aufgaben.push(
      self.registration.showNotification(titel, {
        body: text,
        lang: (inhalt && inhalt.lang) || "de",
        // Das App-Icon ist eine SVG; Plattformen, die nur PNG anzeigen, lassen
        // das Symbol weg - die Benachrichtigung selbst erscheint trotzdem.
        icon: "/icon.svg",
        badge: "/icon.svg",
        data: { url: ziel },
      }),
    );
  }

  event.waitUntil(Promise.all(aufgaben));
});

// Tippen auf eine selbst gebaute Benachrichtigung (bei Declarative Web Push
// öffnet der Browser die Adresse selbst): ein offenes Dogity-Fenster nach vorn
// holen und zur Adresse schicken, sonst ein neues öffnen.
self.addEventListener("notificationclick", (event) => {
  event.notification.close();

  // Nur Adressen dieser App. Der Server schickt ohnehin nur eigene Links; die
  // Prüfung schützt davor, dass ein manipulierter Inhalt irgendwohin führt.
  let ziel = "/dashboard";
  try {
    const gewuenscht = new URL((event.notification.data && event.notification.data.url) || "/dashboard", self.location.origin);
    if (gewuenscht.origin === self.location.origin) ziel = gewuenscht.pathname + gewuenscht.search + gewuenscht.hash;
  } catch {
    // Unlesbare Adresse - es bleibt beim Dashboard.
  }

  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((fenster) => {
      for (const f of fenster) {
        if ("focus" in f) {
          return (
            f
              .focus()
              .then((fokussiert) => ("navigate" in fokussiert ? fokussiert.navigate(ziel) : fokussiert))
              // navigate() lehnt bei nicht kontrollierten Fenstern ab - dann lieber
              // ein neues Fenster als eine Mitteilung, auf die nichts folgt.
              .catch(() => self.clients.openWindow(ziel))
          );
        }
      }
      return self.clients.openWindow(ziel);
    }),
  );
});

// Build-Kennung (Commit-Hash) aus der Skript-Adresse, siehe pwa-register.tsx.
// Jede Veröffentlichung bekommt dadurch ihren eigenen Cache (BUILD_CACHE) und
// räumt beim Aktivieren die alten weg - früher blieben die Build-Dateien,
// Seiten und RSC-Antworten jeder Fassung für immer auf dem Gerät.
const BUILD = new URL(self.location.href).searchParams.get("build") || "lokal";

// Dauerhafter Cache: nur die unveränderlichen Kartendateien (siehe
// isImmutableKartenDatei) und die Verwaltungsliste der Builds. Wer Schriften
// oder Symbole austauscht, erhöht diese Nummer - dann verschwindet der alte
// Cache beim Aktivieren.
const SHELL_CACHE = "dogity-shell-v6";
// Alles Übrige (Seiten, /_next/static, RSC-Antworten, Icons, Manifest) liegt
// pro Build getrennt.
const BUILD_CACHE = "dogity-build-" + BUILD;

// Schlüssel der Verwaltungsliste (JSON-Array der Build-Kennungen, älteste
// zuerst) im SHELL_CACHE. Bewusst eine eigene Liste: Die Reihenfolge von
// caches.keys() ist nicht zugesichert.
const BUILDS_LISTE = "/__dogity-builds";

// Aktueller und vorheriger Build bleiben: Ein noch offener Tab der alten
// Fassung lädt seine Chunks nach, und offline gespeicherte Seiten der vorigen
// Fassung finden ihre Chunks im selben Cache.
const BEHALTENE_BUILDS = 2;

// So lange wartet eine Navigation auf das Netz, bevor die gespeicherte Seite
// gezeigt wird (schwacher Empfang draußen auf dem Hundeplatz).
const NAVIGATION_ZEITLIMIT_MS = 4000;

function leseBuilds() {
  return caches
    .open(SHELL_CACHE)
    .then((cache) => cache.match(BUILDS_LISTE))
    .then((antwort) => (antwort ? antwort.json() : []))
    .then((liste) => (Array.isArray(liste) ? liste : []))
    .catch(() => []);
}

self.addEventListener("install", (event) => {
  // /offline gehört in jeden Build-Cache, sonst läge der Rückfall nur dort, wo
  // die Seite je besucht wurde. Ein Fehlschlag (z. B. kein Netz) darf die
  // Installation nicht scheitern lassen.
  // Dazu die Dateien, die die Seite lädt: Sonst zeigt sie offline nur den rohen
  // Text, und ihre Skripte scheitern (ChunkLoadError).
  event.waitUntil(
    caches
      .open(BUILD_CACHE)
      .then((cache) =>
        cache
          .add("/offline")
          .then(() => cache.match("/offline"))
          .then((antwort) => antwort.text())
          .then((html) => {
            const dateien = [...new Set(html.match(/\/_next\/static\/[^"'\\\s)]+/g) || [])];
            return Promise.all(dateien.map((datei) => cache.add(datei).catch(() => {})));
          }),
      )
      .catch(() => {}),
  );
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    leseBuilds()
      .then((liste) => {
        const behalten = liste.filter((b) => b !== BUILD).concat(BUILD).slice(-BEHALTENE_BUILDS);
        return caches
          .open(SHELL_CACHE)
          .then((cache) =>
            cache.put(BUILDS_LISTE, new Response(JSON.stringify(behalten), { headers: { "Content-Type": "application/json" } })),
          )
          .then(() => caches.keys())
          .then((keys) => {
            const erlaubt = new Set([SHELL_CACHE, ...behalten.map((b) => "dogity-build-" + b)]);
            return Promise.all(keys.filter((key) => !erlaubt.has(key)).map((key) => caches.delete(key)));
          });
      })
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
// Netz gehen. Wer Schriften oder Symbole austauscht, erhöht SHELL_CACHE - sonst
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
    // Netz zuerst, aber nur mit Zeitlimit und nur, wenn es eine gespeicherte
    // Fassung der Seite gibt. Der Abruf läuft auch nach dem Zeitlimit weiter
    // und aktualisiert den Cache - waitUntil muss dafür JETZT, synchron im
    // Handler, gesetzt werden (nach dem Auflösen von respondWith würde es werfen).
    const netz = fetch(request);
    event.waitUntil(
      netz
        .then((response) => {
          // Nur erfolgreiche Antworten cachen - sonst würde z.B. eine 404/5xx
          // als Offline-Fallback konserviert.
          if (!response.ok) return undefined;
          const copy = response.clone();
          return caches.open(BUILD_CACHE).then((cache) => cache.put(request, copy));
        })
        .catch(() => {}),
    );

    event.respondWith(
      new Promise((resolve) => {
        let aufgeloest = false;
        let timer = null;
        const antworte = (response) => {
          if (aufgeloest) return;
          aufgeloest = true;
          clearTimeout(timer);
          resolve(response);
        };

        // Nach dem Zeitlimit die gespeicherte Seite zeigen; gibt es keine,
        // weiter aufs Netz warten (kein 503 wegen des Zeitlimits).
        timer = setTimeout(() => {
          caches
            .match(request)
            .then((gespeichert) => {
              if (gespeichert) antworte(gespeichert);
            })
            .catch(() => {});
        }, NAVIGATION_ZEITLIMIT_MS);

        netz.then(antworte, () =>
          caches
            .match(request)
            .then((cached) => cached || caches.match("/offline"))
            // Weder Seite noch /offline gecacht (z.B. allererster Aufruf ohne
            // Internet) - respondWith() darf nie mit undefined aufgelöst
            // werden, sonst "Failed to convert value to 'Response'".
            .then((response) => response || new Response("Offline", { status: 503, statusText: "Offline" }))
            .catch(() => new Response("Offline", { status: 503, statusText: "Offline" }))
            .then(antworte),
        );
      }),
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
            const ziel = isImmutableKartenDatei(url) ? SHELL_CACHE : BUILD_CACHE;
            caches.open(ziel).then((cache) => cache.put(request, copy));
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
  // Deployment, solange der Build-Cache nicht ersetzt ist) - das war die Ursache
  // für "App zeigt veraltete Inhalte, nur Browserdaten löschen hilft".
  // Cache dient hier nur als Offline-Fallback, nicht als primäre Quelle. Auch
  // bewusst ohne Zeitlimit: RSC-Antworten einer anderen Fassung würden den
  // Next-Router verwirren.
  event.respondWith(
    fetch(request)
      .then((response) => {
        if (response.ok) {
          const copy = response.clone();
          caches.open(BUILD_CACHE).then((cache) => cache.put(request, copy));
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

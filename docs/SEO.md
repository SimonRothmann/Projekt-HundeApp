# Suchmaschinen

## Ausgangslage

Die Startseite war eine reine Weiterleitung: ein Client-Component, das `null`
rendert und dann auf `/login` oder `/dashboard` schickt. Für einen Crawler
gemessen:

```
https://dogity.net  ->  0 Zeichen sichtbarer Text
```

Damit gab es außer dem Markennamen im `<title>` und einer 46 Zeichen langen
Beschreibung **nichts**, worauf Google ranken konnte. Genau das war das
beobachtete Verhalten: gefunden nur, wer „Dogity" bereits kannte.

Ebenfalls gefehlt: `sitemap.xml` (404), eine eigene `robots.txt` (es kam nur
Cloudflares vorgeschaltete Fassung ohne `Sitemap:`-Zeile), Open-Graph-Angaben,
strukturierte Daten.

## Was dagegen unternommen wurde

### 1. Startseite mit echtem Inhalt

`/` rendert jetzt serverseitig eine vollwertige Seite (Funktionen, Zielgruppen,
FAQ) — **3570 Zeichen** statt 0, mit einer `h1`, vier `h2` und elf `h3`.

Die Weiterleitung für angemeldete Besucher bleibt, wandert aber in ein eigenes
Client-Component (`AuthedRedirect`). Suchmaschinen führen kein JavaScript-Redirect
aus und sehen deshalb den Inhalt; angemeldete Nutzer landen weiterhin im
Dashboard.

Alle Aussagen auf der Seite sind durch tatsächlich vorhandene Funktionen
gedeckt. Eine Startseite, die mehr verspricht als die App kann, bringt Besucher,
die sofort wieder abspringen — und genau das wertet Google ab.

### 2. Prüfungsordnungen öffentlich (der eigentliche Hebel)

Das Backend gibt den Sportarten- und Prüfungsordnungskatalog bereits **ohne
Login** heraus (`SportsController`, `[AllowAnonymous]`) — die Daten enthalten
nichts Personenbezogenes. Daraus entstehen **31 öffentliche Seiten** unter
`/pruefungsordnungen/<name>` mit Übungen, Punkten und Anforderungen,
durchschnittlich 1759 Zeichen Text.

Das ist der Long-Tail: Nach „IGP 1 Prüfungsordnung", „Begleithundeprüfung
Ablauf" oder „IBGH 2 Punkte" wird gesucht — nach „Dogity" nur von denen, die
es schon kennen. Der Inhalt ist zugleich für sich genommen nützlich, nicht bloß
Suchmaschinenfutter.

Die Seiten werden beim Bauen vorgerendert (`generateStaticParams`) und einmal
täglich neu geholt. Ist das Backend beim Bauen nicht erreichbar, entsteht keine
kaputte Seite: der Katalog kommt leer zurück und die Seiten werden beim ersten
Aufruf erzeugt.

**Vorbehalt:** Die Beschreibungen stammen aus dem Seed und waren in Teilen
fehlerhaft (vom Betreiber teilweise von Hand korrigiert). Öffentlich gestellter
Text vervielfacht solche Fehler. Jede Seite trägt deshalb den Hinweis, dass sie
eine Zusammenfassung ohne Gewähr ist und allein die Ordnung des VDH bzw. der FCI
verbindlich ist. Vor breiterer Bewerbung sollten die Texte durchgesehen werden.

### 3. Technisches Fundament

- `metadataBase`, Titel-Vorlage (`%s | Dogity`), Beschreibung, Keywords,
  Open Graph, Twitter Card, `canonical` je Seite
- `app/robots.ts` — erlaubt die öffentlichen Seiten, sperrt den eingeloggten
  Bereich und die Anmeldeformulare, verweist auf die Sitemap
- `app/sitemap.ts` — 33 Adressen, die Prüfungsordnungen aus dem Backend statt
  aus einer gepflegten Liste
- `app/opengraph-image.tsx` — 1200×630-Vorschaubild fürs Teilen in
  Gruppenchats, gezeichnet statt als Bilddatei
- JSON-LD: `SoftwareApplication` + `FAQPage` + `WebSite` auf der Startseite,
  `CollectionPage` auf der Übersicht, `Article` je Prüfungsordnung

### 4. Eingeloggten Bereich aus dem Index halten

Die App-Seiten liefern ohne Anmeldung nur „Lädt…". Kämen sie in den Index,
stünden dort lauter inhaltsleere Treffer — das wertet die gesamte Domain ab.
`(app)/layout.tsx` ist deshalb jetzt eine Server-Hülle, die `noindex, nofollow`
setzt; die bisherige Client-Hülle liegt unverändert in `app-shell.tsx`.
`robots.txt` verhindert das Abrufen, das Meta-Tag das Aufnehmen bereits
bekannter Adressen.

## Was Suchmaschinenarbeit NICHT ersetzt

Sichtbarkeit entsteht auch aus Verweisen von außen. Wirksam wären: Eintrag in
Vereins-Linklisten, ein Beitrag in einschlägigen Foren und Gruppen, und die
Anmeldung der Domain in der Google Search Console samt Einreichen der Sitemap.
Das ist nichts, was sich im Code erledigen lässt.

# CanisTrack – Entwicklungs-TODO

Wird laufend von Claude gepflegt, damit jederzeit ein Überblick über den aktuellen Stand besteht. Spiegelt die interne Task-Liste.

## Erledigt

- [x] Git-Repo initialisieren
- [x] Doku-Dateien optimieren (TECH_STACK.md, DEPLOYMENT.md, README.md – Hosting ohne CI/CD-Pflicht)
- [x] Backend-Skeleton: ASP.NET Core 9 Clean Architecture
- [x] Core-Domainmodell + EF Core Migration
- [x] Frontend-Skeleton: Next.js + TypeScript + Tailwind + shadcn
- [x] Lokale Dev-Umgebung ohne Docker
- [x] Trainingstagebuch: Application-Service, API-Controller, Frontend-UI
- [x] Zielplanung: Domain, Migration, Application-Service + Plan-Generator, API-Controller, Frontend-UI
- [x] Community-Domain: Club/Group/Membership (Grundmodell)
- [x] Sportkatalog erweitert: Bewertungskriterien je Übung + drei Fährten-Prüfungsstufen (A/B/C)
- [x] GPS/Fährte-Modul: Domain + API + Frontend (Browser-Geolocation, Leaflet/OSM-Karte)
- [x] Trainer-Übersicht: Gruppen anlegen/verwalten, Mitglieder einladen, individuelle Pläne zuweisen
- [x] Admin-Übersicht (Plattform-Scope): Kennzahlen, Nutzerliste, Prüfungsordnungs-Metadaten pflegen
- [x] PWA/Offline-Unterstützung: Manifest, Service Worker, IndexedDB-Sync-Queue für Training/GPS
- [x] Vereinsverwaltung (Backend + Frontend): Admin legt Vereine an und weist Trainern die Vereins-Rolle zu (ClubTrainer); Trainer können Gruppen optional einem zugewiesenen Verein zuordnen
- [x] Übungs-Verwaltung (Backend + Frontend): Admin pflegt globale Übungen, Vereinstrainer ausschließlich vereinsspezifische Übungen ihres Vereins (Exercise.ClubId + Sichtbarkeitsfilter, per curl/End-to-End verifiziert)
- [x] Prüfungsordnung-Import (Backend + Frontend): Scan der lokalen PDF via `pdftotext`, Admin wählt einzelne Vorschläge gezielt aus und übernimmt sie in den Katalog
- [x] Trainer-Feedback zu Trainingseinheiten (Backend + Frontend): nur ein per TrainerAssignment zugewiesener Trainer kann Feedback zu einem Training eines betreuten Hundes geben, der Besitzer sieht es nur lesend (schließt die letzte offene Lücke aus ROADMAP.md Phase 2 "Trainer Plattform")

## Sicherheitshinweis (erledigt, dokumentiert)

Beim Versuch, eine PDF-Textextraktion per NuGet-Paket (`UglyToad.PdfPig`) einzubinden, fiel eine verdächtige Versionshistorie auf (Sprung von `0.1.9-alpha` auf `1.7.0-custom-5`, dünne Metadaten - typisches Muster für ein gekapertes Paket). Paket sofort entfernt, NuGet-Cache bereinigt, nichts davon wurde je gebaut/ausgeführt. Stattdessen wird das etablierte externe Tool `pdftotext` (poppler-utils) aufgerufen - siehe DEPLOYMENT.md.

Zusätzlich: Ein in README.md im Klartext committetes lokales Dev-DB-Passwort wurde gefunden (war bereits zu GitHub gepusht), aus der Doku entfernt und das Passwort rotiert.

## Aktuell keine offenen Punkte

Alle bisher beauftragten Features sind umgesetzt und verifiziert (Build, Lint, Typecheck, End-to-End-Tests per curl). Nächste sinnvolle Kandidaten laut ROADMAP.md Phase 3 "Vereinsplattform": Termine/Veranstaltungen, Prüfungsanmeldung. Folgen auf Zuruf.

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
- [x] Fährtenaufzeichnung im Frontend prominenter gemacht: direkter "Fährte aufnehmen"-Button auf der Hundedetailseite (legt Training + GPS-Track in einem Schritt an, auch offline), statt vorher zwingend ein vollständiges bewertetes Training anlegen zu müssen
- [x] Testaccounts + Demo-Daten für alle Rollen (`DemoDataSeeder`, läuft automatisch in Development): `admin@canistrack.test`, `trainer@canistrack.test`, `mitglied1@canistrack.test`, `mitglied2@canistrack.test`, Passwort jeweils `Demo1234!`
- [x] Prüfungsordnung-Import gegen die echte PDF ausgeführt und validiert (Scan + Übernahme einzelner Kandidaten end-to-end per curl getestet)
- [x] IGP1-3 (FCI-Internationale Gebrauchshundeprüfung) als neue Sportarten im Katalog angelegt, mit Übungsnamen und Punktzahlen direkt aus der UTI-REG-IGP-de-2025-PDF (Ausnahme von der sonst geltenden "keine Originaltexte"-Regel, ausdrücklich genehmigt durch den Auftraggeber in seiner Funktion als VDH-Vorstand)

## Offener Punkt: IGP1-3 Punktaufteilung prüfen

Die Übungsnamen in IGP1-3 sind aus der PDF korrekt übernommen. Die genaue Punkteaufteilung innerhalb Abteilung B (Unterordnung) und Abteilung C (Schutzdienst) ließ sich aus der mehrspaltigen Tabellenlayout der PDF nicht immer zweifelsfrei pro Prüfungsstufe extrahieren - die aktuell hinterlegten Werte sind plausible Näherungen (mit `"Näherungswert"` markiert in den ScoringNotes) und sollten von dir als VDH-Vorstand vor einem Produktiveinsatz gegen die offizielle PO geprüft/korrigiert werden. Betroffen: `backend/src/CanisTrack.Infrastructure/Persistence/Seed/SportCatalogSeeder.cs`.

## Sicherheitshinweis (erledigt, dokumentiert)

Beim Versuch, eine PDF-Textextraktion per NuGet-Paket (`UglyToad.PdfPig`) einzubinden, fiel eine verdächtige Versionshistorie auf (Sprung von `0.1.9-alpha` auf `1.7.0-custom-5`, dünne Metadaten - typisches Muster für ein gekapertes Paket). Paket sofort entfernt, NuGet-Cache bereinigt, nichts davon wurde je gebaut/ausgeführt. Stattdessen wird das etablierte externe Tool `pdftotext` (poppler-utils) aufgerufen - siehe DEPLOYMENT.md.

Zusätzlich: Ein in README.md im Klartext committetes lokales Dev-DB-Passwort wurde gefunden (war bereits zu GitHub gepusht), aus der Doku entfernt und das Passwort rotiert.

## Aktuell keine offenen Punkte (außer IGP-Punkteprüfung oben)

Alle bisher beauftragten Features sind umgesetzt und verifiziert (Build, Lint, Typecheck, End-to-End-Tests per curl). Nächste sinnvolle Kandidaten laut ROADMAP.md Phase 3 "Vereinsplattform": Termine/Veranstaltungen, Prüfungsanmeldung. Folgen auf Zuruf.

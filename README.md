# CanisTrack
## Die digitale Plattform für modernen Hundesport

Version: 0.1.0

---

# Vision

CanisTrack ist eine moderne, mobile-first Plattform für Hundesportler,
Trainer, Vereine und Prüfungsorganisationen im deutschsprachigen Raum.

Die Plattform verbindet:

- Trainingstagebuch
- Prüfungsordnungen
- Trainingsplanung
- Vereinsverwaltung
- Trainerkommunikation
- Gruppenorganisation
- Fortschrittsanalyse
- GPS-basierte Fährtenverwaltung

zu einer zentralen Anwendung.

Das Ziel ist eine digitale Infrastruktur für den Hundesport,
vergleichbar mit Strava für Ausdauersport oder Garmin Connect für Training.

---

# Zielgruppe

## Primär

Hundesportler:

- Gebrauchshundesport
- Begleithundeprüfung
- IBGH
- IGP
- Fährte
- Unterordnung
- weitere VDH/SWHV Sparten

## Sekundär

Trainer:

- Trainingsgruppen
- Mitgliederverwaltung
- Trainingsplanung
- Feedback

## Tertiär

Vereine:

- Mitglieder
- Termine
- Prüfungen
- Organisation

---

# Grundprinzipien

## Mobile First

Die Anwendung wird hauptsächlich auf dem Hundeplatz verwendet.

Daher:

- schnelle Bedienung
- große Touchflächen
- Offlinefähigkeit
- minimale Dateneingabe

---

## Modularität

Jede Hundesparte ist ein eigenes Modul.

Beispiele:

- BH
- IBGH
- IGP
- Fährte
- Agility
- Obedience

Neue Module dürfen keine Änderungen am Kernsystem benötigen.

---

## Datengetriebene Architektur

Prüfungsordnungen, Übungen und Trainingspläne sind Daten.

Keine hartcodierten Abläufe.

---

## Community First

Der Hundesport lebt von:

- Vereinen
- Trainern
- Gruppen
- gemeinsamer Entwicklung

Die Plattform unterstützt diese Strukturen.

---

# Langfristiges Ziel

CanisTrack soll die zentrale Plattform für Hundesport im deutschsprachigen Raum werden.

Mögliche spätere Funktionen:

- Vereine nutzen die Plattform organisatorisch
- Trainer verwalten Trainingsgruppen
- Prüfungen werden digital dokumentiert
- Nutzer verfolgen komplette Hundebiografien
- Trainingsdaten ermöglichen intelligente Empfehlungen

---

# Entwicklungsphilosophie

Die Anwendung wird entwickelt wie ein professionelles SaaS-Produkt:

- klare Architektur
- automatisierte Tests
- Dokumentation
- sichere Erweiterbarkeit
- niedrige Betriebskosten

CI/CD per externem Dienst (z.B. GitHub Actions) ist bewusst nicht Teil
des Starts, siehe [TECH_STACK.md](TECH_STACK.md) "CI/CD" - Deployment
erfolgt über ein manuelles Skript, um keine zusätzlichen Kosten/Abhängigkeiten
zu erzeugen.

---

# Quickstart (lokale Entwicklung, ohne Docker)

Voraussetzungen: .NET 9 SDK, Node.js LTS, PostgreSQL 17 (lokal installiert,
siehe [DEPLOYMENT.md](DEPLOYMENT.md) "Lokal ohne Docker").

## 1. Datenbank

```bash
# Einmalig: App-Rolle und Dev-Datenbank anlegen (passend zu
# backend/src/CanisTrack.Api/appsettings.Development.json)
psql -U postgres -c "CREATE ROLE canistrack LOGIN PASSWORD 'canistrack_dev_pw';"
psql -U postgres -c "CREATE DATABASE canistrack_dev OWNER canistrack;"
```

## 2. Backend

```bash
cd backend
dotnet tool install --global dotnet-ef   # einmalig
dotnet ef database update --project src/CanisTrack.Infrastructure --startup-project src/CanisTrack.Api
cd src/CanisTrack.Api
dotnet run   # läuft auf http://localhost:5080, Swagger unter /swagger
```

Migrationen und Stammdaten (Rollen, Sportarten-Katalog) werden beim Start
in der Development-Umgebung automatisch angewendet/eingespielt.

## 3. Frontend

```bash
cd frontend
cp .env.local.example .env.local   # einmalig - zeigt auf das lokale Backend
npm install
npm run dev   # läuft auf http://localhost:3000
```

Login/Registrierung unter `/login` bzw. `/register`, danach Dashboard,
Hunde-Verwaltung und Sportarten-Katalog unter `/dashboard`, `/dogs`, `/sports`.

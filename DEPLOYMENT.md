# Deployment Konzept


# Ziel

Einfacher Betrieb.

Niedrige Kosten.

Skalierbar.


---

# Entwicklungsumgebung

## Lokal ohne Docker (aktueller Entwicklungsrechner)

Auf Entwicklungsrechnern ohne Docker läuft jeder Dienst nativ:

- Backend: `dotnet run` (ASP.NET Core, Kestrel auf https://localhost:5443)
- Frontend: `npm run dev` (Next.js auf http://localhost:3000)
- Database: lokal installiertes PostgreSQL (Service, kein Container)

Details siehe [README.md](README.md) Quickstart-Abschnitt.

### Zusätzliche Systemabhängigkeit: poppler-utils

Der Admin-Workflow "Prüfungsordnung-Import" (Übungsnamen + Punktzahlen aus
der lokalen, urheberrechtlich geschützten aber zur Nutzung freigegebenen
PDF extrahieren) ruft das externe Tool `pdftotext` auf, statt ein
NuGet-Paket einzubinden (ein naheliegendes Paket erwies sich beim Prüfen
der Versionshistorie als vermutlich kompromittiert, siehe Git-Historie).
`pdftotext` ist Teil von poppler-utils, kostenlos und auf jedem gängigen
Linux-Server über den Paketmanager installierbar:

- Lokal (Windows): `winget install oschwartz10612.Poppler`
- VPS (Debian/Ubuntu): `apt install poppler-utils`

Pfade werden über `RegulationImport:PdfPath` (lokale PDF, gitignored) und
`RegulationImport:PdftotextPath` (Standard: `pdftotext`, muss im PATH
sein) konfiguriert.

## Mit Docker (sobald verfügbar / auf der VPS)

Docker Compose Services:


frontend

backend

database

storage

worker



---

# Produktion MVP


## Variante A (gewählt)

Alles auf einer Hetzner VPS, um Betriebskosten auf einen Posten
zu beschränken:


Frontend (Next.js):

Docker Container auf der VPS


Backend:

Docker Container auf der VPS


Database:

PostgreSQL Docker Container auf der VPS


Storage:

Cloudflare R2 (kostenloses Freikontingent, kein eigener Server nötig)


---

# Deployment Flow

Kein CI/CD-Dienst in Phase 1 (siehe TECH_STACK.md) — Deployment erfolgt
manuell per Skript, um keine Kosten/Komplexität durch einen externen
CI-Anbieter zu erzeugen:


Developer (lokal)

↓

Build (`dotnet build`, `npm run build`)

↓

Tests (lokal ausführen)

↓

Docker Image bauen

↓

Image per SSH/`docker compose` auf die VPS übertragen

↓

`docker compose up -d` auf der VPS

---

GitHub Actions kann später optional ergänzt werden (siehe TECH_STACK.md),
ist für den Start aber nicht erforderlich.


---

# Backups


Pflicht:


Datenbank täglich.

Dateien regelmäßig.


---

# Monitoring


Start:


- Health Endpoint
- Logs


Später:

- Grafana
- Prometheus


---

# Skalierung


Wenn Wachstum:


## Schritt 1

größerer Server


## Schritt 2

Frontend CDN


## Schritt 3

Database Managed


## Schritt 4

Services trennen


---

# Sicherheit


Pflicht:


HTTPS

Firewall

Updates

Backups

Secrets Management

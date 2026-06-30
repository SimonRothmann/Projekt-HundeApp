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

## Mit Docker (auf der VPS)

Umgesetzt: `docker-compose.yml` (Repo-Wurzel) + `backend/Dockerfile` +
`frontend/Dockerfile`. Prod UND Test laufen auf derselben VPS, mit
gemeinsamer Postgres-Instanz (getrennte Datenbanken) und Caddy als
gemeinsamem Reverse Proxy mit automatischem TLS. Vollständige
Schritt-für-Schritt-Anleitung (Ersteinrichtung, laufende Deployments,
Backups): [deploy/README.md](deploy/README.md).

Services: `postgres`, `backend-prod`, `frontend-prod`, `backend-test`,
`frontend-test`, `caddy`. Storage/Worker (siehe unten) sind noch nicht
Teil des Compose-Setups - erst bei tatsächlichem Bedarf (Dateiuploads,
Hintergrundjobs) ergänzen, kein Premature-Setup für ungenutzte Services.

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

`scripts/deploy.sh` (siehe [deploy/README.md](deploy/README.md)):
Build (`dotnet build`, `npm run build`) + Tests lokal als Vorab-Check

↓

Code-Sync per `rsync` auf die VPS (kein Image-Registry nötig)

↓

`docker compose build && docker compose up -d` AUF der VPS

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

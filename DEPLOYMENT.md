# Deployment Konzept


# Ziel

Einfacher Betrieb.

Niedrige Kosten.

Skalierbar.


---

# Entwicklungsumgebung

## Lokal ohne Docker (aktueller Entwicklungsrechner)

Auf Entwicklungsrechnern ohne Docker läuft jeder Dienst nativ:

- Backend: `dotnet run` (ASP.NET Core, Kestrel auf http://localhost:5080, https auf Port 7297; siehe `launchSettings.json`)
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
`frontend-test`, `mail` (eigener Versand-Mailserver, nur ausgehend, siehe
[docs/BETRIEB.md](docs/BETRIEB.md) "E-Mail") und `caddy`. Einen eigenen
Worker- oder Storage-Dienst gibt es nicht: Die Hintergrundarbeit (Mail-,
Push-, Erinnerungs-, Wetter-, Plan- und Aufbewahrungsdienst) läuft als
`BackgroundService` im Backend-Prozess.

---

# Produktion MVP


## Variante A (gewählt)

Alles auf einer VPS, um Betriebskosten auf einen Posten zu beschränken.
Umgesetzt auf **Contabo** - in der Planungsphase stand hier Hetzner,
berichtigt am 2026-09-13, nachdem der veraltete Name über diese Unterlagen in
die Datenschutzerklärung gewandert war. Verbindlich für Rechtstexte ist
`frontend/src/lib/rechtliches.ts`:


Frontend (Next.js):

Docker Container auf der VPS


Backend:

Docker Container auf der VPS


Database:

PostgreSQL Docker Container auf der VPS


Dateien:

Es gibt keinen Dateispeicher. Hundebilder liegen als Bytes in der
Datenbank (`dog_images`) und gehen als Data-URI über die API, alles andere
sind Datenbankzeilen.

Cloudflare R2 dient nur den Sicherungen (siehe "Backups").


---

# Deployment Flow

Das Deployment bleibt manuell und zweistufig (Einzelheiten:
[deploy/README.md](deploy/README.md)):


Developer (lokal)

↓

Push auf `master`

↓

`scripts/deploy-test.sh` AUF der VPS: Git-Checkout aktualisieren, nur die
Test-Dienste neu bauen und starten

↓

Prüfung auf der Test-Umgebung

↓

`scripts/promote-to-prod.sh` (lokal): `prod`-Branch per Fast-Forward auf
`master`, Push, dann per SSH `scripts/deploy-prod.sh` auf der VPS

---

Die Prüfungen laufen zusätzlich in GitHub Actions
(`.github/workflows/ci.yml`: Backend und Frontend bei jedem Push und
Pull Request, siehe TECH_STACK.md "CI/CD"). Das Deployment selbst löst die
CI nicht aus.


---

# Backups


Pflicht:


Datenbank täglich (Dateien gibt es keine gesondert, siehe "Dateien" oben).


Umgesetzt seit 2026-09-09: verschlüsselte tägliche Sicherung nach
Cloudflare R2, Einrichtung und Wiederherstellung in
[docs/BACKUP.md](docs/BACKUP.md).


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


Wie Updates, Neustarts und Aufräumarbeiten auf der VPS laufen - und was
davon von Hand bleibt: [docs/BETRIEB.md](docs/BETRIEB.md).

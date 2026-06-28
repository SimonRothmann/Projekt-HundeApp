# Technology Stack

---

# Ziel

Maximale Professionalität bei minimalen Kosten.

---

# Frontend

## Next.js

https://nextjs.org/

Warum:

- moderne React Plattform
- SSR
- schnelle Ladezeiten
- PWA Unterstützung
- große Community


---

## TypeScript

Warum:

- weniger Fehler
- bessere Wartbarkeit
- KI kann besseren Code erzeugen


---

## Tailwind CSS

Warum:

- schneller Aufbau
- konsistentes Design


---

## Shadcn UI

Warum:

- modernes SaaS Design
- vollständig anpassbar


---

# Backend

## ASP.NET Core

Warum:

- Enterprise Standard
- performant
- kostenlos


---

## Entity Framework Core

Aufgaben:

- Datenbankzugriff
- Migrationen
- Models


---

# Datenbank

## PostgreSQL


Warum:

- Open Source
- kostenlos
- professionell
- JSON Support


Erweiterungen:

PostGIS

für GPS Daten.


---

# Authentifizierung

MVP:

ASP.NET Identity

Später:

Keycloak

oder

Auth0


---

# Hosting

## Phase 1: ein einziger Server (Zielkosten: ~0€ zusätzlich zum Server)

Entscheidung: Alles läuft auf einer einzigen Hetzner VPS (z.B. CX22),
um die Betriebskosten auf einen einzigen Posten zu reduzieren.

Frontend + Backend + Database + Worker:

Hetzner VPS via Docker Compose

Storage:

Cloudflare R2 (kostenloses Freikontingent)

DNS / SSL:

Cloudflare (kostenlos) als DNS-Proxy vor der VPS, Let's Encrypt für TLS

Begründung gegen Cloudflare Pages für das Frontend:

Next.js läuft im selben Docker Compose Stack wie das Backend.
Das vermeidet einen zusätzlichen Anbieter, vereinfacht Local-Parity
zwischen Entwicklung und Produktion und kostet nichts zusätzlich,
da die VPS bereits bezahlt wird. Cloudflare Pages bleibt eine spätere
Option, falls das Frontend losgelöst skaliert werden soll.

---

# Empfohlene Startarchitektur


Cloudflare (DNS + TLS Proxy, kostenlos)

   |

   |

Hetzner VPS

   |

Docker Compose

   |

Frontend (Next.js)

API

Database

Worker

Storage-Anbindung (Cloudflare R2)


---

# Warum nicht Kubernetes?

Nicht notwendig.

Komplexität ohne Nutzen.

---

# Container

Alles läuft über Docker.


Services:


frontend

backend

database

worker


---

# CI/CD

## Phase 1: kein CI/CD-Dienst

Um keine Kosten/Komplexität durch einen externen CI-Anbieter zu erzeugen,
startet das Projekt mit einem manuellen Deployment-Skript
(`deploy.sh`, lokal oder per SSH auf der VPS ausgeführt):

Push (lokal)

|

Build (lokal: `dotnet build`, `npm run build`)

|

Test (lokal)

|

Docker Image bauen + auf VPS übertragen

|

Deploy (Docker Compose restart)

## Später (optional, sobald Team wächst)

GitHub Actions kann ergänzt werden, sobald automatisierte Pipelines den
Aufwand wert sind. GitHub Actions ist für dieses Repo aktuell nicht
erforderlich und wird bewusst nicht eingerichtet, um keine zusätzliche
Abhängigkeit/Kosten einzuführen.


---

# Monitoring

Start:

- Application Logs
- Health Checks


Später:

- Grafana
- Prometheus
- OpenTelemetry

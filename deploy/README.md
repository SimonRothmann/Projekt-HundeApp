# Prod + Test auf einem VPS

Setzt voraus: Linux-VPS mit Docker + Docker Compose Plugin (z.B.
`curl -fsSL https://get.docker.com | sh`), SSH-Zugriff per Public Key.

Empfohlene Mindest-Specs für Prod+Test gemeinsam auf einer Maschine: 4
vCPU / 8 GB RAM / 50-100 GB SSD. Mit 2 OCPU/12 GB (z.B. Oracle Always
Free Ampere A1) ebenfalls machbar, aber knapp bei gleichzeitigen Builds -
siehe Hinweis unten zu `docker compose build`.

## Architektur

```
                    ┌─────────────┐
   Internet ───────▶│    Caddy    │  (Port 80/443, automatisches TLS)
                    └──────┬──────┘
              ┌────────────┼────────────┬─────────────┐
              ▼            ▼            ▼             ▼
       frontend-prod  backend-prod  frontend-test  backend-test
              │            │            │             │
              └────────────┴─────┬──────┴─────────────┘
                                  ▼
                            postgres (eine Instanz,
                         getrennte DBs: dogity_prod / dogity_test)
```

- **Prod** läuft mit `ASPNETCORE_ENVIRONMENT=Production`: kein Swagger,
  keine Demo-Daten, CORS nur für `PROD_DOMAIN`.
- **Test** läuft mit `ASPNETCORE_ENVIRONMENT=Development`: Swagger unter
  `/swagger`, automatisch befüllte Demo-Accounts (siehe
  `DemoDataSeeder.cs`), CORS permissiv. Bewusst kein eigener
  "Staging"-Modus - nutzt den bereits vorhandenen Dev-Pfad.
- Migrationen + Sportarten-/Übungskatalog laufen in **beiden** Umgebungen
  automatisch beim Containerstart (siehe `Program.cs`).

## Einmalige Einrichtung

1. **DNS**: vier A-Records auf die VPS-IP anlegen - `PROD_DOMAIN`,
   `PROD_API_DOMAIN`, `TEST_DOMAIN`, `TEST_API_DOMAIN` (siehe
   `.env.example`). Caddy braucht das für die automatische
   Let's-Encrypt-Zertifikatsausstellung (HTTP-01-Challenge).

2. **Verzeichnis auf der VPS anlegen**:
   ```bash
   ssh user@vps-ip "mkdir -p /opt/dogity"
   ```

3. **`.env` auf der VPS anlegen** (NIE lokal committen, NIE per `scripts/deploy.sh`
   übertragen - das Skript schließt `.env` explizit aus):
   ```bash
   scp .env.example user@vps-ip:/opt/dogity/.env
   ssh user@vps-ip "nano /opt/dogity/.env"   # echte Werte eintragen
   ```
   Passwörter/Secrets generieren: `openssl rand -base64 32` (DB-Passwörter),
   `openssl rand -base64 48` (JWT-Secrets).

4. **Erstes Deployment**:
   ```bash
   VPS_HOST=user@vps-ip ./scripts/deploy.sh
   ```
   Das baut alle Images auf der VPS und startet den Stack. Postgres legt
   beim allerersten Start automatisch beide Datenbanken an (siehe
   `deploy/postgres-init/`), Migrationen + Katalog laufen automatisch.

5. **Ersten Admin-Account einrichten**: über die UI unter
   `https://<TEST_DOMAIN>` bzw. `https://<PROD_DOMAIN>` registrieren,
   dann `PROD_ADMIN_EMAIL`/`TEST_ADMIN_EMAIL` in der `.env` auf diese
   Adresse setzen und `docker compose up -d` erneut ausführen (oder
   einfach beim nächsten regulären Deploy - `AdminBootstrapper` läuft bei
   jedem Start und vergibt die Rolle nachträglich).

## Laufende Deployments

```bash
VPS_HOST=user@vps-ip ./scripts/deploy.sh
```

Baut lokal (Checks), synct den Code, baut auf der VPS neu und startet neu
durch. Bei reinen Code-Änderungen ohne neue Dependencies dauert der
Docker-Build dank Layer-Caching meist nur wenige Sekunden bis Minuten.

**Hinweis bei knappem RAM (2 OCPU/12 GB)**: `docker compose build` baut
alle vier Anwendungs-Images (Prod+Test je Backend+Frontend) - der
Next.js-Build (`npm run build`) ist dabei der speicherhungrigste Schritt.
Bei Bedarf nacheinander statt parallel bauen:
```bash
ssh user@vps-ip "cd /opt/dogity && docker compose build backend-prod && docker compose build frontend-prod && docker compose build backend-test && docker compose build frontend-test && docker compose up -d"
```

## Backups

Tägliches Datenbank-Backup beider Datenbanken (Cronjob auf der VPS):
```bash
docker compose exec -T postgres pg_dump -U postgres dogity_prod | gzip > /opt/dogity/backups/dogity_prod_$(date +%F).sql.gz
docker compose exec -T postgres pg_dump -U postgres dogity_test | gzip > /opt/dogity/backups/dogity_test_$(date +%F).sql.gz
```

## Logs / Status

```bash
ssh user@vps-ip "cd /opt/dogity && docker compose ps"
ssh user@vps-ip "cd /opt/dogity && docker compose logs -f backend-prod"
```

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

3. **`.env` auf der VPS anlegen** (NIE lokal committen - `.env` ist gitignored,
   die Deploy-Skripte pullen nur den Repo-Code und lassen die `.env` unangetastet):
   ```bash
   scp .env.example user@vps-ip:/opt/dogity/.env
   ssh user@vps-ip "nano /opt/dogity/.env"   # echte Werte eintragen
   ```
   Passwörter/Secrets generieren: `openssl rand -base64 32` (DB-Passwörter),
   `openssl rand -base64 48` (JWT-Secrets).

4. **Erstes Deployment**: Repo direkt auf der VPS klonen (der Zwei-Stufen-
   Workflow arbeitet mit einem Git-Checkout unter `/opt/dogity`):
   ```bash
   ssh user@vps-ip
   sudo mkdir -p /opt/dogity && sudo chown "$USER:$USER" /opt/dogity
   git clone https://github.com/SimonRothmann/Projekt-HundeApp.git /opt/dogity
   cd /opt/dogity && docker compose up -d --build
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

## Laufende Deployments (Zwei-Stufen-Workflow)

Neuer Code wird **immer erst gegen Test deployt**, dort verifiziert und
anschließend in einem separaten, expliziten Schritt nach Prod promotet.
Dahinter zwei Git-Branches:

- `master` = das, was auf der Test-Umgebung läuft
- `prod`   = das, was auf der Prod-Umgebung läuft

Prod-Fixes werden immer erst auf `master` gemacht und dann promotet -
niemals direkt auf `prod` committen (der Promote-Schritt setzt einen
Fast-Forward-Merge voraus, jeder direkte Commit auf `prod` würde ihn
brechen).

### 1. Test-Deploy (nach jedem master-Push)

```bash
ssh dogity /opt/dogity/scripts/deploy-test.sh
```

Zieht den aktuellen master-Stand und baut/startet nur `backend-test` +
`frontend-test` neu. Prod-Services bleiben unangetastet.

### 2. Prod-Promote (nach positivem Test)

```bash
./scripts/promote-to-prod.sh
```

Läuft **lokal auf dem Entwicklungsrechner**: prüft, dass der Working
Tree sauber und auf master ist, fast-forwardet den prod-Branch auf
master, pusht ihn, und triggert per SSH `deploy-prod.sh` auf der VPS.
Danach steht der Working Tree wieder auf master.

**Hinweis bei knappem RAM (2 OCPU/12 GB)**: der Next.js-Build
(`npm run build`) ist der speicherhungrigste Schritt. Da wir jetzt
Prod und Test getrennt deployen, werden nie mehr alle vier Anwendungs-
Images gleichzeitig gebaut - das Problem ist damit implizit gelöst.

## Rollback

Prod-Rollback auf den vorherigen Stand:
```bash
# Auf dem Entwicklungsrechner: prod hart auf den vorherigen Commit setzen
git checkout prod
git reset --hard <vorheriger-commit-sha>
git push --force-with-lease origin prod
git checkout master
# und dann auf der VPS deploy-prod neu triggern:
ssh dogity /opt/dogity/scripts/deploy-prod.sh
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

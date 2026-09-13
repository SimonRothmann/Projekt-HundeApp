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

**Der Cronjob ist nicht Teil des Repos und muss auf der VPS einmalig
eingerichtet werden.** Er stand hier lange nur als zwei lose Befehlszeilen -
und war am 2026-09-13 nachweislich nie eingerichtet: Das Verzeichnis
`/opt/dogity/backups` existierte nicht, es gab also keine einzige Sicherung.
Deshalb jetzt zum Kopieren und mit einem Befehl zum Nachsehen.

Prüfen, ob Sicherungen laufen:
```bash
crontab -l | grep -i backup; ls -lh /opt/dogity/backups 2>/dev/null || echo "KEIN Backup-Verzeichnis - es läuft nichts"
```

Einmalig einrichten (legt Skript und Cronjob an, 3:30 Uhr nachts):
```bash
mkdir -p /opt/dogity/backups /opt/dogity/scripts
cat > /opt/dogity/scripts/backup.sh <<'SKRIPT'
#!/bin/sh
set -e
cd /opt/dogity
for db in dogity_prod dogity_test; do
  docker compose exec -T postgres pg_dump -U postgres "$db" | gzip > "/opt/dogity/backups/${db}_$(date +%F).sql.gz"
done
# Aufbewahrung: 30 Tage. Dieselbe Frist nennt die Datenschutzerklärung
# (frontend/src/lib/rechtliches.ts, SICHERUNG_AUFBEWAHRUNG_TAGE) - beide
# müssen zusammenpassen.
find /opt/dogity/backups -name "dogity_*.sql.gz" -mtime +30 -delete
SKRIPT
chmod +x /opt/dogity/scripts/backup.sh
( crontab -l 2>/dev/null; echo "30 3 * * * /opt/dogity/scripts/backup.sh >> /var/log/dogity-backup.log 2>&1" ) | crontab -
```

Einmal von Hand auslösen - eine Sicherung, die nie gelaufen ist, ist keine:
```bash
/opt/dogity/scripts/backup.sh && ls -lh /opt/dogity/backups
```

Wiederherstellen (nur in eine LEERE Datenbank - ein Einspielen über einen
bestehenden Datenbestand schlägt an den vorhandenen Tabellen fehl):
```bash
gunzip -c /opt/dogity/backups/dogity_prod_JJJJ-MM-TT.sql.gz \
  | docker compose exec -T postgres psql -U postgres dogity_prod
```

Zwei Gründe, warum das nicht warten sollte: Ohne Sicherung ist ein verlorener
Datenbestand endgültig verloren - Trainingstagebücher über Jahre, die niemand
nachtragen kann. Und die Datenschutzerklärung sagt zu, dass täglich gesichert
und nach 30 Tagen gelöscht wird; solange nichts läuft, stimmt dieser Satz
nicht.

## Logs / Status

```bash
ssh user@vps-ip "cd /opt/dogity && docker compose ps"
ssh user@vps-ip "cd /opt/dogity && docker compose logs -f backend-prod"
```

#!/usr/bin/env bash
# Prod-Deploy: läuft AUF DER VPS.
#
# Ablauf: prod-Branch pullen und die Prod-Services (backend-prod,
# frontend-prod) neu bauen. Test-Services bleiben unangetastet. Danach
# wird der Working Tree zurück auf master gestellt, damit ein anschließender
# deploy-test.sh sofort auf dem erwarteten Branch startet.
#
# Nicht direkt aufrufen - immer über promote-to-prod.sh auf dem Entwicklungs-
# rechner, damit sichergestellt ist, dass master vorher sauber in prod
# gemerged und gepusht wurde.
set -euo pipefail

REPO_DIR="${REPO_DIR:-/opt/dogity}"
cd "$REPO_DIR"

echo "==> prod-Branch frisch ziehen"
git fetch origin
git checkout prod
git reset --hard origin/prod

echo "==> Prod-Container bauen und starten"
# Reihenfolge ist wichtig, nicht Geschmackssache: Das Frontend fragt beim
# BAUEN den Prüfungsordnungs-Katalog über NEXT_PUBLIC_API_URL ab und backt
# daraus seine Seiten und die Sitemap (generateStaticParams, app/sitemap.ts -
# siehe docs/SEO.md). Werden beide Images in einem Rutsch gebaut, antwortet
# waehrend des Frontend-Builds noch das ALTE Backend - neue Pruefungsordnungen
# fehlen dann im Frontend, obwohl das Backend sie laengst kennt. Genau das ist
# am 2026-08-23 passiert (Turnierhundsport/Agility im Backend da, im Frontend
# 404). Deshalb: erst das Backend hochziehen, auf seinen Health-Check warten,
# dann das Frontend bauen.
docker compose up -d --build --force-recreate backend-prod

echo "==> Warte auf das neue Backend (prod)"
# /health antwortet erst nach Migration und Seedern (siehe Program.cs) - ein
# erfolgreicher Aufruf heisst also: der neue Katalog steht bereit.
for i in $(seq 1 60); do
  if curl -sfS -o /dev/null "https://api.dogity.net/health"; then
    echo "    Backend ist oben (nach $((i*2))s)"
    break
  fi
  if [ "$i" -eq 60 ]; then
    echo "    Backend antwortet nach 120s nicht - Abbruch, Frontend wird NICHT gebaut." >&2
    exit 1
  fi
  sleep 2
done

echo "==> Frontend bauen und starten"
# Erzwingt einen echten Neubau statt der gecachten Build-Schicht - der
# Next-Build hängt von den API-Daten ab, nicht nur von der Quelle (siehe
# frontend/Dockerfile).
export BUILD_REF="$(date -u +%Y%m%dT%H%M%SZ)"
docker compose up -d --build --force-recreate frontend-prod

echo "==> Abschliessender Rauchtest"
curl -sS -o /dev/null -w "api:    HTTP %{http_code}\n" https://api.dogity.net/health
curl -sS -o /dev/null -w "prod:   HTTP %{http_code}\n" https://dogity.net/

echo "==> Working Tree zurück auf master für nachfolgenden Test-Deploy"
git checkout master
git reset --hard origin/master

echo "==> Container-Status"
docker compose ps --format "table {{.Service}}\t{{.Status}}"

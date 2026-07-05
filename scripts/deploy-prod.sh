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
docker compose up -d --build --force-recreate backend-prod frontend-prod

echo "==> Warte 15 s auf Backend-Migration + Health-Check"
sleep 15
curl -sS -o /dev/null -w "api:    HTTP %{http_code}\n" https://api.dogity.net/health
curl -sS -o /dev/null -w "prod:   HTTP %{http_code}\n" https://dogity.net/

echo "==> Working Tree zurück auf master für nachfolgenden Test-Deploy"
git checkout master
git reset --hard origin/master

echo "==> Container-Status"
docker compose ps --format "table {{.Service}}\t{{.Status}}"

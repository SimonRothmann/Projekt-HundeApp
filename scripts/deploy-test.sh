#!/usr/bin/env bash
# Test-Deploy: läuft AUF DER VPS.
#
# Ablauf: master-Branch pullen und die Test-Services (backend-test,
# frontend-test) neu bauen. Prod-Services (backend-prod, frontend-prod)
# bleiben unangetastet und laufen unverändert weiter mit dem, was zuletzt
# vom prod-Branch gebaut wurde. Postgres/Caddy laufen shared.
#
# Aufruf (lokal auf dem Entwicklungsrechner): `ssh dogity /opt/dogity/scripts/deploy-test.sh`
# Aufruf (direkt auf der VPS): `sudo -u dogity /opt/dogity/scripts/deploy-test.sh`
set -euo pipefail

REPO_DIR="${REPO_DIR:-/opt/dogity}"
cd "$REPO_DIR"

echo "==> Aktueller Branch:"
git rev-parse --abbrev-ref HEAD

echo "==> master frisch ziehen"
git fetch origin
git checkout master
git reset --hard origin/master

echo "==> Test-Container bauen und starten"
# --force-recreate stellt sicher, dass auch bei unveränderten Images ein
# frischer Container startet (relevant, wenn nur env-Werte via Volumes
# geändert wurden - Images unverändert, aber Container muss neu).
docker compose up -d --build --force-recreate backend-test frontend-test

echo "==> Warte 15 s auf Backend-Migration + Health-Check"
sleep 15
curl -sS -o /dev/null -w "test-api: HTTP %{http_code}\n" https://api-test.dogity.net/health
curl -sS -o /dev/null -w "test:     HTTP %{http_code}\n" https://test.dogity.net/

echo "==> Container-Status"
docker compose ps --format "table {{.Service}}\t{{.Status}}"

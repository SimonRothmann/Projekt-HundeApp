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

# git spricht mit GitHub bewusst über HTTP/1.1.
#
# Mit HTTP/2 (der Vorgabe) beantwortet GitHub den Protokoll-v2-Handshake von
# git 2.43 mit "www-authenticate: Basic realm=GitHub" - also 401 - obwohl das
# Repository öffentlich ist. Auf einem Rechner ohne Terminal endet das in
# "could not read Username for 'https://github.com'" und der Deploy bricht ab,
# bevor irgendetwas gebaut wird. Nachgemessen auf der VPS: v2 über HTTP/2
# scheitert reproduzierbar, v2 über HTTP/1.1 und v0 über HTTP/2 laufen durch;
# derselbe Abruf per curl liefert in allen Varianten 200. Es liegt also an
# gits HTTP/2-Weg, nicht an Netz, Sichtbarkeit oder Zugangsdaten.
#
# Der Geschwindigkeitsunterschied ist bei einem Repo dieser Größe belanglos.
GIT_HTTP="-c http.version=HTTP/1.1"

cd "$REPO_DIR"

echo "==> Aktueller Branch:"
git rev-parse --abbrev-ref HEAD

echo "==> master frisch ziehen"
git $GIT_HTTP fetch origin
git checkout master
git reset --hard origin/master

echo "==> Test-Container bauen und starten"
# --force-recreate stellt sicher, dass auch bei unveränderten Images ein
# frischer Container startet (relevant, wenn nur env-Werte via Volumes
# geändert wurden - Images unverändert, aber Container muss neu).
# Reihenfolge ist wichtig, nicht Geschmackssache: Das Frontend fragt beim
# BAUEN den Prüfungsordnungs-Katalog über NEXT_PUBLIC_API_URL ab und backt
# daraus seine Seiten und die Sitemap (generateStaticParams, app/sitemap.ts -
# siehe docs/SEO.md). Werden beide Images in einem Rutsch gebaut, antwortet
# waehrend des Frontend-Builds noch das ALTE Backend - neue Pruefungsordnungen
# fehlen dann im Frontend, obwohl das Backend sie laengst kennt. Genau das ist
# am 2026-08-23 passiert (Turnierhundsport/Agility im Backend da, im Frontend
# 404). Deshalb: erst das Backend hochziehen, auf seinen Health-Check warten,
# dann das Frontend bauen.
docker compose up -d --build --force-recreate backend-test

echo "==> Warte auf das neue Backend (test)"
# /health antwortet erst nach Migration und Seedern (siehe Program.cs) - ein
# erfolgreicher Aufruf heisst also: der neue Katalog steht bereit.
for i in $(seq 1 60); do
  if curl -sfS -o /dev/null "https://api-test.dogity.net/health"; then
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
docker compose up -d --build --force-recreate frontend-test

echo "==> Abschliessender Rauchtest"
curl -sS -o /dev/null -w "test-api: HTTP %{http_code}\n" https://api-test.dogity.net/health
curl -sS -o /dev/null -w "test:     HTTP %{http_code}\n" https://test.dogity.net/

echo "==> Container-Status"
docker compose ps --format "table {{.Service}}\t{{.Status}}"

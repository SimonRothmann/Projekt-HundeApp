#!/usr/bin/env bash
# Promotet den aktuellen master-Stand in Prod: merged lokal master -> prod
# per Fast-Forward, pusht prod, führt anschließend auf der VPS
# deploy-prod.sh aus. Der lokale Working Tree bleibt am Ende auf master.
#
# Aufruf: `./scripts/promote-to-prod.sh`
#
# Voraussetzungen:
# - master ist sauber (kein uncommitted work), origin/master ist up-to-date
# - prod kann per Fast-Forward auf master gebracht werden (keine Divergenz -
#   Prod-Fixes werden immer erst auf master gemacht und dann promotet, nie
#   direkt auf prod)
# - SSH-Zugang zur VPS unter Alias "dogity" (siehe ~/.ssh/config)
set -euo pipefail

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

echo "==> Sanity-Check"
if [[ -n "$(git status --porcelain)" ]]; then
  echo "Working Tree nicht sauber. Bitte committen/stashen und erneut versuchen."
  exit 1
fi

CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [[ "$CURRENT_BRANCH" != "master" ]]; then
  echo "Aktueller Branch ist $CURRENT_BRANCH, erwartet: master. Bitte wechseln."
  exit 1
fi

echo "==> master synchronisieren"
git $GIT_HTTP fetch origin
git $GIT_HTTP pull --ff-only

echo "==> prod fast-forwarden auf master"
git checkout prod
git $GIT_HTTP fetch origin
git $GIT_HTTP pull --ff-only
git merge --ff-only master
git $GIT_HTTP push origin prod
git checkout master

echo "==> Deploy auf VPS auslösen"
ssh dogity /opt/dogity/scripts/deploy-prod.sh

echo "==> Fertig. Prod läuft jetzt mit dem gerade freigegebenen master-Stand."

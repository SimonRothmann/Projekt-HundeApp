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
git fetch origin
git pull --ff-only

echo "==> prod fast-forwarden auf master"
git checkout prod
git fetch origin
git pull --ff-only
git merge --ff-only master
git push origin prod
git checkout master

echo "==> Deploy auf VPS auslösen"
ssh dogity /opt/dogity/scripts/deploy-prod.sh

echo "==> Fertig. Prod läuft jetzt mit dem gerade freigegebenen master-Stand."

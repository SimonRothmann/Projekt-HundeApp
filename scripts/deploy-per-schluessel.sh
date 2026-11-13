#!/usr/bin/env bash
# Erzwungener Befehl für den Deploy-Schlüssel der GitHub Action
# (.github/workflows/deploy.yml). Läuft AUF DER VPS.
#
# In ~/.ssh/authorized_keys des Benutzers dogity steht der Schlüssel mit
# command="/opt/dogity/scripts/deploy-per-schluessel.sh",restrict - wer sich
# mit ihm anmeldet, landet immer hier, nie in einer Shell. Was der Aufrufer
# als Befehl mitschickt, steht in SSH_ORIGINAL_COMMAND und darf nur "test"
# oder "prod" sein.
#
# "prod" ruft deploy-prod.sh direkt auf: Den Fast-Forward von master nach prod
# erledigt die Action vorher selbst (das, was sonst promote-to-prod.sh auf dem
# Entwicklungsrechner tut).
set -euo pipefail

case "${SSH_ORIGINAL_COMMAND:-}" in
  test) exec /opt/dogity/scripts/deploy-test.sh ;;
  prod) exec /opt/dogity/scripts/deploy-prod.sh ;;
  *)
    echo "Erlaubt sind nur: test, prod" >&2
    exit 2
    ;;
esac

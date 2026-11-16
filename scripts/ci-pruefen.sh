#!/usr/bin/env bash
# Prüft, dass der CI-Lauf (.github/workflows/ci.yml) für genau diesen Commit
# erfolgreich war. Läuft in den Deploy-Workflows auf dem GitHub-Runner, vor dem
# Deploy - nicht auf der VPS.
#
# Aufruf: scripts/ci-pruefen.sh <Commit-SHA>
# Braucht GH_TOKEN (Recht actions: read) und GITHUB_REPOSITORY; beides setzt
# der Workflow.
#
# - Ein erfolgreicher Lauf für den Commit genügt (egal ob er durch einen Push auf
#   master, auf prod oder durch einen Pull Request entstand - der Commit ist derselbe).
# - Läuft die CI noch, wird gewartet (höchstens CI_WARTEZEIT Sekunden, Vorgabe 25 min).
# - Steht noch gar kein Lauf da, wird kurz gewartet (frischer Push: GitHub legt ihn
#   erst nach einigen Sekunden an), dann gilt die CI als fehlend.
# - Rot, abgebrochen oder fehlend: Abbruch mit Meldung (Exit-Code 1).
set -euo pipefail

sha="${1:?Aufruf: ci-pruefen.sh <Commit-SHA>}"
repo="${GITHUB_REPOSITORY:?GITHUB_REPOSITORY fehlt}"
warte_max="${CI_WARTEZEIT:-1500}"
warte_auf_lauf="${CI_WARTEZEIT_AUF_LAUF:-180}"
pause="${CI_PAUSE:-20}"

kurz="${sha:0:7}"
start=$SECONDS

while true; do
  laeufe=$(gh api "repos/${repo}/actions/workflows/ci.yml/runs?head_sha=${sha}&per_page=30" \
    --jq '[.workflow_runs[] | {status, conclusion, event, url: .html_url}]')

  gesamt=$(jq 'length' <<< "$laeufe")
  gut=$(jq '[.[] | select(.conclusion == "success")] | length' <<< "$laeufe")
  offen=$(jq '[.[] | select(.status != "completed")] | length' <<< "$laeufe")

  if [ "$gut" -gt 0 ]; then
    echo "CI für ${kurz} ist grün."
    exit 0
  fi

  vergangen=$((SECONDS - start))

  if [ "$gesamt" -eq 0 ]; then
    if [ "$vergangen" -ge "$warte_auf_lauf" ]; then
      echo "::error::Für den Commit ${kurz} gibt es keinen CI-Lauf. Erst pushen und die CI abwarten (oder den Lauf unter Actions -> CI neu starten)."
      exit 1
    fi
  elif [ "$offen" -gt 0 ]; then
    if [ "$vergangen" -ge "$warte_max" ]; then
      echo "::error::Die CI für ${kurz} läuft nach $((warte_max / 60)) Minuten noch. Später erneut versuchen."
      exit 1
    fi
  else
    echo "::error::Die CI für ${kurz} ist nicht grün (kein erfolgreicher Lauf):"
    jq -r '.[] | "  - \(.event): \(.conclusion) \(.url)"' <<< "$laeufe"
    echo "::error::Erst die CI grün bekommen. Ein 'cancelled' heißt: ein neuerer Push hat den Lauf ersetzt - den Lauf unter Actions -> CI neu starten oder einen neueren Stand deployen."
    exit 1
  fi

  echo "Warte auf die CI für ${kurz} (${vergangen} s) ..."
  sleep "$pause"
done

#!/usr/bin/env bash
# Sicherung zurückspielen. Läuft LOKAL auf dem Entwicklungsrechner, nicht
# auf der VPS - und das ist Absicht:
#
# Der geheime GPG-Schlüssel liegt bewusst nur hier (bzw. in 1Password). Auf
# der VPS steht nur der öffentliche. Entschlüsselt wird deshalb hier, und der
# entschlüsselte Strom läuft durch die SSH-Verbindung direkt in psql. Der
# geheime Schlüssel betritt den Server nie, und es liegt auch nie eine
# unverschlüsselte Kopie auf einer Platte.
#
#   ./scripts/restore-db.sh <quelle> [zieldatenbank]
#
#   quelle          lokale .gpg-Datei ODER ein R2-Pfad
#                   (z.B. r2:dogity-backups/taeglich/dogity_prod_2026-09-09.sql.gz.gpg)
#   zieldatenbank   dogity_test (Vorgabe) oder dogity_prod
#
# Beispiel für die vierteljährliche Rückspielprobe (siehe docs/BACKUP.md):
#   ./scripts/restore-db.sh r2:dogity-backups/taeglich/dogity_prod_2026-09-09.sql.gz.gpg
set -euo pipefail

QUELLE="${1:-}"
ZIEL_DB="${2:-dogity_test}"
SSH_ZIEL="${SSH_ZIEL:-dogity}"
REPO_DIR="${REPO_DIR:-/opt/dogity}"

if [ -z "$QUELLE" ]; then
  echo "Aufruf: $0 <quelle> [zieldatenbank]" >&2
  echo "        $0 r2:dogity-backups/taeglich/dogity_prod_2026-09-09.sql.gz.gpg" >&2
  exit 1
fi

case "$ZIEL_DB" in
  dogity_test|dogity_prod) ;;
  *) echo "Unbekannte Zieldatenbank: $ZIEL_DB" >&2; exit 1 ;;
esac

# Prod zu überschreiben ist die eine Sache, die dieses Skript unumkehrbar
# kaputt machen kann. Deshalb muss der Name getippt werden - ein "j" reicht
# nicht, weil man "j" auch aus Versehen drückt.
if [ "$ZIEL_DB" = "dogity_prod" ]; then
  echo
  echo "  ACHTUNG: Das überschreibt die PRODUKTIVE Datenbank vollständig."
  echo "  Alles, was seit '$(basename "$QUELLE")' entstanden ist, ist danach weg."
  echo
  printf "  Zum Bestätigen dogity_prod eintippen: "
  read -r bestaetigung
  [ "$bestaetigung" = "dogity_prod" ] || { echo "Abgebrochen."; exit 1; }
fi

# Quelle holen. R2-Pfade laufen über rclone, alles andere ist eine Datei.
arbeitsdatei=""
aufraeumen() { [ -n "$arbeitsdatei" ] && rm -f "$arbeitsdatei"; }
trap aufraeumen EXIT

if [[ "$QUELLE" == *:* && "$QUELLE" != /* ]]; then
  command -v rclone >/dev/null || { echo "rclone fehlt - siehe docs/BACKUP.md" >&2; exit 1; }
  arbeitsdatei="$(mktemp -t dogity-restore)"
  echo "==> Hole $QUELLE"
  rclone cat "$QUELLE" > "$arbeitsdatei"
  eingabe="$arbeitsdatei"
else
  [ -f "$QUELLE" ] || { echo "Datei nicht gefunden: $QUELLE" >&2; exit 1; }
  eingabe="$QUELLE"
fi

# Erst entschlüsseln und prüfen, dann anfassen. Ein kaputter Schlüsselbund
# soll auffallen, BEVOR die Zieldatenbank geleert ist.
echo "==> Entschlüsseln und prüfen"
zeilen="$(gpg --batch --quiet --decrypt "$eingabe" 2>/dev/null | gunzip | head -50 | grep -c 'PostgreSQL database dump' || true)"
[ "$zeilen" -ge 1 ] || {
  echo "Die entschlüsselten Daten sehen nicht wie ein pg_dump aus." >&2
  echo "Steckt der richtige geheime Schlüssel im Schlüsselbund? (gpg --list-secret-keys)" >&2
  exit 1
}

# Zielschema leeren. Nicht die Datenbank löschen: daran hängen die laufenden
# Backend-Container, und DROP DATABASE scheitert, solange jemand verbunden
# ist. Das Schema neu anzulegen erreicht dasselbe, ohne die Container
# anzuhalten - und AUTHORIZATION setzt gleich den richtigen Eigentümer,
# damit der Dump (mit --no-owner erstellt) unter der Zielrolle einspielbar ist.
echo "==> Schema in $ZIEL_DB zurücksetzen"
ssh "$SSH_ZIEL" "cd $REPO_DIR && docker compose exec -T postgres psql -U postgres -v ON_ERROR_STOP=1 -d $ZIEL_DB" <<SQL
DROP SCHEMA IF EXISTS public CASCADE;
CREATE SCHEMA public AUTHORIZATION $ZIEL_DB;
SQL

echo "==> Einspielen"
gpg --batch --quiet --decrypt "$eingabe" \
  | gunzip \
  | ssh "$SSH_ZIEL" "cd $REPO_DIR && docker compose exec -T postgres psql -U $ZIEL_DB -v ON_ERROR_STOP=1 -d $ZIEL_DB -q"

# ANALYZE zuerst: pg_stat_user_tables fuehrt Schaetzwerte, die direkt nach
# einem Einspielen noch auf null stehen. Ohne das sieht eine gelungene
# Wiederherstellung aus wie eine leere Datenbank.
echo "==> Gegenprobe"
ssh "$SSH_ZIEL" "cd $REPO_DIR && docker compose exec -T postgres psql -U postgres -d $ZIEL_DB -q -c 'ANALYZE;' -c \
  \"select count(*) as tabellen from information_schema.tables where table_schema='public';\" -c \
  \"select relname as tabelle, n_live_tup as zeilen from pg_stat_user_tables order by n_live_tup desc limit 8;\""

echo
echo "==> Fertig. Wenn $ZIEL_DB von einem Backend benutzt wird, einmal neu starten:"
echo "    ssh $SSH_ZIEL 'cd $REPO_DIR && docker compose restart backend-${ZIEL_DB#dogity_}'"

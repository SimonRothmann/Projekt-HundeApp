#!/usr/bin/env bash
# Erzeugt das VAPID-Schlüsselpaar für Web-Push-Benachrichtigungen und gibt die
# drei .env-Zeilen aus (Hintergrund: docs/BETRIEB.md "E-Mail und Push aktivieren").
#
# Nur openssl und die üblichen Werkzeuge (tail, dd, tr) - nichts wird geladen
# oder installiert. Läuft lokal; der geheime Schlüssel gehört in die .env auf
# dem Server, nicht ins Repo.
#
# Aufruf:
#   scripts/vapid-schluessel.sh [PRAEFIX] [DATEI]
#
#   PRAEFIX  PROD (Vorgabe) oder TEST - passend zu den Namen in docker-compose.yml
#            (PROD_VAPID_PUBLIC_KEY usw.). Prod und Test bekommen bewusst
#            verschiedene Schlüssel, wie bei den JWT-Geheimnissen.
#   DATEI    z. B. .env - die Zeilen werden angehängt, auf dem Bildschirm
#            erscheint nur der ÖFFENTLICHE Schlüssel. So muss der private nie
#            sichtbar werden. Ohne DATEI gehen alle drei Zeilen nach stdout:
#            `scripts/vapid-schluessel.sh PROD >> .env` tut dasselbe.
#
# Der Kontakt (VAPID_SUBJECT, "mailto:...") steht für die Push-Dienste von
# Apple/Google/Mozilla: Sie melden sich dort, falls etwas schiefläuft. Vorgabe
# info@dogity.net; mit `VAPID_SUBJECT=mailto:... scripts/vapid-schluessel.sh`
# änderbar.
#
# Ein vorhandenes Schlüsselpaar wird NIE überschrieben: Mit einem neuen Paar
# wären alle angemeldeten Geräte auf einen Schlag tot, und jede Person müsste
# Push neu einschalten. "Vorhanden" heißt: der private Schlüssel hat einen Wert.
# Die leeren Platzhalter aus .env.example (`PROD_VAPID_PRIVATE_KEY=`) zählen
# nicht - sie werden beim Anhängen ersetzt, damit keine doppelten Zeilen
# entstehen.
set -euo pipefail

PRAEFIX="${1:-PROD}"
DATEI="${2:-}"
SUBJECT="${VAPID_SUBJECT:-mailto:info@dogity.net}"

case "$PRAEFIX" in
  PROD | TEST) ;;
  *)
    echo "Erstes Argument: PROD oder TEST (war: $PRAEFIX)." >&2
    exit 1
    ;;
esac

if ! command -v openssl > /dev/null 2>&1; then
  echo "openssl fehlt." >&2
  exit 1
fi

# Muster "=." verlangt mindestens ein Zeichen hinter dem Gleichheitszeichen.
if [ -n "$DATEI" ] && [ -f "$DATEI" ] && grep -q "^${PRAEFIX}_VAPID_PRIVATE_KEY=." "$DATEI"; then
  echo "In $DATEI steht schon ${PRAEFIX}_VAPID_PRIVATE_KEY - nichts geändert." >&2
  echo "Ein neues Schlüsselpaar würde alle angemeldeten Geräte ungültig machen." >&2
  exit 1
fi

TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT

# SEC1-DER eines P-256-Schlüssels (121 Byte): Der private Schlüssel (32 Byte)
# beginnt bei Byte 8 (Offset 7), der öffentliche, unkomprimiert (0x04 + X + Y,
# 65 Byte), sind die letzten 65 Byte.
openssl ecparam -name prime256v1 -genkey -noout -outform DER > "$TMP" 2> /dev/null

if [ "$(wc -c < "$TMP" | tr -d ' ')" -ne 121 ]; then
  echo "Unerwartetes Schlüsselformat von openssl - abgebrochen." >&2
  exit 1
fi

base64url() {
  openssl base64 -A | tr '+/' '-_' | tr -d '='
}

PRIVAT="$(dd if="$TMP" bs=1 skip=7 count=32 2> /dev/null | base64url)"
OEFFENTLICH="$(tail -c 65 "$TMP" | base64url)"

if [ -z "$PRIVAT" ] || [ -z "$OEFFENTLICH" ]; then
  echo "Schlüssel konnte nicht gelesen werden - abgebrochen." >&2
  exit 1
fi

ZEILEN="${PRAEFIX}_VAPID_PUBLIC_KEY=${OEFFENTLICH}
${PRAEFIX}_VAPID_PRIVATE_KEY=${PRIVAT}
${PRAEFIX}_VAPID_SUBJECT=${SUBJECT}"

if [ -n "$DATEI" ]; then
  # Leere Platzhalter dieses Präfixes (aus .env.example) vorher entfernen. Über
  # eine Temp-Datei und `cat >`, damit Rechte und Besitzer der .env bleiben.
  if [ -f "$DATEI" ]; then
    BEREINIGT="$(mktemp)"
    trap 'rm -f "$TMP" "$BEREINIGT"' EXIT
    # grep -v liefert Exit 1, wenn nichts übrig bleibt (leere Datei) - kein Fehler.
    grep -v -E "^${PRAEFIX}_VAPID_(PUBLIC_KEY|PRIVATE_KEY|SUBJECT)=[[:space:]]*$" "$DATEI" > "$BEREINIGT" || true
    cat "$BEREINIGT" > "$DATEI"
  fi
  # Eine Leerzeile davor, falls die Datei nicht mit einem Zeilenumbruch endet.
  if [ -s "$DATEI" ] && [ -n "$(tail -c 1 "$DATEI")" ]; then
    echo >> "$DATEI"
  fi
  printf '%s\n' "$ZEILEN" >> "$DATEI"
  echo "An $DATEI angehängt: ${PRAEFIX}_VAPID_PUBLIC_KEY, ${PRAEFIX}_VAPID_PRIVATE_KEY, ${PRAEFIX}_VAPID_SUBJECT."
  echo "Öffentlicher Schlüssel: ${OEFFENTLICH}"
else
  printf '%s\n' "$ZEILEN"
fi

#!/usr/bin/env bash
# Tägliche Sicherung der Prod-Datenbank. Läuft AUF DER VPS, angestoßen vom
# systemd-Timer (siehe deploy/systemd/, Einrichtung in docs/BACKUP.md).
#
# Ablauf: dumpen -> packen -> verschlüsseln -> lokal ablegen -> nach
# Cloudflare R2 kopieren.
#
# Drei Entscheidungen, die den Rest erklären:
#
# 1. Verschlüsselt wird ASYMMETRISCH. Auf der VPS liegt nur der öffentliche
#    Schlüssel. Der Server kann damit verschlüsseln, aber nichts entschlüsseln.
#    Wer den Server übernimmt, bekommt die Mitgliederdaten aus den Sicherungen
#    also nicht mit dazu. Der geheime Schlüssel liegt beim Entwickler (1Password).
#
# 2. Es wird nur `dogity_prod` gesichert. `dogity_test` besteht aus Demo-Daten,
#    die bei jedem Start neu erzeugt werden (DemoDataSeeder.cs) - dafür eine
#    Sicherung aufzubewahren wäre Ballast ohne Gegenwert.
#
# 3. Aufbewahrt wird nach Großvater-Vater-Sohn, umgesetzt über drei Präfixe
#    im Bucket (taeglich/, woechentlich/, monatlich/). Gelöscht wird NICHT von
#    hier aus, sondern von R2-Lifecycle-Regeln - die greifen auch dann noch,
#    wenn dieses Skript defekt ist oder gar nicht mehr läuft.
#
# Aufruf von Hand: `ssh dogity sudo -u dogity /opt/dogity/scripts/backup-db.sh`
set -euo pipefail

REPO_DIR="${REPO_DIR:-/opt/dogity}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/dogity}"
DATENBANK="${DATENBANK:-dogity_prod}"

# Empfänger des GPG-Schlüssels (siehe docs/BACKUP.md, Schritt 3).
GPG_EMPFAENGER="${GPG_EMPFAENGER:-backup@dogity.net}"

# rclone-Ziel in der Form <remote>:<bucket>. Leer lassen schaltet den
# Versand ab - dann bleibt es bei der lokalen Sicherung.
#
# Bewusst ${VAR-...} statt ${VAR:-...}: mit Doppelpunkt würde auch ein
# ausdrücklich leer gesetztes R2_ZIEL wieder auf die Vorgabe zurückfallen,
# und der Notausschalter wäre keiner.
R2_ZIEL="${R2_ZIEL-r2:dogity-backups}"

# Tage, die lokal vorgehalten werden. Die lange Aufbewahrung macht R2.
LOKALE_TAGE="${LOKALE_TAGE:-14}"

# Optionale Totmannschaltung (z.B. healthchecks.io, kostenloses Kontingent).
# Ohne gesetzten Wert passiert nichts - ein fehlgeschlagener Lauf ist dann
# nur in `systemctl status dogity-backup` und im Journal zu sehen.
HEALTHCHECK_URL="${HEALTHCHECK_URL:-}"

# --- Mengengerüst -----------------------------------------------------------
#
# Gemessen am 2026-09-09: dogity_prod ist 12 MB groß, der gepackte Dump
# 437 KB. Aufbewahrt werden 14 tägliche, 10 wöchentliche und 13 monatliche
# Stände, macht rund 16 MB im Bucket - 0,16 % des Freikontingents von 10 GB.
#
# Die folgenden Schwellen sind nicht dafür da, diesen Normalfall zu regeln,
# sondern für den Fall, dass etwas außer Kontrolle gerät: ein Timer, der in
# Schleife läuft, eine Datenbank, die unerwartet explodiert, liegengebliebene
# Teil-Uploads. R2 sperrt bei Überschreitung nicht, sondern rechnet ab -
# also muss die Bremse hier sitzen.
FREIKONTINGENT_BYTES=$((10 * 1000 * 1000 * 1000))
WARN_PROZENT="${WARN_PROZENT:-20}"   # ab hier: Lauf gilt als fehlgeschlagen
STOPP_PROZENT="${STOPP_PROZENT:-50}" # ab hier: kein Upload mehr

# Eine einzelne Sicherung, die größer ist als das, ist mit hoher
# Wahrscheinlichkeit ein Fehler und wird nicht hochgeladen.
MAX_DATEI_BYTES="${MAX_DATEI_BYTES:-524288000}" # 500 MB
# ... und eine, die kleiner ist als das, ist ein abgebrochener Dump.
MIN_DATEI_BYTES="${MIN_DATEI_BYTES:-5120}"      # 5 KB

meldung() { printf '%s  %s\n' "$(date +'%Y-%m-%d %H:%M:%S')" "$*"; }

fehler() {
  meldung "FEHLER: $*"
  [ -n "$HEALTHCHECK_URL" ] && curl -fsS -m 10 --retry 3 "$HEALTHCHECK_URL/fail" >/dev/null 2>&1 || true
  exit 1
}

cd "$REPO_DIR"
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

# Zwei Läufe gleichzeitig würden sich beim `docker compose exec` in die Quere
# kommen; flock hält den zweiten an, statt ihn scheitern zu lassen.
#
# Die Sperrdatei liegt bewusst im Sicherungsverzeichnis (Modus 700) und nicht
# in /tmp: dort könnte sie jeder andere Benutzer vorab anlegen und die Sperre
# halten - die Sicherung würde dann Tag für Tag stillschweigend übersprungen.
exec 9>"${BACKUP_DIR}/.lock"
flock -n 9 || { meldung "Ein anderer Lauf ist noch aktiv - dieser wird übersprungen."; exit 0; }

heute="$(date +%F)"
basis="dogity_prod_${heute}"
dump_datei="${BACKUP_DIR}/${basis}.sql.gz.gpg"
rollen_datei="${BACKUP_DIR}/${basis}_rollen.sql.gz.gpg"

# --- 1. Dump ----------------------------------------------------------------
#
# --no-owner/--no-privileges macht den Dump zwischen den Datenbanken
# übertragbar: die vierteljährliche Rückspielprobe geht nach dogity_test,
# wo es die Rolle dogity_prod gar nicht gibt. Die Rechte entstehen beim
# Zurückspielen dadurch, dass unter dem Ziel-Benutzer eingespielt wird.
#
# Kein --clean im Dump: eine Sicherung soll nichts löschen können. Das
# Aufräumen des Ziels macht restore-db.sh bewusst und mit Rückfrage.
meldung "==> Dump von ${DATENBANK}"
docker compose exec -T postgres pg_dump -U postgres --no-owner --no-privileges "$DATENBANK" \
  | gzip -9 \
  | gpg --batch --yes --no-tty --trust-model always \
        --encrypt --recipient "$GPG_EMPFAENGER" \
  > "$dump_datei" \
  || fehler "Dump fehlgeschlagen (siehe Meldungen oben)."

# Die Rollen liegen im Cluster, nicht in der Datenbank. Ohne sie lässt sich
# ein von Grund auf neu aufgesetzter Postgres nicht so herstellen, dass die
# Container ihn benutzen können. Wenige Kilobyte, deshalb bei jedem Lauf dabei.
meldung "==> Rollen des Clusters"
docker compose exec -T postgres pg_dumpall -U postgres --globals-only \
  | gzip -9 \
  | gpg --batch --yes --no-tty --trust-model always \
        --encrypt --recipient "$GPG_EMPFAENGER" \
  > "$rollen_datei" \
  || fehler "Rollen-Dump fehlgeschlagen."

chmod 600 "$dump_datei" "$rollen_datei"

groesse="$(stat -c %s "$dump_datei")"
meldung "    ${basis}.sql.gz.gpg: $((groesse / 1024)) KB"

# --- 2. Plausibilität -------------------------------------------------------
#
# `set -o pipefail` fängt einen abgestürzten pg_dump ab. Was es NICHT fängt:
# einen Dump, der sauber durchläuft, aber leer ist, weil die Datenbank aus
# irgendeinem Grund keine Tabellen mehr hat. Genau deshalb die Untergrenze.
[ "$groesse" -ge "$MIN_DATEI_BYTES" ] \
  || fehler "Sicherung ist nur ${groesse} Bytes groß - das kann nicht stimmen. Nichts hochgeladen."
[ "$groesse" -le "$MAX_DATEI_BYTES" ] \
  || fehler "Sicherung ist ${groesse} Bytes groß und überschreitet das Limit von ${MAX_DATEI_BYTES}. Nichts hochgeladen."

# --- 3. Lokal aufräumen -----------------------------------------------------
meldung "==> Lokale Aufbewahrung: ${LOKALE_TAGE} Tage"
find "$BACKUP_DIR" -maxdepth 1 -name 'dogity_prod_*.gpg' -type f -mtime "+${LOKALE_TAGE}" -print -delete

# --- 4. Nach R2 -------------------------------------------------------------
if [ -z "$R2_ZIEL" ]; then
  meldung "==> R2_ZIEL ist leer - Versand übersprungen, nur lokal gesichert."
else
  command -v rclone >/dev/null || fehler "rclone ist nicht installiert (siehe docs/BACKUP.md)."

  # Belegung VOR dem Hochladen messen. Ein Listenaufruf, also eine
  # Class-B-Operation von zehn Millionen freien im Monat.
  belegt="$(rclone size "$R2_ZIEL" --json 2>/dev/null | grep -o '"bytes":[0-9]*' | cut -d: -f2 || true)"
  [ -n "${belegt:-}" ] || fehler "Belegung des Buckets nicht messbar - zur Sicherheit nichts hochgeladen."

  anteil=$((belegt * 100 / FREIKONTINGENT_BYTES))
  meldung "==> Bucket belegt: $((belegt / 1024 / 1024)) MB (${anteil} % des Freikontingents)"

  if [ "$anteil" -ge "$STOPP_PROZENT" ]; then
    fehler "Bucket zu ${anteil} % voll (Grenze ${STOPP_PROZENT} %). Upload gestoppt, lokale Sicherung liegt in ${BACKUP_DIR}. Bitte Lifecycle-Regeln prüfen."
  fi

  # Zielpräfixe. Sonntag ist zusätzlich Wochenstand, der Erste zusätzlich
  # Monatsstand. Dieselbe Datei landet dann mehrfach - bei 437 KB belanglos,
  # dafür ist jeder Präfix für sich vollständig und die Lifecycle-Regeln
  # bleiben unabhängig voneinander.
  ziele=("taeglich")
  [ "$(date +%u)" = "7" ] && ziele+=("woechentlich")
  [ "$(date +%d)" = "01" ] && ziele+=("monatlich")

  for praefix in "${ziele[@]}"; do
    for datei in "$dump_datei" "$rollen_datei"; do
      # --s3-no-check-bucket: der API-Token darf genau diesen einen Bucket
      # beschreiben, aber keine Buckets auflisten oder anlegen.
      rclone copyto "$datei" "${R2_ZIEL}/${praefix}/$(basename "$datei")" \
        --s3-no-check-bucket --retries 3 \
        || fehler "Upload nach ${praefix}/ fehlgeschlagen."
    done
    meldung "    -> ${praefix}/"
  done

  if [ "$anteil" -ge "$WARN_PROZENT" ]; then
    meldung "==> Sicherung liegt in R2, ABER der Bucket ist zu ${anteil} % voll (Warnschwelle ${WARN_PROZENT} %)."
    meldung "    Lifecycle-Regeln prüfen, siehe docs/BACKUP.md."
    exit 1
  fi
fi

# --- 5. Spur hinterlassen ---------------------------------------------------
#
# Damit ein Blick ins Verzeichnis reicht, um zu sehen, ob und wann es
# zuletzt funktioniert hat - ohne Journal-Archäologie.
printf '%s  erfolgreich  %s KB  -> %s\n' \
  "$(date +'%Y-%m-%d %H:%M:%S')" "$((groesse / 1024))" "${ziele[*]:-nur lokal}" \
  > "${BACKUP_DIR}/LETZTER_LAUF"

[ -n "$HEALTHCHECK_URL" ] && curl -fsS -m 10 --retry 3 "$HEALTHCHECK_URL" >/dev/null 2>&1 || true

meldung "==> Fertig."

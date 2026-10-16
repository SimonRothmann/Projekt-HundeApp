#!/bin/bash
# Startet den Versand-Mailserver: OpenDKIM (signiert) + Postfix (verschickt).
#
# Die Konfiguration entsteht hier per postconf statt als main.cf im Repo: Sie
# hängt an Umgebungswerten (Hostname, Domain, Netz des Containers), und eine
# Stelle ist leichter zu prüfen als eine Vorlage plus Ersetzungen.
set -euo pipefail

: "${MAIL_HOSTNAME:?MAIL_HOSTNAME fehlt (z. B. mail.dogity.net)}"
: "${MAIL_DOMAIN:?MAIL_DOMAIN fehlt (z. B. dogity.net)}"
DKIM_SELECTOR="${DKIM_SELECTOR:-mail}"
KEYDIR=/var/lib/dkim

log() { echo "[mail] $*"; }

# --- DKIM-Schlüssel: einmal erzeugen, danach nur noch verwenden ------------
# Der private Schlüssel verlässt das Volume nie. Den öffentlichen Teil gibt
# das Skript bei JEDEM Start aus - so ist der DNS-Eintrag jederzeit im Log
# nachzulesen (docker compose logs mail | grep DKIM).
mkdir -p "$KEYDIR" /etc/opendkim
if [ ! -f "$KEYDIR/$DKIM_SELECTOR.private" ]; then
  log "Erzeuge neuen DKIM-Schlüssel (2048 Bit) für $MAIL_DOMAIN, Selektor $DKIM_SELECTOR"
  opendkim-genkey -b 2048 -d "$MAIL_DOMAIN" -s "$DKIM_SELECTOR" -D "$KEYDIR"
fi
chown -R opendkim:opendkim "$KEYDIR"
chmod 700 "$KEYDIR"
chmod 600 "$KEYDIR/$DKIM_SELECTOR.private"

# Die .txt von opendkim-genkey ist im Zonendatei-Format über mehrere Zeilen
# verteilt; für Cloudflare braucht es den Wert als eine Zeile.
DKIM_WERT=$(sed -e 's/^[^(]*(//' -e 's/).*$//' "$KEYDIR/$DKIM_SELECTOR.txt" | tr -d '\n\t' | sed -e 's/" *"//g' -e 's/"//g' -e 's/  */ /g' -e 's/^ //' -e 's/ $//')
log "DKIM-Eintrag für das DNS (TXT): Name ${DKIM_SELECTOR}._domainkey  Wert: ${DKIM_WERT}"

cat > /etc/opendkim/opendkim.conf <<EOF
# Erzeugt von entrypoint.sh - Änderungen dort, nicht hier.
Syslog                  no
UMask                   007
UserID                  opendkim:opendkim
Mode                    s
Domain                  ${MAIL_DOMAIN}
Selector                ${DKIM_SELECTOR}
KeyFile                 ${KEYDIR}/${DKIM_SELECTOR}.private
Socket                  inet:8891@127.0.0.1
Canonicalization        relaxed/simple
SignatureAlgorithm      rsa-sha256
# From doppelt signieren: Ein nachträglich hinzugefügtes zweites From würde
# die Signatur brechen, statt unbemerkt durchzugehen.
OversignHeaders         From
# Die von RFC 6376 empfohlene Liste plus List-Unsubscribe-Post: RFC 8058
# (Ein-Klick-Abmeldung) verlangt, dass beide List-Unsubscribe-Kopfzeilen
# von der DKIM-Signatur gedeckt sind - sonst ignorieren Gmail und Yahoo sie.
# Die Vorgabe von OpenDKIM kennt List-Unsubscribe-Post noch nicht.
SignHeaders             From,Reply-To,Subject,Date,To,Cc,Message-ID,MIME-Version,Content-Type,In-Reply-To,References,List-Id,List-Help,List-Unsubscribe,List-Unsubscribe-Post,List-Subscribe,List-Post,List-Owner,List-Archive
# Nur Mails aus dem eigenen Netz signieren (InternalHosts = 127.0.0.1 und das
# Compose-Netz, siehe unten).
InternalHosts           refile:/etc/opendkim/internal-hosts
EOF

# --- Netz: Nur das Compose-Netz darf Mails übergeben ------------------------
# Das eigene Netz des Containers (z. B. 172.18.0.0/16) - genau die Dienste aus
# docker-compose.yml. Kein Port ist nach außen veröffentlicht; das hier ist
# die zweite Sicherung, falls das je jemand ändert.
# Die Route "Netz dev eth0 scope link" nennt genau das Compose-Netz.
NETZ=$(ip route show dev eth0 | awk '/scope link/ {print $1; exit}')
if [ -z "$NETZ" ]; then
  log "FEHLER: Netz des Containers nicht ermittelbar (ip route show dev eth0)"
  exit 1
fi
log "Mails angenommen aus: 127.0.0.0/8 $NETZ (Absender nur @$MAIL_DOMAIN)"
printf '127.0.0.1\n%s\n' "$NETZ" > /etc/opendkim/internal-hosts

# --- Postfix ---------------------------------------------------------------
postconf -e \
  "compatibility_level = 3.6" \
  "myhostname = ${MAIL_HOSTNAME}" \
  "mydomain = ${MAIL_DOMAIN}" \
  "myorigin = ${MAIL_DOMAIN}" \
  "smtpd_banner = \$myhostname ESMTP" \
  "inet_interfaces = all" \
  "inet_protocols = ipv4" \
  "mydestination =" \
  "local_transport = error:Dieser Server stellt keine Mails zu" \
  "relay_domains =" \
  "alias_maps =" \
  "alias_database =" \
  "mynetworks = 127.0.0.0/8 ${NETZ}" \
  "smtpd_client_restrictions = permit_mynetworks, reject" \
  "smtpd_relay_restrictions = permit_mynetworks, reject" \
  "smtpd_recipient_restrictions = permit_mynetworks, reject" \
  "smtpd_sender_restrictions = reject_non_fqdn_sender, check_sender_access inline:{ ${MAIL_DOMAIN}=OK }, reject" \
  "disable_vrfy_command = yes" \
  "smtpd_tls_security_level = none" \
  "smtp_tls_security_level = may" \
  "smtp_tls_CAfile = /etc/ssl/certs/ca-certificates.crt" \
  "smtp_tls_loglevel = 1" \
  "smtp_tls_note_starttls_offer = yes" \
  "message_size_limit = 10240000" \
  "maximal_queue_lifetime = 2d" \
  "bounce_queue_lifetime = 2d" \
  "milter_protocol = 6" \
  "milter_default_action = tempfail" \
  "smtpd_milters = inet:127.0.0.1:8891" \
  "non_smtpd_milters = inet:127.0.0.1:8891" \
  "maillog_file = /dev/stdout"

postfix set-permissions >/dev/null 2>&1 || true
postfix check

# --- Starten und überwachen ------------------------------------------------
opendkim -f -x /etc/opendkim/opendkim.conf &
DKIM_PID=$!

postfix start-fg &
POSTFIX_PID=$!

stoppen() {
  log "Beende Postfix und OpenDKIM"
  postfix stop >/dev/null 2>&1 || true
  kill "$DKIM_PID" 2>/dev/null || true
  exit 0
}
trap stoppen TERM INT

log "Bereit: $MAIL_HOSTNAME verschickt für @$MAIL_DOMAIN"

# Endet einer der beiden, endet der Container - Docker startet ihn neu
# (restart: unless-stopped). Ohne OpenDKIM hielte Postfix sonst jede Mail an
# (milter_default_action = tempfail), und niemand merkte es.
wait -n "$DKIM_PID" "$POSTFIX_PID"
log "Ein Dienst ist beendet - Container stoppt"
stoppen

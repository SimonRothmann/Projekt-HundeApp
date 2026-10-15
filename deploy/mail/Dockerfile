# Eigener Versand-Mailserver (nur ausgehend): Postfix + OpenDKIM.
#
# Warum selbst gebaut statt eines fertigen Images: Das sind zwei Pakete aus
# den Alpine-Quellen und ein Startskript - nachvollziehbar in einer Datei, ohne
# einem fremden Image-Herausgeber vertrauen zu müssen. Warum überhaupt ein
# eigener Server statt eines Versanddienstes: kostenlos, Standort Deutschland
# (Contabo), kein Auftragsverarbeiter für Mails. Die Abwägung (Zustellbarkeit)
# steht in docs/BETRIEB.md "E-Mail".
#
# Der Container nimmt KEINE Verbindungen von außen an (kein veröffentlichter
# Port, siehe docker-compose.yml); nur die Dienste im Compose-Netz können ihm
# Mails übergeben, und nur mit Absender @${MAIL_DOMAIN}.
FROM alpine:3.22

# bash nur für "wait -n" im Startskript: Stirbt Postfix oder OpenDKIM, beendet
# sich der Container, und Docker startet ihn neu - statt still Mails zu
# stauen, weil die DKIM-Signatur fehlt.
RUN apk add --no-cache postfix opendkim opendkim-utils ca-certificates tzdata bash

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod 755 /usr/local/bin/entrypoint.sh

# Warteschlange und DKIM-Schlüssel liegen in Volumes (docker-compose.yml):
# Ein Neustart verliert weder wartende Mails noch den Schlüssel - ein neuer
# Schlüssel hieße, den DNS-Eintrag neu zu setzen.
VOLUME ["/var/spool/postfix", "/var/lib/dkim"]

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]

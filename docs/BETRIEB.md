# Betrieb der VPS

Was von selbst läuft, was von Hand bleibt, und woran man merkt, dass etwas
klemmt. Die Sicherung der Datenbank hat eine eigene Seite:
[BACKUP.md](BACKUP.md).

## Läuft automatisch

| Wann | Was | Nachsehen |
|---|---|---|
| täglich, früh | `unattended-upgrades` spielt Sicherheits- **und** Fehlerkorrektur-Updates ein | `systemctl status unattended-upgrades` |
| 03:00 täglich | Sicherung der Prod-Datenbank nach R2 | `cat /var/backups/dogity/LETZTER_LAUF` |
| 04:30, nur wenn nötig | Neustart, falls ein Kernel-Update darauf wartet | `uptime`, `uname -r` |
| 04:00 sonntags | Build-Cache auf 20 GB beschneiden, verwaiste Images entfernen | `docker system df` |
| laufend | Livepatch schließt Kernel-Lücken ohne Neustart (Ubuntu Pro, seit 2026-09-09) | `canonical-livepatch status` |

Die Konfiguration für Updates und Neustart steht in
`/etc/apt/apt.conf.d/52unattended-upgrades-lokal` - eine eigene Datei, damit
ein Paket-Update die Ubuntu-Vorgabe in `50unattended-upgrades` überschreiben
darf, ohne diese Einstellungen mitzunehmen.

## Bleibt von Hand: die Monatsrunde

Zwei Dinge werden bewusst nicht automatisiert, weil beide laufende Container
neu starten. Das gehört an einen Zeitpunkt, den man sich aussucht.

**1. Docker-Engine.** Ein Update startet den Daemon neu und damit alles:

```bash
ssh dogity 'sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin'
```

**Ein geänderter `deploy/caddy/Caddyfile` braucht keinen Handgriff mehr.**
Beide Deploy-Skripte rufen am Ende `scripts/caddy-neu-laden.sh` auf: Caddy
prüft die neue Datei und tauscht sie ohne Unterbrechung aus; ist sie
fehlerhaft, läuft er mit der alten weiter und der Deploy endet mit einer
Fehlermeldung. Das geht, weil der Ordner `deploy/caddy` eingebunden ist und
nicht die einzelne Datei - die hinge nach `git pull` an der alten Fassung.
Achtung: Der Caddyfile gilt für alle vier Domains, schon der Test-Deploy
bringt eine Änderung also auch auf Prod.

Von Hand, etwa nach einer Änderung direkt auf dem Server:

```bash
ssh dogity '/opt/dogity/scripts/caddy-neu-laden.sh'
```

**2. Postgres und Caddy nachziehen.** Die beiden fasst kein Deploy an - sie
sind fertige Images, und `docker compose up` benutzt ein lokal vorhandenes
Image ohne Rückfrage. Erst nach einer frischen Sicherung:

```bash
ssh dogity 'cd /opt/dogity && docker compose pull postgres caddy && docker compose up -d postgres caddy'
```

⚠️ **Das Tag muss auf `postgres:17` stehen bleiben.** 17.10 → 17.11 ist ein
Fehlerkorrektur-Update, das Datenverzeichnis bleibt lesbar. Ein Sprung auf
`postgres:18` macht das Volume unbrauchbar und verlangt Dump und Restore.

Danach kurz nachsehen, dass alles wieder oben ist:

```bash
ssh dogity 'cd /opt/dogity && docker compose ps && curl -sS -o /dev/null -w "prod: HTTP %{http_code}\n" https://dogity.net/'
```

## Basis-Images der Anwendung

`deploy-test.sh` baut mit `--pull`, `deploy-prod.sh` bewusst ohne. Damit
taucht ein neues .NET-, Node- oder Debian-Image immer zuerst auf Test auf
und wandert erst beim Promote nach Prod - dieselbe Zwei-Stufen-Logik wie
beim Anwendungscode.

Ohne `--pull` löst BuildKit `FROM …:9.0` beliebig lange gegen den lokalen
Cache auf. Die Laufzeitumgebung altert dann still, während der eigene Code
bei jedem Deploy frisch ist.

## Wenn etwas klemmt

**Läuft die Sicherung?**

```bash
ssh dogity 'cat /var/backups/dogity/LETZTER_LAUF; systemctl list-timers dogity-backup.timer --no-pager'
```

**Wächst die Platte?**

```bash
ssh dogity 'df -h /; docker system df'
```

Der Build-Cache ist mit Abstand der größte Posten - er war am 2026-09-09 auf
47 GB gewachsen, bei 58 GB Gesamtbelegung. Deshalb der wöchentliche Timer.

**Nach jedem Deploy: blockiert die CSP etwas?**

Die Content-Security-Policy ist seit 2026-09-09 durchsetzend, nicht mehr nur
meldend. Verstöße gehen weiterhin an `/csp-report` und landen im
Frontend-Log - aber ein Deploy erzeugt den Container neu und wirft sie weg.
Der Blick lohnt sich also in den Tagen NACH einem Deploy, nicht davor:

```bash
ssh dogity 'cd /opt/dogity && docker compose logs frontend-prod | grep CSP-Report | head -20'
```

Leer ist der Normalfall. Etwas gefunden heißt: der Browser hat eine
Ressource blockiert, die die Seite braucht - dann gehört die Regel in
`next.config.ts` angepasst und neu ausgerollt.

**Stehen Updates an, die niemand einspielt?**

```bash
ssh dogity 'apt list --upgradable 2>/dev/null | tail -n +2 | wc -l; test -f /var/run/reboot-required && cat /var/run/reboot-required || echo "kein Neustart nötig"'
```

## Einrichtung des Cache-Timers

Einmalig, nach dem ersten Deploy dieser Dateien:

```bash
ssh dogity 'sudo cp /opt/dogity/deploy/systemd/dogity-build-cache.* /etc/systemd/system/ && sudo systemctl daemon-reload && sudo systemctl enable --now dogity-build-cache.timer'
```

Der Cache wird dabei nicht sofort beschnitten - der Timer läuft erst am
Sonntag. Einmal von Hand anstoßen:

```bash
ssh dogity 'sudo systemctl start dogity-build-cache.service && docker system df'
```

## Grundeinrichtung der Maschine

Einmalig gesetzt, hier festgehalten, damit ein neu aufgesetzter Server
denselben Stand bekommt.

**Ubuntu Pro** (kostenlose Privatlizenz, bis zu fünf Rechner):

```bash
sudo pro attach <token>
```

Aktiviert `esm-apps`, `esm-infra` und `livepatch`. Nachkonfigurieren ist
nichts nötig - `unattended-upgrades` hat die beiden ESM-Herkünfte in seiner
Liste erlaubter Quellen ohnehin stehen (Ubuntu-Vorgabe), sie waren bis dahin
nur leer. `esm-apps` wirkt sofort und deckt `universe` ab, das regulär keine
zugesicherten Sicherheitsupdates bekommt (betrifft hier z.B. rclone).
`esm-infra` wird erst 2029 relevant, wenn der Standardsupport für 24.04
endet.

**Journal deckeln.** Ohne Vorgabe nimmt journald bis zu 10 % der Platte,
hier also rund 19 GB. Als eigene Datei, damit ein Paket-Update die
Ubuntu-Vorgabe ersetzen darf, ohne diese Einstellung mitzunehmen:

```bash
sudo install -d /etc/systemd/journald.conf.d
printf "[Journal]\nSystemMaxUse=500M\n" | sudo tee /etc/systemd/journald.conf.d/50-dogity.conf
sudo systemctl restart systemd-journald
```

**Swap.** Die Maschine hat 11 GB RAM und lief ohne Swap. Im Normalbetrieb
reicht das, aber der Next-Build ist der speicherhungrigste Schritt (siehe
deploy/README.md), und ohne Swap gibt es bei einer Spitze keinen Puffer -
der OOM-Killer sucht sich dann ein Opfer, und das kann Postgres sein.
2 GB als Versicherung:

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo "/swapfile none swap sw 0 0" | sudo tee -a /etc/fstab
```

Dazu `vm.swappiness=10` in `/etc/sysctl.d/99-dogity-swap.conf`: mit der
Vorgabe 60 lagert der Kernel auch ohne Not aus, was bei einer Datenbank
unnötig Tempo kostet. 10 heißt: Swap erst, wenn es eng wird - genau der
Zweck hier.

## E-Mail und Push aktivieren

Beides ist im Code fertig und wird allein über die `.env` auf dem Server
eingeschaltet - ohne Codewechsel. Solange nichts eingetragen ist, passiert
nichts Neues: Mails werden nur protokolliert, Push ist aus, die Glocke in der
App bleibt der einzige Weg.

Per Mail kommt nur Wichtiges: die Willkommensmail, die Mail zum Zurücksetzen
des Passworts, Einladungen (Verein, Gruppe, Mit-Trainer:in, Mitbesitz eines
Hundes) und die Entscheidung über eine Beitrittsanfrage (Verein, Gruppe). Alles
andere bleibt Glocke und Push. Wichtiges per Mail lässt sich je Person unter
Profil → Funktionen abschalten; Willkommens- und Passwort-Mail nicht.

**Abmelden ohne Anmeldung (seit 2026-10-16):** Bei der Registrierung wählt die
Person, ob sie diese Mails will (Häkchen, vorausgewählt). Jede Benachrichtigungs-
und die Willkommensmail enthält einen Abmelde-Link (`/email-abmelden?t=…`), der
ohne Anmeldung `EmailNotifications = false` setzt; die Passwort- und die Testmail
enthalten keinen. Das Token im Link ist eine DataProtection-Nutzlast - die
Schlüssel liegen im Volume `*-dp-keys`; gehen sie verloren, werden alte
Abmelde-Links ungültig (die Person stellt es dann unter Profil → Funktionen ein).

Bewusst **ohne** die Kopfzeilen `List-Unsubscribe`/`List-Unsubscribe-Post`
(RFC 8058, „Abbestellen“ im Mailprogramm): Mit ihnen sortierte Gmail die
Willkommensmail unter „Werbung“ (Test, 2026-10-16). Sie kennzeichnen Massen-
und Werbemail; für Mails zum eigenen Konto verlangt sie niemand. Sollte Dogity
je einen Newsletter verschicken, gehören sie dort hinein.

### E-Mail über den eigenen Versandserver

Entschieden 2026-10-15: kein Versanddienst, sondern ein eigener, reiner
Versandserver - kostenlos, Standort Deutschland (Contabo), kein
Auftragsverarbeiter für Mails. Brevo wurde verworfen. Ausweg, falls die
Zustellung trotz allem schlecht bleibt: ein externer SMTP-Relay (etwa
Lettermint, NL, 300 Mails/Monat frei) - nur `.env`, kein Code.

**Aufbau:** Container `mail` (`deploy/mail`: Postfix + OpenDKIM auf Alpine).
Er nimmt Mails NUR aus dem Compose-Netz an (kein veröffentlichter Port), nur
mit Absender `@dogity.net`, verschickt nur über IPv4 und signiert jede Mail
mit DKIM. Warteschlange und DKIM-Schlüssel liegen in den Volumes `mail-spool`
und `mail-dkim`. Das Backend übergibt ohne Anmeldung an `mail:25`. Absender ist
`noreply@dogity.net`, Antworten gehen an `info@dogity.net`.

**Empfang** läuft getrennt davon über Cloudflare Email Routing:
`info@dogity.net` → Postfach des Betreibers. Die MX-Einträge gehören
Cloudflare und bleiben unverändert.

**Einrichtung, in dieser Reihenfolge:**

1. **DNS bei Cloudflare** (`dogity.net` → DNS → Records):
   - `A`-Eintrag `mail` → `161.97.135.174`, **"DNS only"** (graue Wolke).
     Mails können nicht durch den Cloudflare-Proxy.
   - Den bestehenden SPF-Eintrag (TXT auf `@`) **ändern**, keinen zweiten
     anlegen - es darf nur einen geben:
     `v=spf1 ip4:161.97.135.174 include:_spf.mx.cloudflare.net ~all`
     (der Cloudflare-Teil gehört zur Weiterleitung von info@).
   - DMARC: TXT `_dmarc` → `v=DMARC1; p=none; rua=mailto:info@dogity.net; fo=1`.
     Nach der Testphase auf `p=quarantine` stellen.
2. **Reverse DNS bei Contabo** (Kundenbereich → Reverse DNS) für
   `161.97.135.174`: `mail.dogity.net`. Viele Empfänger prüfen, dass IP, Name
   und HELO zusammenpassen; ohne das landen Mails im Spam.
3. **Deploy** (Test genügt): Die Deploy-Skripte bauen und starten den Dienst
   `mail` mit. Beim allerersten Mal, falls das laufende Skript noch die alte
   Fassung las: `docker compose up -d --build mail` von Hand.
4. **DKIM-Eintrag setzen.** Den Schlüssel erzeugt der Container beim ersten
   Start selbst; der private Teil verlässt das Volume nie. Den DNS-Wert zeigt
   jedes Startprotokoll:

   ```bash
   docker compose logs mail | grep "DKIM-Eintrag"
   ```

   In Cloudflare: TXT, Name `mail._domainkey`, Inhalt = der ausgegebene Wert
   (`v=DKIM1; h=sha256; k=rsa; p=…`).
5. **Testphase auf Test:** in der Server-`.env` `TEST_SMTP_HOST=mail`, Test
   neu deployen, dann im Adminbereich "Testmail senden" an eigene Adressen
   (Gmail, GMX/Web.de, Outlook, iCloud, T-Online) und an eine Adresse von
   mail-tester.com (Ziel: 10/10). Prüfen: Posteingang oder Spam? In den
   Kopfzeilen `spf=pass`, `dkim=pass`, `dmarc=pass`?
6. **T-Online freischalten lassen.** T-Online lehnt unbekannte Server ab
   (Antwort `554 … None/bad reputation … (NOWL)`, gemessen 2026-10-15). Eine
   kurze Mail an `tobr@rx.t-online.de` mit IP, Hostname `mail.dogity.net` und
   Verweis auf das Impressum unter https://dogity.net/impressum - T-Online
   verlangt ein erreichbares Impressum auf der Domain.
7. **Prod einschalten:** `SMTP_HOST=mail` in die Server-`.env`, Prod deployen.
   Danach `TEST_SMTP_HOST` wieder leeren, wenn Test nicht mehr senden soll.

**Laufend im Blick behalten:** Sperrlisten (z. B. mxtoolbox.com
"Blacklist Check" für die IP), DMARC-Berichte im info@-Postfach, die
Warteschlange (`docker compose exec mail mailq`). Hängende Mails stehen mit
Grund im Log (`docker compose logs mail | grep -i "status=deferred\|status=bounced"`).

**Sicherung:** Das Volume `mail-dkim` ist nicht im Datenbank-Backup. Geht es
verloren, entsteht beim nächsten Start ein neuer Schlüssel - dann den
DKIM-Eintrag in Cloudflare neu setzen (Schritt 4). Mehr nicht.

### Push-Benachrichtigungen

Web Push mit VAPID: Jede Meldung der Glocke geht zusätzlich an die Geräte, die
Push eingeschaltet haben (Profil → Funktionen → "Push-Benachrichtigungen auf
diesem Gerät", je Gerät).

1. **Schlüsselpaar erzeugen** - lokal, nur mit `openssl`:

   ```bash
   scripts/vapid-schluessel.sh PROD .env      # oder TEST
   ```

   hängt `PROD_VAPID_PUBLIC_KEY`, `PROD_VAPID_PRIVATE_KEY` und
   `PROD_VAPID_SUBJECT` an die angegebene Datei an; auf dem Bildschirm erscheint
   nur der öffentliche Schlüssel. Wer die Zeilen sehen will, lässt die Datei weg
   (`scripts/vapid-schluessel.sh PROD >> .env` tut dasselbe). Der Kontakt
   (`mailto:info@dogity.net`) ist änderbar mit `VAPID_SUBJECT=mailto:… `
   vor dem Aufruf.
2. **Auf den Server:** die drei Zeilen in die `.env` dort übernehmen. Prod und
   Test bekommen bewusst verschiedene Paare.
3. **Ein einmal erzeugtes Paar nie ersetzen.** Mit einem neuen Schlüssel sind
   alle angemeldeten Geräte ungültig; jede Person müsste Push neu einschalten.
   Das Skript weigert sich deshalb, in eine Datei zu schreiben, in der der
   private Schlüssel schon einen Wert hat. Leere Platzhalter aus `.env.example`
   (`PROD_VAPID_PRIVATE_KEY=`) ersetzt es. Die Ausgabe nach stdout
   (`… >> .env`) prüft nichts - dort vorher selbst nachsehen.

Auf dem iPhone/iPad funktioniert Web Push nur, wenn Dogity **zum
Home-Bildschirm hinzugefügt** ist (Teilen → "Zum Home-Bildschirm"; iOS/iPadOS
16.4 oder neuer, auch in der EU). Im normalen Safari-Tab gibt es keine
Push-Nachrichten; die App zeigt dort einen entsprechenden Hinweis statt des
Knopfes. Die Nachricht ist Ende-zu-Ende verschlüsselt (RFC 8291): Apple, Google
und Mozilla leiten sie nur weiter und können sie nicht lesen. Ein Gerät, das der
Push-Dienst nicht mehr kennt (App gelöscht, Erlaubnis entzogen), wird beim
nächsten Versand automatisch vergessen.

### Nach dem Einschalten prüfen

```bash
curl -s https://<PROD_API_DOMAIN>/api/system/features
# {"email":true,"pushPublicKey":"B…"}   - "email":false / null = nicht eingerichtet
```

Die Antwort nennt nur Wahrheitswerte und den öffentlichen Schlüssel, keine
Geheimnisse. Danach:

- **Mail:** "Passwort vergessen" mit dem eigenen Konto anfordern; die Mail
  sollte in Minuten da sein (sonst `docker compose logs mail`). Der Link
  beginnt mit `https://<PROD_DOMAIN>/reset-password`.
- **Push:** auf dem Handy Dogity zum Home-Bildschirm hinzufügen, anmelden,
  Profil → Funktionen → "Einschalten"; dann lässt man sich von einem zweiten
  Konto zum Mitbesitzer eines Hundes einladen - die Einladung kommt als Push.
- Fehler beim Versand stehen ohne Inhalt im Backend-Log
  (`docker compose logs backend-prod | grep -i "versand\|push"`).

## Belastbarkeit (Lasttest 2026-10-16)

Gemessen lokal mit einem Release-Build im Production-Modus gegen eine eigene
Datenbank (500 Konten mit Hund, Ziel und 5.000 Trainings). Jede simulierte
Sitzung lädt Startseite und Hundeseite, speichert in 30 % der Runden ein
Training und in 5 % eine Fährte mit 400 Punkten, sonst die Statistik, und
macht dazwischen Pausen von 2-8 Sekunden wie ein Mensch.

| Gleichzeitige Sitzungen | Anfragen/s | Fehler | Startseite p95 | Fährte speichern p95 |
|---|---|---|---|---|
| 500 | ~470 | 0 | 28 ms | 251 ms |
| 1.000 | ~1.010 | 0 | 25 ms | 55 ms |

Bei 1.000 Sitzungen brauchte das Backend knapp 3 Kerne und 330 MB, Postgres
gut einen Kern. Die VPS hat 6 vCPU und 12 GB - Luft ist da, auch wenn ein
VPS-Kern langsamer ist als einer am Entwicklungsrechner.

Zwei Funde sind behoben:
- **Jede SQL-Abfrage landete im Protokoll** (EF-Core-Kategorie
  `Database.Command` auf Information): 367 MB in fünf Minuten. Bei der
  Obergrenze von 30 MB je Dienst wären die Protokolle nach Sekunden
  überschrieben worden und hätten im Fehlerfall nichts mehr gezeigt. Steht
  jetzt in `appsettings.json` auf Warning; lokal zeigt eine eigene
  `appsettings.Development.json` die Abfragen weiter.
- **Die Verbindungsvorräte waren unbegrenzt** (Npgsql-Vorgabe 100 je
  Backend gegen `max_connections = 100` der gemeinsamen Postgres). Jetzt
  `Maximum Pool Size` 50 (Prod) und 20 (Test) in `docker-compose.yml`; der
  Wiederholungslauf mit 50 Verbindungen lief ohne Fehler.

Was offen bleibt, steht in `TODO.md` unter Performance.

**Speicher der Datenbank.** Seit 2026-10-16 bekommt Postgres 2 GB Puffer
(`shared_buffers`) statt 128 MB, dazu mehr Arbeitsspeicher je Sortierung
und für Wartung - im ungünstigsten Fall (alle 70 Verbindungen sortieren
gleichzeitig) zusammen rund 4 GB, im Alltag deutlich weniger. Die Werte stehen beim Dienst
`postgres` in `docker-compose.yml`. Die Puffer füllen sich erst mit den
Daten - bei damals 12 bzw. 14 MB pro Datenbank zeigt `docker stats` also
weiterhin wenig, das ist kein Fehler.

Geänderte Werte greifen erst nach einem Neustart der Datenbank - Prod und
Test sind dabei rund eine halbe Minute weg. Die Deploy-Skripte starten
Postgres nicht gezielt neu. Weil die Backends von Postgres abhängen, kann
Compose die Datenbank beim Deploy aber mit neu erstellen, sobald sich ihre
Einstellungen geändert haben - dann behält das jeweils andere Backend tote
Verbindungen, und seine ersten Anfragen scheitern daran. Deshalb nach einer
Änderung an diesen Werten: Test wie gewohnt deployen (das bringt die neue
`docker-compose.yml` auf den Server), danach einmal:

```bash
ssh dogity 'cd /opt/dogity && docker compose up -d --wait postgres && docker compose restart backend-prod backend-test'
```

Prüfen: `SHOW shared_buffers;` liefert `2GB`.

## Bewusst offen

**SSH-Passwort-Anmeldung.** Steht auf `yes`, Port 22 ist weltweit offen.
Bewusst so belassen (Stand 2026-09-09), weil die Lage nachgemessen
entspannter ist, als sie klingt: fail2ban läuft mit `sshd`-Jail und hatte
zu dem Zeitpunkt 551 Sperren im Log, Root-Login ist aus, die Firewall lässt
nur 22/80/443 durch - und es hat nie eine erfolgreiche Anmeldung per
Passwort gegeben, alle liefen über `publickey`. Der Druck ist real (rund
4.000 Fehlversuche am Tag), läuft aber ins Leere.

Wer es doch abschaltet: vorher sichergehen, dass der Schlüssel greift, und
die bestehende Sitzung offen lassen.

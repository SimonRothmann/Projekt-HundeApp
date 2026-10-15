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

### E-Mail über Brevo

Stand der Recherche Oktober 2026; Brevo ändert gelegentlich Menünamen. Der Code
spricht nur SMTP (STARTTLS), jeder andere Relay geht genauso.

Warum Brevo: kostenloser Tarif mit 300 Mails am Tag (hier reichen einige
wenige), Sitz in Paris, Server in der EU, SMTP-Relay ohne eigene Software. Bei
300 Mails am Tag ist Schluss - weitere weist Brevo ab. Das Backend protokolliert
es (ohne Inhalt), verwirft die Mail und lässt die Glocke unberührt.

1. **Konto anlegen** bei Brevo (EU-Konto) und den Auftragsverarbeitungsvertrag
   abschließen: Brevo stellt ihn als Data Processing Agreement in den
   Kontoeinstellungen bereit - herunterladen, ablegen und in
   `VERARBEITUNGSVERZEICHNIS.md` (Abschnitt 3, "Offen") als erledigt eintragen.
2. **Domain `dogity.net` authentifizieren** (Brevo: Senders, Domains & Dedicated
   IPs → Domains → Domain hinzufügen). Brevo zeigt die Einträge an; sie kommen
   in das DNS bei Cloudflare:
   - **Brevo-Code** als TXT-Eintrag auf `dogity.net` (`brevo-code:…`),
   - **DKIM**: die CNAME-Einträge, die Brevo nennt (`brevo1._domainkey`,
     `brevo2._domainkey`) - in Cloudflare auf **"Nur DNS"** (graue Wolke), sonst
     findet Brevo sie nicht,
   - **DMARC** als TXT auf `_dmarc`, zum Anfang nur beobachtend:
     `v=DMARC1; p=none; rua=mailto:hallo@dogity.net`. Gmail und Yahoo verlangen
     für Absender wie uns mindestens diesen Eintrag.
3. **Genau ein SPF-Eintrag.** Eine Domain darf nur *einen* TXT-Eintrag mit
   `v=spf1` haben - zwei machen beide ungültig. Läuft gleichzeitig Cloudflare
   Email Routing (siehe unten), werden beide Anbieter in einem Eintrag
   zusammengeführt:

   ```
   v=spf1 include:_spf.mx.cloudflare.net include:spf.brevo.com ~all
   ```

   Ohne Email Routing genügt `v=spf1 include:spf.brevo.com ~all`.
4. **SMTP-Schlüssel** in Brevo erzeugen (SMTP & API → SMTP). Der **SMTP-Login** ist
   die dort angezeigte Kennung (meist die Konto-Mailadresse), das Passwort ist
   der **SMTP-Schlüssel** - nicht der API-Schlüssel.
5. **Absenderadresse** `hallo@dogity.net` als Absender in Brevo anlegen (sie
   muss zur authentifizierten Domain gehören).
6. **Antwortadresse über Cloudflare Email Routing:** In Cloudflare unter
   `dogity.net` → E-Mail → Email Routing die Adresse `hallo@dogity.net` anlegen
   und an das private Postfach weiterleiten (Zielpostfach einmal bestätigen).
   Das ist die Adresse hinter "Antworte einfach auf diese Mail" in der
   Willkommensmail. Email Routing setzt MX-Einträge und - falls Cloudflare es
   anbietet - einen SPF-Eintrag: den mit Schritt 3 zusammenführen.
7. **`.env` auf dem Server** ergänzen (Namen siehe `.env.example`, Werte
   nirgends einchecken). **Voraussetzung: der AV-Vertrag aus Schritt 1 ist
   abgeschlossen** - die Datenschutzerklärung sagt, dass der Versand erst dann
   beginnt. Mit dem Eintrag von `SMTP_HOST` ist der Versand aktiv:

   | Variable | Wert |
   |---|---|
   | `SMTP_HOST` | `smtp-relay.brevo.com` |
   | `SMTP_PORT` | `587` |
   | `SMTP_USERNAME` | SMTP-Login aus Schritt 4 |
   | `SMTP_PASSWORD` | SMTP-Schlüssel aus Schritt 4 |
   | `SMTP_FROM_ADDRESS` | `hallo@dogity.net` |
   | `SMTP_FROM_NAME` | `Dogity` |
   | `SMTP_REPLY_TO_ADDRESS` | `hallo@dogity.net` |

   `docker-compose.yml` reicht sie als `Email__Smtp__Host` usw. an `backend-prod`
   durch. Nur Prod versendet; die Testumgebung hat keinen Mailversand, ihre
   Demo-Konten haben keine echten Adressen.

Ist der Host gesetzt, ändert sich auch die Seite "Passwort vergessen": Sie
verspricht dann einen Link per Mail statt "der Betreiber meldet sich", und die
Benachrichtigung an die Admins ("möchte das Passwort zurücksetzen") entfällt.
Ohne Host bleibt es beim bisherigen Weg.

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
   (`mailto:hallo@dogity.net`) ist änderbar mit `VAPID_SUBJECT=mailto:… `
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
  sollte in Minuten da sein (sonst Brevo → Logs → Transactional). Der Link
  beginnt mit `https://<PROD_DOMAIN>/reset-password`.
- **Push:** auf dem Handy Dogity zum Home-Bildschirm hinzufügen, anmelden,
  Profil → Funktionen → "Einschalten"; dann lässt man sich von einem zweiten
  Konto zum Mitbesitzer eines Hundes einladen - die Einladung kommt als Push.
- Fehler beim Versand stehen ohne Inhalt im Backend-Log
  (`docker compose logs backend-prod | grep -i "versand\|push"`).

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

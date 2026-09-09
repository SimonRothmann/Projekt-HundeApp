# Sicherung der Datenbank

Täglich um 03:00 wird `dogity_prod` gedumpt, verschlüsselt, lokal abgelegt
und nach Cloudflare R2 kopiert. Kosten: keine.

- `scripts/backup-db.sh` – läuft auf der VPS, angestoßen vom systemd-Timer
- `scripts/restore-db.sh` – läuft auf dem Entwicklungsrechner
- `deploy/systemd/` – Dienst und Timer

## Warum das ins Freikontingent passt

Gemessen am 2026-09-09: `dogity_prod` ist 12 MB groß, der gepackte und
verschlüsselte Dump **437 KB**, der Rollen-Dump 687 Bytes.

Aufbewahrt werden 14 tägliche, 10 wöchentliche und 13 monatliche Stände –
zusammen **rund 16 MB**. Das sind 0,16 % der 10 GB. Selbst wenn die Daten um
das Hundertfache wachsen, bleibt es unter einem Fünftel.

R2 sperrt bei Überschreitung nicht, sondern rechnet ab (0,015 $ je GB und
Monat). Die Bremse muss also woanders sitzen. Sechs Sperren, die
unabhängig voneinander greifen:

| Sperre | Wo | Wirkt gegen |
|---|---|---|
| Lifecycle-Regeln je Präfix | R2 | Anhäufung über die Zeit – **greift auch dann, wenn das Skript defekt ist oder gar nicht mehr läuft** |
| Abbruch liegengebliebener Teil-Uploads | R2 | angefangene Uploads, die mitzählen, aber in keiner Dateiliste auftauchen |
| Harter Stopp bei 50 % Belegung | Skript, vor **jedem** Upload | Ausreißer jeder Art – lokal wird weiter gesichert, nur der Versand entfällt |
| Warnung ab 20 % Belegung | Skript | Frühwarnung; der Lauf gilt dann als fehlgeschlagen und fällt in `systemctl status` auf |
| Obergrenze 500 MB je Datei | Skript | ein einzelner Dump, der aus dem Ruder läuft |
| Untergrenze 5 KB je Datei | Skript | ein Dump, der sauber durchläuft, aber leer ist |

Dazu: der API-Token darf ausschließlich in diesen einen Bucket schreiben.

## Einrichtung

### 1. Schlüsselpaar anlegen (auf dem Entwicklungsrechner)

Verschlüsselt wird asymmetrisch. Auf der VPS liegt nur der öffentliche
Schlüssel – der Server kann damit verschlüsseln, aber nichts entschlüsseln.
Wer den Server übernimmt, bekommt die Mitgliederdaten aus den Sicherungen
also nicht mit dazu.

```bash
gpg --quick-generate-key "Dogity Backup <backup@dogity.net>" default default never
```

Passphrase vergeben. Danach prüfen, dass ein Unterschlüssel mit `[E]`
(encrypt) dabei ist:

```bash
gpg --list-keys backup@dogity.net
```

**Jetzt sofort den geheimen Schlüssel sichern**, sonst ist jede spätere
Sicherung wertlos:

```bash
gpg --armor --export-secret-keys backup@dogity.net
```

Die Ausgabe zusammen mit der Passphrase in 1Password ablegen. Das ist der
einzige Punkt in diesem Aufbau, der nicht reparierbar ist.

### 2. Öffentlichen Schlüssel auf die VPS

```bash
gpg --armor --export backup@dogity.net | ssh dogity 'gpg --import'
```

### 3. R2-Bucket anlegen

`dogity.net` liegt schon auf Cloudflare, das Konto besteht also. Im
Dashboard unter **R2 Object Storage**; falls R2 noch nicht aktiv ist, einmal
aktivieren (Cloudflare will dafür eine Zahlungsmethode hinterlegt haben,
obwohl im Freikontingent nichts abgerechnet wird).

**Create bucket**:

- Name: `dogity-backups`
- Location: **Specify jurisdiction → EU**

Die Jurisdiktion lässt sich **nach dem Anlegen nicht mehr ändern**. Da in den
Sicherungen personenbezogene Daten von Vereinsmitgliedern stecken, hier nicht
durchklicken – EU auswählen.

### 4. API-Token

R2 → **Manage API Tokens** → **Create API token**:

- Permission: **Object Read & Write**
- **Apply to specific buckets** → `dogity-backups`
- TTL: unbegrenzt

Cloudflare zeigt danach **Access Key ID**, **Secret Access Key** und den
Endpunkt. Das Secret erscheint nur ein einziges Mal – gleich nach 1Password.

Wichtig: durch die EU-Jurisdiktion lautet der Endpunkt
`https://<ACCOUNT_ID>.eu.r2.cloudflarestorage.com` – mit `.eu.` Ohne das
findet rclone den Bucket nicht.

R2 kennt keine Berechtigung „schreiben, aber nicht löschen". Ein
übernommener Server könnte die Kopien also löschen. Deshalb die lokale
Aufbewahrung zusätzlich – und deshalb der Abzug auf den eigenen Rechner
(unten) als dritte Stufe, wenn es dir wichtig ist.

### 5. Lifecycle-Regeln – das ist die eigentliche Obergrenze

Bucket → **Settings** → **Object Lifecycle Rules** → **Add rule**, vier Mal:

| Regel | Präfix | Aktion |
|---|---|---|
| `taeglich` | `taeglich/` | Delete objects after **14** days |
| `woechentlich` | `woechentlich/` | Delete objects after **70** days |
| `monatlich` | `monatlich/` | Delete objects after **400** days |
| `teil-uploads` | *(leer, ganzer Bucket)* | Abort incomplete multipart uploads after **1** day |

Die vierte Regel wird gern vergessen: abgebrochene Uploads liegen unsichtbar
im Bucket und zählen trotzdem gegen die 10 GB.

### 6. rclone auf der VPS

Aus dem Ubuntu-Repo, damit `unattended-upgrades` es mitpflegt:

```bash
ssh dogity 'sudo apt-get install -y rclone'
```

Dann als Benutzer `dogity` einrichten. **Mit einem Leerzeichen am Anfang**
eintippen – auf der VPS steht `HISTCONTROL=ignoreboth`, dadurch landet die
Zeile mit dem Secret nicht in der Shell-Historie:

```
 rclone config create r2 s3 provider Cloudflare region auto acl private access_key_id <ACCESS_KEY_ID> secret_access_key <SECRET> endpoint https://<ACCOUNT_ID>.eu.r2.cloudflarestorage.com
```

Probe:

```bash
ssh dogity 'rclone lsd r2:'
```

### 7. Verzeichnis, Konfiguration, Timer

```bash
ssh dogity 'sudo install -d -m 700 -o dogity -g dogity /var/backups/dogity'
```

```bash
ssh dogity 'printf "GPG_EMPFAENGER=backup@dogity.net\nR2_ZIEL=r2:dogity-backups\n" | sudo tee /etc/dogity-backup.env'
```

```bash
ssh dogity 'sudo cp /opt/dogity/deploy/systemd/dogity-backup.* /etc/systemd/system/ && sudo systemctl daemon-reload && sudo systemctl enable --now dogity-backup.timer'
```

### 8. Erster Lauf

```bash
ssh dogity 'sudo systemctl start dogity-backup.service; journalctl -u dogity-backup -n 40 --no-pager'
```

Danach muss im Bucket etwas liegen:

```bash
ssh dogity 'rclone ls r2:dogity-backups && rclone size r2:dogity-backups'
```

Erwartung nach dem ersten Lauf: zwei Objekte unter `taeglich/`, zusammen
gut 400 KB.

## Wiederherstellen

Läuft auf dem Entwicklungsrechner, weil nur dort der geheime Schlüssel
liegt. Entschlüsselt wird lokal, der Klartext läuft durch die
SSH-Verbindung direkt in `psql` – der geheime Schlüssel betritt die VPS
nie, und es liegt zu keinem Zeitpunkt eine unverschlüsselte Kopie auf
einer Platte.

Dafür braucht der Entwicklungsrechner ebenfalls rclone mit demselben
`r2`-Remote (`brew install rclone`, dann Schritt 6 dort wiederholen).

```bash
./scripts/restore-db.sh r2:dogity-backups/taeglich/dogity_prod_2026-09-09.sql.gz.gpg
```

Ohne zweites Argument geht es nach `dogity_test`. Für Prod muss der
Datenbankname von Hand eingetippt werden – ein „j" reicht nicht.

## Vierteljährlich: Rückspielprobe

Eine Sicherung, die nie zurückgespielt wurde, ist eine Vermutung. Der
Aufbau hier macht die Probe billig, weil es mit `dogity_test` eine
Umgebung gibt, in der nichts kaputtgehen kann:

1. `./scripts/restore-db.sh r2:dogity-backups/monatlich/<neuester Stand>`
2. Die Gegenprobe am Ende zeigt Tabellenzahl und Zeilenzahlen – plausibel?
3. `ssh dogity 'cd /opt/dogity && docker compose restart backend-test'`
4. Auf test.dogity.net anmelden und schauen, ob die Daten stimmen
5. Danach wieder Demo-Daten: `docker compose up -d --force-recreate backend-test`
   (der `DemoDataSeeder` füllt beim Start neu auf)

Nächster Termin jeweils in `TODO.md` notieren.

## Wenn etwas nicht stimmt

**Der Timer läuft, aber es kommt nichts an.** `systemctl status
dogity-backup` und `journalctl -u dogity-backup -n 50`. Das Skript endet
absichtlich mit Fehler, wenn der Bucket über 20 % voll ist – dann sind
wahrscheinlich die Lifecycle-Regeln nicht aktiv.

**Schneller Blick, ob überhaupt etwas passiert:**

```bash
ssh dogity 'cat /var/backups/dogity/LETZTER_LAUF; ls -lh /var/backups/dogity'
```

**Aktive Benachrichtigung** gibt es bewusst nicht – ein fehlgeschlagener
Timer ist sonst still. Wer das will: healthchecks.io hat ein kostenloses
Kontingent; die URL in `/etc/dogity-backup.env` als `HEALTHCHECK_URL`
eintragen, das Skript meldet sich dann bei jedem Lauf.

## Dritte Stufe: Abzug auf den eigenen Rechner

Optional, kostet nichts, und deckt die eine Lücke von R2 ab – dass die
Zugangsdaten auf dem Server liegen und ein übernommener Server die Kopien
löschen könnte. Da der Abzug **ziehend** läuft, braucht die VPS dafür
keinerlei Zugangsdaten:

```bash
rsync -av dogity:/var/backups/dogity/ ~/Documents/Dogity-Backups/
```

Bei 437 KB je Stand darf das ruhig gelegentlich von Hand passieren.
Time Machine nimmt es danach mit.

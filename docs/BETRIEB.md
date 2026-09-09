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

## Bewusst offen

**Ubuntu Pro / Livepatch.** Auf der Maschine verfügbar, nicht verbunden.
Kostenlos für bis zu fünf private Rechner. Livepatch schließt
Kernel-Sicherheitslücken im laufenden Betrieb, ohne Neustart - für einen
Einzelserver ohne Redundanz der wirksamste einzelne Hebel. `sudo pro attach
<token>`, dann `sudo pro enable livepatch`.

**SSH-Passwort-Anmeldung.** Steht auf `yes`, Port 22 ist weltweit offen.
Root-Login ist aus, die Firewall lässt nur 22/80/443 durch. Vor dem
Abschalten sichergehen, dass der Schlüssel greift, und die bestehende
Sitzung offen lassen.

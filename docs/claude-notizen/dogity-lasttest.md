---
name: dogity-lasttest
description: Dogity-Lasttest 2026-10-16 - 1.000 gleichzeitige Sitzungen fehlerfrei; behoben SQL-Log + Pool; PG-Speicher 2GB (355bd09); offen Wetter, Login-Drossel; Vorgehen lokal
metadata:
  type: project
---

Betreiber will sicher sein, dass viele gleichzeitige Nutzer:innen gehen (16.10.). Lokal gemessen (Release, Production-Modus, eigene DB mit 500 Konten/5.000 Trainings, Node-Skript mit selbst signierten HS256-JWTs, Sitzungen mit Denkpausen): 500 → ~470 req/s, 1.000 → ~1.010 req/s, 0 Fehler, Startseite p95 < 30 ms; Backend ~3 Kerne/330 MB, Postgres ~1 Kern. Ergebnisse in docs/BETRIEB.md „Belastbarkeit“.

Behoben (27227f3): EF `Database.Command` auf Warning in appsettings.json (vorher 367 MB Log in 5 min); `Maximum Pool Size` 50 Prod / 20 Test in docker-compose.yml (max_connections 100 geteilt). appsettings.Development.json ist gitignored und nicht im Image - Test bekommt also auch Warning.

Postgres-Speicher auf Betreiberwunsch ("2-4 GB") erhöht: 355bd09, shared_buffers 2GB/effective_cache_size 6GB/work_mem 16MB/maintenance 256MB/shm 512mb per `command:` in docker-compose.yml. Deploy-Skripte starten PG nicht gezielt neu - nach Test-Deploy `docker compose up -d --wait postgres && docker compose restart backend-prod backend-test` (BETRIEB.md); Prüfung `SHOW shared_buffers` = 2GB. DBs damals 12/14 MB.

Offen in TODO.md (Performance): Fährte-Speichern wartet auf Open-Meteo (6 s), Login-Drossel 10/min je IP (Vereins-WLAN). Gegen Test/Prod NICHT ohne ausdrückliches OK lasttesten.

**How to apply:** Für einen erneuten Lauf: sauberer git worktree + `dotnet publish`, eigene DB, Backend auf Port ≠ 5080, danach Worktree/DB/Secret wegräumen. Siehe [[dogity-lokale-verifikation]].

---
name: dogity-lokale-verifikation
description: Rezept, um Dogity lokal end-to-end zu prüfen (Postgres, Backend-Port 5080, next start statt next dev)
metadata:
  type: project
---

Dogity lokal zum Laufen bringen, um Änderungen wirklich durchzuklicken:

1. Postgres ohne Docker: `initdb`/`pg_ctl` mit `LC_ALL=C` (sonst
   „Postmaster ist während des Starts multithreaded geworden") und einem
   kurzen Socket-Pfad via `-k /tmp/dgsock` (sonst > 103 Zeichen).
2. Backend **muss auf Port 5080 lauschen**. `frontend/src/lib/api.ts`
   überschreibt eine auf localhost/127.0.0.1 zeigende `NEXT_PUBLIC_API_URL`
   zur Laufzeit mit `<host>:5080` (bzw. 7297 bei HTTPS) — ein Backend auf
   einem anderen Port wird vom Browser nie angesprochen.
3. Im Browser-Pane **`next build` + `next start`** verwenden, nicht
   `next dev`: mit Turbopack hydratisiert die Seite dort nicht (Eingaben
   und Effekte laufen nicht, alles bleibt bei „Lädt…").
4. Demo-Anmeldung: `trainer@dogity.test` / `Demo1234!`
   (DemoDataSeeder, nur in Development).

Siehe [[deployment-workflow-user-deploys]] und
[[mobile-app-first-no-horizontal-scroll]].

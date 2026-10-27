---
name: dogity-betriebswirklichkeit-statt-entwuerfe
description: "Bei Dogity den laufenden VPS per `ssh dogity` selbst prüfen statt den Planungsunterlagen im Repo zu glauben"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-09-13T11:37:55.798Z
---

Bei Fragen zum laufenden Betrieb von Dogity (Hoster, Sicherungen, Timer,
Dienste) **auf dem Server nachsehen** – `ssh dogity` ist eingerichtet und
lesende Diagnose ausdrücklich erwünscht. Die Planungsunterlagen im Repo
(DEPLOYMENT.md, TECH_STACK.md, COST STRATEGY.md, ARCHITECTURE.md) beschreiben
teils den Stand der Entwurfsphase, nicht die Wirklichkeit.

**Why:** Am 2026-09-13 habe ich aus DEPLOYMENT.md „Hetzner" übernommen und in
die veröffentlichte Datenschutzerklärung geschrieben – tatsächlich läuft alles
auf **Contabo**. Im selben Zug habe ich behauptet, es gebe keine Sicherungen,
weil `/opt/dogity/backups` fehlte und ich `docs/BACKUP.md` mit falschem Pfad
gesucht hatte. Tatsächlich läuft ein systemd-Timer (nicht cron), der täglich
asymmetrisch verschlüsselt nach Cloudflare R2 sichert. Der Nutzer musste beides
korrigieren: „Prüfe das auf dem VPS selbst."

**How to apply:** Bevor eine Betriebsaussage in einen Text geht, der nach außen
wirkt (Rechtstexte, Dokumentation, Berichte): auf dem VPS prüfen
(`systemctl list-timers`, `systemctl status`, `journalctl -u …`). Verbindlich
für Rechtstexte ist `frontend/src/lib/rechtliches.ts`. Vor der Behauptung, es
gebe etwas nicht, erst im ganzen Repo suchen – ein Fehlschlag mit falschem Pfad
ist kein Beweis. Siehe [[deployment-workflow-user-deploys]] und
[[dogity-lokale-verifikation]].

# Claude-Notizen zu Dogity

Arbeitsgedächtnis des KI-Assistenten (Claude Code) zu diesem Projekt, Stand 2026-10-27, mitgenommen bei der Notebook-Rückgabe. Es sind Arbeitsnotizen, keine geprüfte Doku: Bei Widerspruch gelten Code, docs/ und TODO.md.

Auf einem neuen Rechner wieder einspielen: die Dateien nach `~/.claude/projects/<Projektpfad mit - statt />/memory/` kopieren und die Zeilen unten in die dortige `MEMORY.md` übernehmen.

## Index

- [Deployment-Workflow](deployment-workflow-user-deploys.md) — Nutzer deployt selbst; Claude sagt wann und verifiziert danach (kein Auto-SSH-Deploy)
- [Dogity: Admin nur Betreiber](dogity-admin-nur-betreiber.md) — Admin-Rolle (und Admin + Trainer:in) hat nur der Nutzer; dort sind kleine Kompromisse okay
- [Dogity: Betriebswirklichkeit statt Entwürfe](dogity-betriebswirklichkeit-statt-entwuerfe.md) — Hoster/Sicherungen per `ssh dogity` am Server prüfen; Repo-Planungsdocs sind veraltet (Contabo, nicht Hetzner)
- [Dogity im iOS-Simulator prüfen](dogity-ios-simulator-pruefen.md) — seit 2026-10-02 Xcode da: UI zusätzlich im echten Safari (Simulator) prüfen; Chromium-375px übersah Safari-Datumsfeld-Overflow
- [Dogity-Lasttest](dogity-lasttest.md) — 16.10.: 1.000 Sitzungen fehlerfrei; SQL-Log + Pool behoben, PG 2 GB Puffer; offen Wetter/Login-Drossel
- [Dogity lokal prüfen](dogity-lokale-verifikation.md) — Postgres-Kniffe, Backend MUSS auf Port 5080, `next start` statt `next dev`
- [Dogity Mail/Push/Hoopers](dogity-mail-push-hoopers.md) — Mail+Push+Opt-out auf Prod; 0.35-0.38 (Obedience, Pause, Erinnerungen, Rally Obedience) live; 23.10. S1 Übungsprüfung (0.39, cd0eee0) gepusht, PR1 Prüfungsreife (0.40, 51979f9); beide LIVE (27.10.); G1 Gesundheit (0.41, 09da3dd) wartet auf Test-Deploy; Übergabe + 59 Befunde in TODO.md (87326a4), Notebook-Rückgabe; danach Gesundheit/Impfungen, Mantrailing
- [Dogity: Termine/Statistik/Feedback](dogity-termine-statistik-entscheidung.md) — 08.10.: Leiste bleibt inkl. Statistik, kein Termine-Reiter (Vereine regeln Termine anders), Feedback freiwillig, --no-deps irrelevant
- [Wetter-Feature (Dogity)](dogity-wetter-feature.md) — UMGESETZT: Temperatur beim Fährte-Legen/-Suchen + Differenz (Open-Meteo, ohne API-Key); Ort & Uhrzeit fürs Training wahlweise per Knopf oder von Hand
- [Entwicklung nur außerhalb Stoßzeiten](entwicklung-nur-ausserhalb-stosszeiten.md) — seit 17.10.: vor Entwicklungsarbeit Uhrzeit prüfen (werktags 5-11 Uhr Pazifik ≈ 14-20 Uhr DE), in der Stoßzeit nicht starten
- [Gruppentrainings-Pläne](group-training-plans-feature.md) — notiertes Feature NACH dem Trainingsplan; Brief in docs/GROUP_TRAINING_PLANS.md
- [Kein Edge](kein-edge-browser.md) — Headless/Screenshots nie mit Edge (Popup nervt), Firefox mit Temp-Profil oder eingebauter Browser/Simulator
- [Mobile-App-first / kein H-Scroll](mobile-app-first-no-horizontal-scroll.md) — Grundregel: jede Frontend-Änderung muss sich wie eine native App verhalten, niemals horizontaler Scroll
- [Sachkunde-Lernmodul](sachkunde-lernmodul.md) — Fragentrainer swhv BH/VT umgesetzt; Rechtefrage vom Nutzer entschieden, Prüfungssimulation bewusst offen
- [Smart Trainingsplan-Generator](smart-training-plan-generator.md) — adaptiver Plan-Generator IST umgesetzt & auf Prod (Doc „geplant" veraltet); inkl. „Übungen gewichten"-Regler

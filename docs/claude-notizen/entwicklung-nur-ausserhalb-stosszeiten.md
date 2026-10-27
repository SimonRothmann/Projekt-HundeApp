---
name: entwicklung-nur-ausserhalb-stosszeiten
description: "Betreiber-Regel seit 2026-10-17 - Entwicklung (Sub-Worker-Workflows, größere Umsetzungen) nur außerhalb der Claude-Stoßzeiten; vor Arbeitsbeginn Uhrzeit prüfen"
metadata:
  node_type: memory
  type: feedback
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-10-17T09:53:47.625Z
---

Der Nutzer will, dass nur noch außerhalb der Stoßzeiten des Claude-Dienstes entwickelt wird, damit der Verbrauch (Session-/Wochenlimits) gering bleibt, und hat mich gebeten, das zu überwachen (2026-10-17).

Stoßzeit laut Berichten (Register/TechRadar, März 2026): werktags Mo-Fr 05:00-11:00 Uhr Pazifikzeit. Immer per `TZ=America/Los_Angeles date` prüfen statt umzurechnen (Sommer-/Winterzeit verschieben sich nicht gleichzeitig): in Deutschland meist 14:00-20:00 Uhr, zwischen Ende der EU-Sommerzeit (letzter So. im Oktober) und Ende der US-Sommerzeit (1. So. im November) 13:00-19:00 Uhr. Wochenende ganztags keine Stoßzeit. Laut Sekundärquellen wurde der Stoßzeit-Abzug für Claude Code (Pro/Max) im Mai 2026 aufgehoben - nicht sicher belegt; maßgeblich ist, was der Nutzer will.

**Why:** Token-/Limitverbrauch gering halten; der Nutzer lief mehrfach ins Session-Limit.

**How to apply:** Vor jeder Entwicklungsarbeit (Workflows mit Sub-Workern, Implementierung, größere Recherchen) die Uhrzeit prüfen. In der Stoßzeit nicht starten, sondern dem Nutzer sagen, wann das Fenster endet, und anbieten, Spezifikation vorzubereiten bzw. danach zu starten. Kurze Antworten, Deploy-Prüfungen und Notfall-Hotfixes sind erlaubt (so dem Nutzer angekündigt - er kann es verschärfen). Läuft ein Workflow in eine beginnende Stoßzeit hinein, nicht abbrechen, aber keinen neuen nachschieben. Siehe [[deployment-workflow-user-deploys]].

Ergänzung 2026-10-27: Der Betreiber darf die Regel im Einzelfall aufheben (27.10.: "Jetzt sofort" trotz Stoßzeit nach Rückfrage) - dann starten, aber vorher kurz fragen, nicht stillschweigend. Und Sub-Worker in Workflows immer mit model:'sonnet', sonst erben sie Opus und verbrauchen ein Vielfaches.

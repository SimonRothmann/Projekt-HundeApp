---
name: dogity-ios-simulator-pruefen
description: "Dogity-Oberfläche auch im iOS-Simulator (echtes Safari) prüfen, nicht nur in Chromium mit 375 px"
metadata:
  node_type: memory
  type: feedback
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-10-02T09:09:14.760Z
---

Oberflächenänderungen an Dogity zusätzlich im iOS-Simulator prüfen (Xcode ist seit 2026-10-02 installiert, Gerät z.B. „iPhone 18 Pro“, Werkzeug mcp__Claude_Code_iOS_Simulator__control: attach, open_url, tap, screenshot). Das Simulator-Safari erreicht lokale Server über http://localhost:3000.

**Why:** Die Chromium-Emulation bei 375 px hat einen „total großen Overflow“ in der Anwesenheitskarte nicht gezeigt - Safari gibt input[type=date] eine eigene Mindestbreite. Der Nutzer hat es auf dem iPhone gefunden und kritisiert, dass es nicht richtig geprüft war.

**How to apply:** Bei Formularen, Datums-/Zeitfeldern, Sheets und allem, was Safari anders rendert, vor dem Fertigmelden einmal im Simulator durchklicken (lokal vor dem Deploy, auf test.dogity.net danach). Siehe auch [[mobile-app-first-no-horizontal-scroll]] und [[dogity-lokale-verifikation]].

---
name: kein-edge-browser
description: "Für automatisierte Screenshots/Headless nie Microsoft Edge verwenden, sondern Firefox (Standardbrowser) oder den eingebauten Browser/Simulator"
metadata:
  node_type: memory
  type: feedback
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-10-02T19:44:20.127Z
---

Kein Microsoft Edge für Headless-Screenshots oder Automatisierung. Standardbrowser ist Firefox (/Applications/Firefox.app).

**Why:** Edge wirft beim Nutzer ständig ein störendes Popup hoch (2026-10-02 bei Dogity-Screenshots beschwert).

**How to apply:** Statische Screenshots/Zuschneiden: `firefox --headless --no-remote --profile <tmpdir> --screenshot out.png --window-size=W,H file://...` (eigenes Temp-Profil nötig, sonst kein Bild, weil Firefox schon läuft). Für eingeloggte Seiten den eingebauten Browser (mcp__Claude_Browser__*) oder den iOS-Simulator nehmen statt eines CDP-Skripts mit Edge. Firefox hat kein CDP mehr. Siehe [[dogity-ios-simulator-pruefen]].

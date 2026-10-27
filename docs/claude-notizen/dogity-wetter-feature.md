---
name: dogity-wetter-feature
description: "Dogity — Wetter-Feature ist UMGESETZT (commit f14b3ef, 2026-08-14): Temperatur beim Legen/Suchen einer Fährte plus Differenz, dazu Ort+Uhrzeit fürs normale Training"
metadata: 
  node_type: memory
  type: project
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-08-14T08:32:17.930Z
---

**Umgesetzt am 2026-08-14** (commit `f14b3ef` auf master, Doku in `docs/WEATHER.md`). Vorher als „nächster Punkt nach der Fährten-Auswertung" vorgemerkt.

Fachliche Begründung des Nutzers: Bei der Fährte ist vor allem die **Temperaturänderung zwischen Legen und Suchen** interessant — sie beeinflusst maßgeblich, wie sich die Geruchsspur hält. Umgesetzt als zwei Messungen auf `GpsTrack` (`Laid*`/`Search*`) plus berechnetes `TemperatureDeltaC`; vollautomatisch, weil Ort und beide Zeitpunkte schon in den aufgezeichneten Punkten stecken.

Wichtige Nutzervorgabe fürs **normale Training**: Uhrzeit **und** Ort müssen **wahlweise per Knopf (jetzt / aktueller Standort) oder von Hand** setzbar sein — Trainings werden häufig **nachgetragen**, dann stimmen „jetzt" und „hier" gerade nicht. Diese Regel gilt allgemein für alles, was Dogity automatisch aus dem Moment ableiten will.

Datenquelle: **Open-Meteo**, kostenlos und ohne API-Key (passt zur Kostenstrategie). Forecast-API mit `past_days` für ≤90 Tage, Archive-API (ERA5) für Älteres.

Die alten Freitextfelder `GpsTrack.Weather`/`Wind` bleiben absichtlich bestehen — das sind die **eigenen Beobachtungen** des Nutzers, keine Messwerte.

Noch offen/denkbar: Wetter-Trend über mehrere Trainings (Daten liegen jetzt vor), Bodentemperatur/-feuchte (könnte Open-Meteo auch, fachlich für Fährten interessant).

Bezug: [[deployment-workflow-user-deploys]] fürs Ausrollen, [[mobile-app-first-no-horizontal-scroll]] fürs UI, [[group-training-plans-feature]] als nächstes notiertes Feature.

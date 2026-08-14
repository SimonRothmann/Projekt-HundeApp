# Fährten-Auswertung

Status: **umgesetzt** (Backend + Frontend). Kern: `GpsTrackEvaluator` (rein, deterministisch, ohne DB).

## Was gemessen wird

Je Ablauf-Versuch (`GpsWalkRun`) gegen die gelegte Fährte (`GpsTrack`):

| Kennzahl | Bedeutung |
|---|---|
| `AvgDeviationMeters` / `MaxDeviationMeters` | Ø / max. **senkrechter** Abstand zur gelegten Linie |
| `OnTrackPercent` | Anteil der Punkte innerhalb der „auf Fährte"-Schwelle |
| `ArticlesFound` / `ArticlesTotal` | Gegenstände, denen sich das Team genähert hat |
| `GpsWalkStop[]` | erkannte Halte, klassifiziert (siehe unten) |
| `GpsWalkPoint.DeviationMeters` | Abweichung je Punkt → Karte färbt die Linie abschnittsweise ein |

Alle Werte sind **persistiert** (Migration `AddTrackEvaluation`), damit Trend-Auswertungen nicht sämtliche GPS-Punkte laden müssen. Berechnung beim Anlegen eines Ablaufs, per `POST /api/gps-tracks/{id}/walk-runs/{runId}/evaluate` neu anstoßbar; Altbestand wird beim Anwendungsstart einmalig nachgerechnet (`IGpsTrackEvaluationBackfill`, idempotent über `EvaluatedAt == null`).

## Warum Punkt-zu-Segment statt Punkt-zu-Punkt

Der Hundeführer läuft **5–10 m hinter dem Hund**. Ein Punkt-zu-Punkt-Vergleich („wo müsste er zum Zeitpunkt T sein?") würde diesen zeitlichen Versatz fälschlich als Fehler werten. Gemessen wird deshalb der **senkrechte Abstand zur Linie** — wer hinterherläuft, aber auf derselben Spur, hat Abweichung ≈ 0. Der Test `HandlerWalkingBehind_IsNotCountedAsDeviation` sichert genau das ab.

## Bewusste Messgrenze: die Leinenlänge

Das Gerät ist **am Hundeführer**, nicht am Hund. Der Hund kann im Radius der Fährtenleine (~10 m) ausscheren und zurückkommen, **ohne dass sich das Gerät bewegt**. Solche Ausschläge sind in der Abweichung unsichtbar; die Messung unterschätzt die Hundeabweichung systematisch.

Teilweise ist das fachlich korrekt: kurzes Spinnen, das der Hund selbst korrigiert, ist kein Fährtenverlust. Was zuverlässig sichtbar wird, ist das, was auch zählt — wenn das **Team** wirklich abkommt.

Deshalb heißt die Kennzahl in der UI ausdrücklich **„Abweichung der Hundeführer-Linie"**, mit Hinweis auf die Leinenlänge als Unschärfe.

## Stockungen schließen den blinden Fleck

Sucht, kreist oder verweist der Hund, **bleibt der Hundeführer stehen** — die Position weicht nicht ab, die Bewegung aber schon. Ein Halt gilt als erkannt, wenn die Netto-Verschiebung über mindestens `StopMinDurationSeconds` unter `StopMaxDisplacementMeters` bleibt (gleitendes Fenster, robuster als Momentangeschwindigkeit, die bei GPS-Rauschen flackert).

Klassifizierung über den nächstgelegenen Marker (`GpsMarkerType`) innerhalb von `MarkerProximityMeters`:

- **Gegenstand** → `Indication` = **Verweisen**, erwünscht (mit Dauer)
- **Leckerlipot / Verleitung** → `Explained`, neutral
- kein Marker in Reichweite → `Unexplained` = **das eigentliche Warnsignal**

## Schwellenwerte (fest, dokumentiert)

```
OnTrackThresholdMeters   = 3    // "auf Fährte"
GreenMaxMeters           = 3    // Ampel grün
AmberMaxMeters           = 6    // Ampel gelb, darüber rot
MarkerProximityMeters    = 8    // Gegenstand erreicht / Halt erklärt
StopMaxDisplacementMeters= 3
StopMinDurationSeconds   = 10
```

Bewusst großzügig: Der GPS-Fehler liegt bei **±3–8 m** — in derselben Größenordnung wie die zu messende Abweichung. `MarkerProximityMeters` ist identisch mit dem Auslöseradius der Ablauf-Haptik (`use-walk-run-haptics.ts`), damit Vibration und Auswertung dieselbe Wahrheit nutzen.

**Keine Prüfungspunkte-Schätzung.** Bei diesem Messfehler und Kriterien, die GPS gar nicht sieht (Ausarbeitung, Verweisen-Qualität), wäre eine Note wie „88/100" Schein-Genauigkeit.

Die Douglas-Peucker-Vereinfachung (1 m Toleranz, ab 2000 Punkten) betrifft nur den **Lesepfad**; gerechnet wird auf den gespeicherten Rohpunkten.

## Offen / bewusst nicht enthalten

- GPS am Hund (zweites Gerät / GPX-Import) — einzig echte Lösung für die Leinen-Unschärfe
- ~~Wetter~~ → **umgesetzt**, siehe [WEATHER.md](WEATHER.md): Temperatur beim Legen und Suchen plus die Änderung dazwischen, automatisch über Open-Meteo
- Track-Templates offizieller Prüfungs-Layouts, Heatmaps

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

Alle Werte sind **persistiert** (Migration `AddTrackEvaluation`), damit Trend-Auswertungen nicht sämtliche GPS-Punkte laden müssen. Ein Ablauf lässt sich nur anlegen, solange die Frist zum Suchen läuft (Fährte 24, Trail 48 Stunden nach dem Ende des Legens, gemessen am Beginn des Ablaufs; `Ablauffristen`). Berechnung beim Anlegen eines Ablaufs, per `POST /api/gps-tracks/{id}/walk-runs/{runId}/evaluate` neu anstoßbar; Altbestand wird beim Anwendungsstart einmalig nachgerechnet (`IGpsTrackEvaluationBackfill`, idempotent über `EvaluatedAt == null` bzw. eine ältere `EvaluationVersion`).

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

## Winkel und Schenkel

Nur bei der **Fährte** (`GpsTrackKind.Faehrte`), nicht beim Mantrailing. Die Geometrie der gelegten Linie rechnet `FaehrtenGeometrie` (rein), die Auswertung je Ablauf steht im `GpsTrackEvaluator`. Gespeichert wird je Ablauf in `GpsWalkRunLeg` / `GpsWalkRunCorner`; bei jeder Neuauswertung werden sie ersetzt.

**Erkennung** (an der gelegten Linie, Marker ausgenommen):

```
VereinfachungToleranzMeters = 4     // Douglas-Peucker, eigene Toleranz (nicht die der Anzeige)
WinkelMinDegrees            = 45    // ab dieser Richtungsänderung ein Winkel
SpitzAbDegrees              = 105   // darüber spitz (Innenwinkel unter 75°)
MinAbschnittMeters          = 10    // Abschnitte beiderseits ≥ 10 m; Knicke näher als 10 m = ein Winkel
MaxWinkel                   = 100   // mehr: weder Winkel noch Schenkel (Schutz vor präparierten Zickzack-Aufzeichnungen)
```

Knicke unter 20° nehmen an der Zusammenfassung naher Knicke nicht teil (Reste der Vereinfachung einer Kurve). Zusammengefasste Knicke: Summe der Richtungsänderungen, Lage in der Mitte zwischen erstem und letztem Knick. Richtung links/rechts aus dem Vorzeichen des Kreuzprodukts in Laufrichtung. **Schenkel** sind die Abschnitte zwischen Start, Winkeln und Ende; Längen und Stationierungen (Meter ab Start) gelten entlang der vereinfachten Linie. Die Erkennung ist von der Vibration (`use-walk-run-haptics`, warnt früh ab 30°) getrennt.

**Auswertung je Ablauf** (auf dem *Fortschritt*: Stationierung der auf die vereinfachte Linie projizierten Ablaufpunkte, laufendes Maximum – Rauschen und Zurückgehen setzen ihn nicht zurück):

- **Schenkel**: Dauer vom ersten Punkt mit Fortschritt ≥ Anfang bis zum ersten mit Fortschritt ≥ Ende (leer, wenn das Ende nie erreicht wurde), daraus das Tempo. Beim letzten Schenkel reicht ein Fortschritt bis `MarkerProximityMeters` (8 m) vor dem Fährtenende – dort bleibt der Hundeführer meist stehen, während der Hund am Endgegenstand liegt; das Tempo gilt dann für die gemessene Strecke; Ø Abweichung der Punkte im Schenkel. Ein Punkt genau auf dem Winkel zählt noch zum Schenkel davor.
- **Winkel erreicht**: Fortschritt ≥ Stationierung des Winkels.
- **Überschießen**: größte Abweichung der Punkte mit Fortschritt in [Winkel − 5 m, Winkel + 20 m]; über der gelben Grenze der Fährte (6 m) gilt der Winkel als überschossen. Nicht erreichte Winkel sind weder sauber noch überschossen.
- **Halt am Winkel**: Summe der Halte im Umkreis von `MarkerProximityMeters` (8 m). Ein bisher *unerklärter* Halt dort wird als `AtCorner` („am Winkel“) eingeordnet – der Hund arbeitet den Winkel aus, das ist normal. Gegenstand (Verweisen) und Leckerli (erklärt) gehen vor.
- **Zeit am Winkel**: Zeit zwischen Fortschritt Winkel − 10 m und Winkel + 10 m.

Die Gesamtkennzahlen (Ø/max. Abweichung, % auf Fährte) und die Toleranzen bleiben unverändert; nur die Einordnung der Halte kennt „am Winkel“. Bestehende Auswertungen trägt der Backfill beim Start nach: `GpsWalkRun.EvaluationVersion` gegen `GpsTrackEvaluator.AktuelleVersion` (1); er speichert alle 50 Abläufe und überspringt einen Ablauf, dessen Berechnung eine Ausnahme wirft (Warnung im Log, Version unverändert, nächster Start versucht es erneut).

**Grenzen**: Wie die Abweichung messen auch Winkel und Schenkel die Linie des Hundeführers. Kreuzt sich eine Fährte selbst, kann die Projektion auf den falschen Schenkel springen; die Winkel sind nicht von Hand korrigierbar. Die Entwicklung des Hundes zeigt den Anteil sauberer Winkel nach links / rechts / spitz (ein spitzer Winkel zählt in seiner Richtung und bei „spitz“), erst ab 3 erreichten Winkeln je Gruppe und über die angezeigten jüngsten 12 Abläufe – ohne Note.

## Gegenstände: Angabe der Hundeführer:in und Material

`ArticlesFound` bleibt die GPS-Zahl: Der Hundeführer kam einem Gegenstand auf 8 m (`MarkerProximityMeters`) nahe. Ob der Hund verwiesen, aufgenommen oder überlaufen hat, sieht GPS nicht – das trägt die Hundeführer:in je Gegenstand ein (`GpsWalkRunArticle`: Ablauf, Marker, `ArticleResult` Indicated/PickedUp/Missed; höchstens eine Zeile je Ablauf und Gegenstand, „keine Angabe“ = keine Zeile). Anders als Halte, Schenkel und Winkel ist das eine Eingabe und bleibt bei einer Neuauswertung stehen. Das DTO liefert `articles`, `articlesConfirmedFound` (verwiesen + aufgenommen, `null` ohne Angabe) und `reachedArticleIds` (die Gegenstände, denen GPS nahe kam – dieselbe Regel wie `ArticlesFound`, aus den Rohpunkten des Ablaufs, als Hinweis neben der Angabe). Die Anzeige zeigt mit Angaben die bestätigte Zahl, sonst die GPS-Zahl; die Halte-Einordnung bleibt die GPS-Sicht.

`GpsPoint.Material` (Leder, Holz, Stoff, Teppich, Kunststoff, Sonstiges; Liste und Prüfung in `GegenstandsMaterial`) trägt man nach dem Legen am Gegenstand ein, nicht beim Legen. `GpsTrack.ExcludeFromStats` nimmt eine bewusst schwere Übungsfährte aus der Entwicklung des Hundes: Fährten-Trend, Winkel und Fundquote je Material lassen sie aus (`StatsService.GetDogTrackStatsAsync` – die einzige Stelle, die Fährtenwerte auswertet). Die Fundquote je Material zählt nur bewertete Gegenstände mit Material, über dieselben jüngsten 12 Abläufe wie die Winkel, und zeigt ein Material erst ab 3 Gegenständen.

## Schritte, Bodenzustand, Wind, verdeckt absuchen (Paket F4)

- **Schritte**: `UserPreference.StepLengthCm` (40 bis 120 cm, leer = 75). Die Anzeige rechnet die Länge der Fährte (nicht des Trails) und die Schenkellängen daraus um und rundet auf 10 (`lib/schritte.ts`). Gespeichert wird nur die Schrittlänge, nie die Schrittzahl.
- **Bodenzustand**: `GpsTrack.GroundCondition`, Mehrfachauswahl als Text wie `Surface` (trocken, feucht, nass, gefroren, Schnee), nur bei der Fährte.
- **Wind**: Open-Meteo `wind_direction_10m` → `LaidWindDirectionDeg` / `SearchWindDirectionDeg` (Grad, aus der der Wind kommt). Ältere Fährten bleiben ohne Richtung. Je Schenkel ordnet `lib/wind.ts` ein: Peilung Anfang→Ende (Start, Winkel, Ende der Legung) gegen die Windrichtung, Sektoren je 90 Grad (Gegen-, Rücken-, Seitenwind von links/rechts), mit dem Wind beim Suchen, sonst beim Legen, ab 5 km/h.
- **Verdeckt absuchen**: `GpsTrack.DoubleBlind` wie beim Mantrailing, bei der Fährte nur setzbar, solange kein Ablauf existiert (`PUT /api/gps-tracks/{id}/verdeckt`). Beim Absuchen sind Linie, Marker und Haptik aus; bis der Ansatz (erster Punkt der Legung) auf 10 m gefunden ist, zeigt die Aufzeichnung Entfernung und Richtung dorthin. Wer die Fährte gelegt hat, sucht sie bewusst ebenfalls verdeckt.

## Offen / bewusst nicht enthalten

- GPS am Hund (zweites Gerät / GPX-Import) — einzig echte Lösung für die Leinen-Unschärfe
- ~~Wetter~~ → **umgesetzt**, siehe [WEATHER.md](WEATHER.md): Temperatur beim Legen und Suchen plus die Änderung dazwischen, automatisch über Open-Meteo
- Track-Templates offizieller Prüfungs-Layouts, Heatmaps

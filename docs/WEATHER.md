# Wetter

Status: **umgesetzt** (Backend + Frontend). Datenquelle: [Open-Meteo](https://open-meteo.com) — kostenlos, **ohne API-Key**, ohne Registrierung (siehe COST STRATEGY.md „Start mit 0-10€ monatlich").

## Warum zwei Zeitpunkte bei der Fährte

Fachlich entscheidend ist nicht das Wetter an sich, sondern die **Temperaturänderung zwischen Legen und Suchen** — sie bestimmt maßgeblich, wie sich die Geruchsspur hält. Deshalb speichert `GpsTrack` zwei Messungen plus die Differenz (`TemperatureDeltaC`).

Beides ist **vollautomatisch**, ohne Eingabe: Ort und beide Zeitpunkte stecken bereits in den aufgezeichneten Punkten.

- **Legezeit** = Zeitstempel des ersten automatischen Punkts der gelegten Fährte
- **Suchzeit** = Zeitstempel des ersten Punkts des ersten Ablaufs
- **Ort** = Koordinaten des ersten Fährtenpunkts

Da der Suchzeitpunkt erst mit dem ersten Ablauf existiert, wird die Anreicherung dort erneut ausgeführt (`GpsTrackService.AddWalkRunAsync`). Für Bestandsfährten gibt es `POST /api/gps-tracks/{id}/weather` („Wetter laden").

Die vorhandenen Freitextfelder `GpsTrack.Weather`/`Wind` bleiben unangetastet — das sind die **eigenen Beobachtungen** des Nutzers, nicht Messwerte.

## Normales Training: Uhrzeit + Ort

`TrainingSession` kannte bisher nur ein **Datum** — für einen Wetterabruf fehlten Uhrzeit und Ort. Beide sind jetzt optionale Felder (`StartTime`, `Latitude`/`Longitude`, `LocationName`) und lassen sich **wahlweise per Knopf oder von Hand** setzen:

- Uhrzeit: „Jetzt" **oder** freie Eingabe
- Ort: „Aktuellen Standort verwenden" **oder** Ortssuche per Name/PLZ (Geocoding)

Das ist bewusst so: Trainings werden häufig **nachgetragen**, dann stimmen „jetzt" und „hier" gerade nicht.

Gesetzt über `PUT /api/trainings/{id}/context` — das verwirft den alten Wetterstand (er bezog sich auf andere Zeit/Ort) und holt ihn frisch.

## Endpunktwahl

| Alter der Daten | Endpunkt | Grund |
|---|---|---|
| ≤ 90 Tage | `api.open-meteo.com/v1/forecast` mit `past_days` | verzögerungsfrei — deckt auch das Training von heute Morgen ab |
| > 90 Tage | `archive-api.open-meteo.com/v1/archive` (ERA5) | reicht bis 1940 zurück, hat aber ~5 Tage Rückstand |

Alles wird in **UTC** angefragt und verglichen (kein `timezone`-Parameter-Ratespiel). Gewählt wird der **nächstgelegene Stundenwert**; liegt keiner innerhalb von 3 Stunden, gibt es keinen Wert.

Bekannte Ungenauigkeit: Die App speichert je Training **keine Zeitzone**. Die eingegebene Uhrzeit wird als UTC interpretiert — in Mitteleuropa 1–2 Stunden Versatz. Bei stündlichen Werten vertretbar und ehrlicher, als eine Zeitzone zu erfinden.

## Ausfallverhalten

Wetter ist eine **Anreicherung**, kein Kernvorgang: Bei Nichterreichbarkeit, Timeout (6 s) oder ungültiger Antwort gibt der Provider `null` zurück und protokolliert eine Warnung. Ein Ausfall von Open-Meteo darf **niemals** verhindern, dass ein Training oder eine Fährte gespeichert wird. Abgesichert durch `OpenMeteoWeatherProviderTests`.

## Nicht enthalten

- Wetter-Trend/Auswertung über mehrere Trainings hinweg (die Daten liegen jetzt vor)
- Bodentemperatur/-feuchte (Open-Meteo könnte das — fachlich für Fährten interessant)
- Automatischer Backfill des gesamten Bestands (bewusst per Klick je Fährte, um keinen Anfragen-Sturm beim Start auszulösen)

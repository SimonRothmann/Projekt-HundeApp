# Coding Guidelines


# Allgemein


Code muss:

- lesbar
- testbar
- wartbar

sein.


---

# Naming


Klassen:

PascalCase


Beispiel:

TrainingSession


---

Methoden:

camelCase


Beispiel:

CreateTrainingPlan


---

Variablen:

camelCase


---

# Backend Struktur



Domain

Application

Infrastructure

API

Tests


---

# API Regeln


REST Prinzip.


Beispiel:


GET

/api/dogs


POST

/api/training


PUT

/api/training/{id}


---

# Fehlerbehandlung


Nie:


Exception anzeigen


Immer:

strukturierte Fehler.


---

# Validierung


Jede Eingabe prüfen.


Backend:

immer.


Frontend:

zusätzlich.


---

# Logging


Alle wichtigen Aktionen:


- Login
- Änderungen
- Fehler


---

# Tests


Pflicht:

Unit Tests

Integration Tests


---

# Datenbank


Keine direkten SQL Queries im Business Code.


Immer:

Repository / ORM.


---

# Migrationen


Jede Datenbankänderung:

eigene Migration.


---

# Security


Immer prüfen:


- Auth
- Berechtigungen
- Datenschutz


---

# Performance


Beachten:

- Pagination
- Lazy Loading
- Caching


---

# Code Reviews


Vor Merge:


- Funktion
- Sicherheit
- Architektur

---

# Personenzeit vs. Vereinszeit


- **Personenzeit** (`Ortszeit`, Frontend `heuteIso`): Was der Person gehört - "heute", Wochen und Planwoche, Statistik, Tagebuch, Merkzettel, Gesundheit, Pausen, Fährten-Erinnerung, Ruhezeiten (8-22 Uhr) beim Empfänger. Die Zone meldet das Gerät (`UserPreference.TimeZone`); ohne Meldung und bei unbekannter Zone gilt Europe/Berlin. Bei Anfragen zählt die Zone der anfragenden Person, in Hintergrunddiensten die der empfangenden (`db.ZoneAsync`, `db.ZonenAsync`, `db.HeuteAsync`).
- **Vereinszeit** (`Vereinszeit`, Frontend `vereinszeitTag`): Was dem Verein gehört - Gruppentermine, Vereinsprüfungen, Arbeitseinsätze, Aushang, Vereinsstatistik, Anmeldungen, nächtliche Jobs, Admin-Statistik, Gültigkeit von Prüfungsordnungen. Vereine liegen in DE/AT/CH.
- Neue Stellen ordnen sich vorher ein; im Zweifel Vereinszeit.

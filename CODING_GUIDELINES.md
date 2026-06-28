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
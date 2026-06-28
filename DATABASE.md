# Database Design Document

Projekt:
CanisTrack

Datenbank:
PostgreSQL

---

# Grundprinzipien

Die Datenbank ist vollständig modular aufgebaut.

Wichtig:

Keine Sportart wird hartcodiert.

Keine Prüfung wird als Code implementiert.

Alles wird über Daten modelliert.

---

# Namenskonventionen

Tabellen:

Plural

snake_case


Beispiele:

users

dogs

training_sessions

sports


Primary Keys:

UUID


Beispiel:

id UUID PRIMARY KEY


Zeitstempel:

created_at

updated_at


Soft Delete:

deleted_at


---

# Entity Übersicht


Identity

├── users
├── roles
├── permissions
└── user_roles

Dog

├── dogs
├── dog_health_records
├── dog_documents
└── dog_owners

Sport

├── sports
├── regulations
├── regulation_versions
├── exercises
└── exercise_steps

Training

├── training_sessions
├── training_exercises
├── training_comments
└── training_media

Planning

├── goals
├── training_plans
├── training_plan_items

Community

├── clubs
├── groups
├── group_members
└── trainer_assignments

Tracking

├── gps_tracks
├── gps_points
└── locations

Competition

├── exams
├── exam_results
└── certificates


---

# Identity Bereich

## users

Benutzerkonto.


| Feld | Typ |
|-|-|
| id | UUID |
| email | varchar |
| username | varchar |
| password_hash | varchar |
| firstname | varchar |
| lastname | varchar |
| avatar_url | varchar |
| created_at | timestamp |


---

## roles


Beispiele:


USER

TRAINER

CLUB_ADMIN

JUDGE

ADMIN


---

## user_roles


Ein Benutzer kann mehrere Rollen besitzen.


Beispiel:



Max Müller

USER

TRAINER

CLUB_ADMIN


---

# Hundeverwaltung


## dogs


| Feld | Typ |
|-|-|
| id | UUID |
| name | varchar |
| breed | varchar |
| birthday | date |
| gender | varchar |
| image_url | varchar |
| notes | text |


---

## dog_owners


Relation:

Viele Benutzer können einen Hund besitzen.


Beispiel:



Hund

|

Besitzer

|

Trainer


---

# Sportmodell

Der wichtigste Bereich.


---

## sports


Beispiel:

|id|name|
|-|-|
|1|BH|
|2|IBGH|
|3|Fährte|


---

## regulations


Eine Prüfungsordnung.


Beispiel:



BH 2025

IBGH 2025

IGP 2025


---

## regulation_versions


Damit Änderungen nachvollziehbar bleiben.


Beispiel:



BH

Version 2025

gültig ab 01.01.2025

BH

Version 2027

gültig ab 01.01.2027


---

## exercises


Das Herzstück.


Beispiel:



Fußarbeit

Sitz

Platz

Abrufen

Winkel

Gegenstände

Fährtenaufnahme


Attribute:

| Feld | Beschreibung |
|-|-|
|id|UUID|
|sport_id|Sportart|
|name|Name|
|description|Beschreibung|
|difficulty|Schwierigkeit|
|category|Kategorie|

---

## regulation_exercises


Verknüpft Übungen mit Prüfungen.


Beispiel:



IBGH3

|

Fußarbeit

Pflicht

Bewertung 15 Punkte


---

# Trainingsmodell


## training_sessions


Eine komplette Trainingseinheit.


Beispiel:


"Samstag Training Hundeplatz"


Felder:

|Feld|Typ|
|-|-|
|id|UUID|
|user_id|UUID|
|dog_id|UUID|
|date|date|
|duration|int|
|location_id|UUID|
|weather|json|
|notes|text|


---

## training_exercises


Einzelne Übungen.


Beispiel:


Training:

10.06.2026


enthält:



Fußarbeit

Winkel

Abrufen


---

Felder:


id

training_session_id

exercise_id

rating

difficulty

success

notes



---

# Trainingsbewertung


Bewertung:

1-5 Sterne


Zusätzlich:


Probleme

Verbesserung

Nächstes Ziel


---

# Zielsystem


## goals


Beispiel:



BH Prüfung

Datum:

15.05.2027


Felder:


id

dog_id

exam_id

target_date

status



---

## training_plans


Automatisch generierter Plan.


Beispiel:



KW 12

3x Fußarbeit

2x Ablage

1x Spaßtraining

1x Pause


---

# Community Modell


## clubs


Verein.


Beispiel:



SV OG Musterstadt

SWHV Verein


---

## groups


Trainingsgruppe.


Beispiel:



Dienstag Gruppe

Trainer:

Anna

Mitglieder:

10


---

## group_members


Felder:



group_id

user_id

role

joined_at


---

# Trainer Modell


## trainer_assignments


Ein Trainer betreut Mitglieder.


Beispiel:



Trainer Anna

betreut

Max + Hund Bello


---

Felder:



trainer_id

member_id

dog_id

start_date



---

# Fährtenmodell


## gps_tracks


Eine komplette Fährte.


Felder:



id

training_session_id

length_meter

duration

surface

weather

wind

comment



---

## gps_points


Einzelne GPS Punkte.



track_id

latitude

longitude

timestamp

accuracy



---

# Wetterdaten


Automatisch.


Speichern:


temperature

humidity

wind_direction

weather_condition



---

# Prüfungen


## exams


Beispiel:



BH Prüfung

Datum

Richter

Ort


---

## exam_results



exercise_id

points

comment


---

# Dokumente


## certificates


Speichert:


- Urkunden
- Ergebnisse
- Bilder


---

# Multi Tenant Struktur


Langfristig wichtig.


Ein Verein ist ein Tenant.


Beispiel:



CanisTrack

|

Verein A

|

Gruppen

|

Mitglieder


---

# Berechtigungen


Beispiel:


Trainer:

Kann:

✓ Trainings sehen

✓ Feedback geben


Kann nicht:

✗ Benutzer löschen


---

# Datenschutz


Pflicht:


- DSGVO
- Löschkonzept
- Exportfunktion
- Einwilligungen

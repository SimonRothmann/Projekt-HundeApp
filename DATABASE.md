# Database Design Document

Projekt:
Dogity

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


`USER` wird bei der Registrierung vergeben, `ADMIN` über den
Admin-Bootstrap (`PROD_ADMIN_EMAIL`/`TEST_ADMIN_EMAIL`).

`TRAINER` wird **nicht von Hand** gesetzt, sondern aus der Datenlage
abgeleitet und automatisch nachgeführt (`TrainerRoleService`): Wer eine Gruppe
leitet, in `group_trainers` steht oder in `club_trainers` einem Verein
zugewiesen ist, trägt die Rolle — wer nirgends mehr steht, verliert sie
wieder. Beim Backend-Start läuft der Abgleich einmal über alle Betroffenen.

Die Rolle ist reine Anzeige (Admin-Übersicht, JWT). Autorisiert wird immer
über die konkrete Zuordnung, nie über `TRAINER` — ein verspäteter Abgleich
kann deshalb nichts aufsperren.


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


Wie eine neue Prüfungsordnungs-Revision in den Übungskatalog eingepflegt
wird (PDF → `SportCatalogSeeder.cs`), ist Schritt für Schritt in
[PRUEFUNGSORDNUNG_UPDATE.md](PRUEFUNGSORDNUNG_UPDATE.md) festgehalten.


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


Attribute:

| Feld | Beschreibung |
|-|-|
|id|UUID|
|regulation_version_id|Version der Prüfungsordnung|
|exercise_id|Übung|
|is_mandatory|Pflicht- oder Kürübung|
|max_points|Maximale Punktzahl (0 bei Zeit-/Fehlerpunktwertung)|
|scoring_notes|Prüfungsspezifische Anforderungen dieser Stufe|
|sort_order|Position innerhalb der Prüfungsordnung, kleinere Werte zuerst|


`sort_order` kommt aus der Reihenfolge im `SportCatalogSeeder` - das ist die
Reihenfolge der Prüfungsordnung selbst (Abteilung A vor B vor C). Ohne ihn gab
die Datenbank die Übungen in beliebiger Reihenfolge aus, auf den öffentlichen
Seiten stand dann z.B. "Sitz mit Abholen" vor der "Leinenführigkeit". Von Hand
oder per PDF-Import ergänzte Übungen hängen sich hinten an; bei gleichem Wert
entscheidet der Übungsname.


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

`condition` hält die Verfassung des Hundes an diesem Trainingstag
(Motivated/Settled/Distracted/Tired/Stressed), optional. Grundlage der
Auswertung "Verfassung gegen Bewertung" und "Trainingstage am Stück" - siehe
docs/VERFASSUNG.md. Bewusst an der Einheit und nicht an der einzelnen Übung:
sie soll mit EINEM Tipp gesetzt sein.


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

## group_trainers


Weitere Trainer:innen einer Gruppe neben der/dem in `groups.trainer_id`
hinterlegten Hauptverantwortlichen. Eine Gruppe kann mehrere Trainer:innen
haben, und dieselbe Trainer:in kann in beliebig vielen Gruppen stehen
("in anderen Gruppen mittrainieren"). Wer hier steht, darf die Gruppe genauso
verwalten wie die/der Hauptverantwortliche.


Felder:



group_id

user_id


Eindeutig je (group_id, user_id).


---

# Sachkunde-Fragentrainer


## quiz_catalogs


Ein Fragenkatalog zum Lernen, z.B. die Sachkundeprüfung zur BH/VT.


Felder:



code (eindeutig, z.B. SWHV-BHVT-ERW)

name

description

publisher

source_url

edition

audience (Adults/Youth)

sort_order


Herausgeber, Quelle und Stand stehen bewusst an der Zeile: die Verbände geben
neue Fassungen heraus, und beim Nachziehen muss nachvollziehbar sein, welche
Fassung eine Instanz führt. Gepflegt wird der Inhalt nicht von Hand, sondern
über scripts/import-sachkunde.py und den SachkundeSeeder.


---


## quiz_questions


Eine Frage eines Katalogs.


Felder:



catalog_id

section (A..E, J)

section_name

number (Fragennummer laut Katalog)

sort_order

text

kind (SingleChoice/MultipleChoice/Assignment/FreeText)

sample_solution (nur Assignment/FreeText)

image_name

edited_at

edited_by_user_id


Eindeutig je (catalog_id, number). Der Index kennt `deleted_at` NICHT - wird
eine Frage entfernt und später wieder aufgenommen, muss der Seeder die
vorhandene Zeile wiederbeleben statt eine zweite anzulegen.


Freitextfragen lassen sich nicht automatisch prüfen. Sie tragen statt
Antwortmöglichkeiten eine Musterlösung und werden selbst eingeschätzt
("gewusst" / "nicht gewusst").


`edited_at` wird gesetzt, sobald jemand die Frage in der Verwaltung von Hand
überarbeitet hat. Der Seeder überspringt solche Fragen dann vollständig - ohne
das wäre jede Korrektur beim nächsten Start wieder weg. Dasselbe Muster wie
`goals.plan_managed_by_trainer_id`: wer eingreift, behält das Sagen.


---


## quiz_options


Eine Zeile unterhalb einer Frage - je nach `kind` eine Antwortmöglichkeit, ein
zuzuordnender Begriff oder die Beschriftung eines Zuordnungsschlüssels.


Felder:



question_id

kind (Answer/Term/Label)

text

is_correct (nur bei Answer)

match_key (bei Term der richtige Schlüssel, bei Label der benannte)

image_name (wenn die Antwort selbst ein Bild ist)

sort_order


Bewusst eine Tabelle statt dreier: die Zeilen unterscheiden sich nur in ihrer
Rolle, und eine Frage lädt sie ohnehin immer zusammen. `sort_order` ist der
Abgleichschlüssel des Seeders innerhalb einer Frage; Begriffe und
Beschriftungen liegen deshalb bei 100+ bzw. 200+, damit sie nicht mit den
Antwortzeilen kollidieren.


Label-Zeilen fehlen, wenn die Schlüssel aus einer Abbildung kommen (A2: die
Ziffern 1-5 im Bild). Die Oberfläche leitet die wählbaren Schlüssel dann aus
den Begriffen ab - das verrät nichts, weil die Zuordnung eineindeutig ist.


---


## quiz_masteries


Lernstand einer Frage - je NUTZER, nicht je Hund.


Felder:



user_id

question_id

box (Leitner-Fach 1..5)

last_answered_at

due_at

correct_count

wrong_count

last_was_correct


Eindeutig je (user_id, question_id).


Der Unterschied zu `exercise_masteries`: eine Übung wird mit einem bestimmten
Hund trainiert, die Sachkunde ist der Nachweis des Hundeführers und gilt für
jeden weiteren Hund mit. Die Leitner-Mechanik ist dieselbe, die Bezugsgröße
nicht - und die Intervalle sind kürzer (1/2/4/9/21 Tage statt 2/4/7/14/28),
weil die Sachkunde in Wochen gelernt und nicht über Monate aufgebaut wird.


Eine falsche Antwort setzt `box` auf 1 und `due_at` auf jetzt, nicht nur eine
Stufe herunter: eine Übung, die heute schlechter lief, ist nicht verlernt -
eine falsch beantwortete Frage war schlicht nicht gewusst.


"Von vorne anfangen" setzt die Werte dieser Zeilen zurück und LÖSCHT sie
nicht. Weich gelöschte Zeilen stünden dem eindeutigen Index im Weg, sobald
dieselbe Frage wieder beantwortet wird.


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



Dogity

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

# Verzeichnis von Verarbeitungstätigkeiten (Art. 30 DSGVO)

Internes Dokument. Gehört **nicht** auf die Website, muss aber auf Verlangen
der Aufsichtsbehörde vorgelegt werden können.

Die Pflicht trifft auch kleine Verantwortliche: Die Ausnahme in Art. 30 Abs. 5
DSGVO gilt nur für Verarbeitungen, die *gelegentlich* erfolgen. Ein dauerhaft
betriebenes Konto- und Tagebuchsystem ist das Gegenteil davon.

**Stand:** 2026-09-13 · **Nächste Prüfung:** bei jeder Änderung an den
verarbeiteten Daten (neue Felder, neuer Dienst, neuer Empfänger)

---

## 1. Verantwortlicher

| | |
|---|---|
| Name | Simon Rothmann |
| Anschrift | Hauptstr. 20/2, 76307 Karlsbad |
| Kontakt | siehe `frontend/src/lib/rechtliches.ts` |
| Datenschutzbeauftragter | nicht bestellt (Voraussetzungen des § 38 BDSG liegen nicht vor) |
| Aufsichtsbehörde | LfDI Baden-Württemberg, Stuttgart |

---

## 2. Verarbeitungstätigkeiten

### 2.1 Nutzerkonto und Anmeldung

| | |
|---|---|
| Zweck | Zugang zum persönlichen Trainingstagebuch, Zuordnung der Einträge, Schutz vor fremdem Zugriff |
| Betroffene | Registrierte Nutzer:innen |
| Datenkategorien | Vorname, Nachname, E-Mail, Passwort-Hash, Rolle, Registrierungszeitpunkt, Avatar-URL, Refresh-Token-Hashes |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b (Nutzungsverhältnis), lit. f (Missbrauchsabwehr) |
| Empfänger | Hosting-Dienstleister (Auftragsverarbeiter) |
| Drittland | nein |
| Löschfrist | bis zur Kontolöschung; Refresh-Tokens spätestens nach 60 Tagen |
| Tabellen | `users`, `user_roles`, `refresh_tokens` |

### 2.2 Hunde und Trainingstagebuch

| | |
|---|---|
| Zweck | Führen des Trainingstagebuchs, Auswertung des Verlaufs, Prüfungsvorbereitung |
| Betroffene | Registrierte Nutzer:innen (Hundedaten sind über den Halter personenbeziehbar) |
| Datenkategorien | Hundename, Rasse, Geburtstag, Geschlecht, Notizen, Foto; Trainingsdatum/-zeit/-dauer, Ortsname und Koordinaten, Wetter, Übungen mit Bewertung und Notizen, Trainer-Rückmeldungen; Ziele und Trainingspläne |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b |
| Empfänger | Mitbesitzer:innen und zugeordnete Trainer:innen (innerhalb der App); Hosting-Dienstleister |
| Drittland | nein |
| Löschfrist | bis zur Löschung des Eintrags bzw. des Kontos |
| Tabellen | `dogs`, `dog_owners`, `dog_images`, `training_sessions`, `training_exercises`, `goals`, `training_plans`, `training_plan_items`, `exercise_masteries` |

### 2.3 Fährtenaufzeichnung (Standortdaten)

| | |
|---|---|
| Zweck | Aufzeichnen der gelegten Fährte und des Ablaufs, Auswertung von Abweichung, Gegenständen und Stockungen |
| Betroffene | Registrierte Nutzer:innen |
| Datenkategorien | GPS-Punkte (Koordinaten, Zeitstempel, Genauigkeit), Markierungen, errechnete Auswertungen |
| Besonderheit | Bewegungsprofil mit Metergenauigkeit – die eingriffsintensivste Verarbeitung der App. Start nur durch den Nutzer, zusätzlich durch die Browser-Standortfreigabe geschützt. Keine Hintergrundaufzeichnung. |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b (Kernfunktion, vom Nutzer ausgelöst) |
| Empfänger | wie 2.2; **keine** Übermittlung an Kartendienste |
| Drittland | nein |
| Löschfrist | bis zur Löschung der Fährte bzw. des Kontos |
| Tabellen | `gps_tracks`, `gps_points`, `gps_walk_runs`, `gps_walk_points`, `gps_walk_stops` |

### 2.4 Verein, Gruppen und Gruppentraining

| | |
|---|---|
| Zweck | Mitglieder- und Gruppenverwaltung, Planung von Gruppentrainings, Zuordnung Trainer:in ↔ Hund |
| Betroffene | Mitglieder, Trainer:innen, Vereinsverwaltung |
| Datenkategorien | Mitgliedschaften und Beitrittsanfragen mit Status und Zeitpunkt, Trainerrollen, Gruppenzugehörigkeit, Trainingstermine und zugewiesene Übungen |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b |
| Empfänger | Vereinsverwaltung und Trainer:innen des jeweiligen Vereins |
| Drittland | nein |
| Löschfrist | bis zum Austritt bzw. zur Kontolöschung |
| Tabellen | `clubs`, `club_memberships`, `club_trainers`, `club_registrations`, `groups`, `group_members`, `group_trainers`, `trainer_assignments`, `group_training_*` |

### 2.5 Sachkunde-Lernmodul und Benachrichtigungen

| | |
|---|---|
| Zweck | Lernfortschritt im Fragentrainer, Hinweise innerhalb der App |
| Betroffene | Registrierte Nutzer:innen |
| Datenkategorien | je Frage: Lernfach, richtige/falsche Antworten, Zeitpunkte; Benachrichtigungstexte mit Zeitpunkt und Lesestatus |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b |
| Löschfrist | bis zur Kontolöschung |
| Tabellen | `quiz_masteries`, `notifications` |

### 2.6 Betrieb der Server

| | |
|---|---|
| Zweck | Auslieferung der Seite, Betrieb, Fehlersuche, Abwehr von Angriffen |
| Betroffene | alle Besucher:innen |
| Datenkategorien | IP-Adresse, Zeitpunkt, aufgerufene Adresse, Browserkennung; kurzzeitige Zählung der Anmeldeversuche je IP |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. f |
| Besonderheit | keine Zusammenführung mit Kontodaten, keine Auswertung des Nutzungsverhaltens |
| Löschfrist | Container-Logs rollieren (10 MB × 3 je Dienst); Sicherungen 30 Tage |

---

## 3. Auftragsverarbeiter (Art. 28)

| Dienstleister | Leistung | Vertrag | Drittland |
|---|---|---|---|
| Hetzner Online GmbH | Server, Datenbank, Sicherungen | AV-Vertrag erforderlich – im Kundenkonto abschließbar | nein (Standort Deutschland) |

**Offen:** Abschluss bzw. Nachweis des AV-Vertrags dokumentieren.

---

## 4. Eigenständig Verantwortliche (keine Auftragsverarbeitung)

| Stelle | Wann | Was fließt | Drittland |
|---|---|---|---|
| OpenStreetMap Foundation | Kartenkacheln, Standardansicht | IP-Adresse und Kartenausschnitt, direkt vom Gerät | UK (Angemessenheitsbeschluss) |
| Esri | Kartenkacheln, nur bei aktiv gewähltem Luftbild | IP-Adresse und Kartenausschnitt, direkt vom Gerät | **USA** |
| komoot GmbH (Photon) | Ortssuche | Suchbegriff, ggf. Umkreis-Koordinaten – **vom Server**, ohne Nutzer-IP | nein |
| Open-Meteo | Wetter zu Ort und Zeitpunkt | Koordinaten und Datum – **vom Server**, ohne Nutzer-IP | nein |
| Ko-fi | nur ausgehender Verweis | nichts, solange der Link nicht angeklickt wird | USA (erst nach Klick) |

**Offen:** Für Esri prüfen, ob eine Teilnahme am EU-U.S. Data Privacy Framework
besteht. Falls nicht, Luftbild-Anbieter wechseln oder das Umschalten an eine
Einwilligung koppeln. Bis dahin mindert die Voreinstellung das Risiko: Die
Karte startet immer mit OpenStreetMap, das Luftbild lädt erst auf aktive
Auswahl.

---

## 5. Technische und organisatorische Maßnahmen (Art. 32)

- TLS für alle Verbindungen, HSTS, automatische Zertifikate über Let's Encrypt
- Passwörter nur als Hash (ASP.NET Core Identity)
- Kurzlebige Access-Tokens (60 Minuten), rotierende Refresh-Tokens mit
  Wiederverwendungserkennung, serverseitig widerrufbar
- Content-Security-Policy durchsetzend, `frame-ancestors 'none'`,
  `X-Content-Type-Options`, Referrer-Policy
- Rate-Limit auf den Anmeldewegen (10 Anfragen/Minute je IP)
- Zugriffsprüfung auf Datenebene: jeder Abruf ist an Besitz oder eine
  ausdrückliche Trainerzuordnung gebunden
- Getrennte Datenbanken für Test und Produktion, eigene Rollen
- Tägliche Sicherung, Aufbewahrung 30 Tage

---

## 6. Betroffenenrechte – technische Umsetzung

| Recht | Umsetzung |
|---|---|
| Auskunft und Übertragbarkeit (Art. 15, 20) | `GET /api/profile/export`, in der App unter Profil → „Meine Daten herunterladen“ |
| Löschung (Art. 17) | `DELETE /api/profile` mit Passwortbestätigung, in der App unter Profil → „Konto löschen“; entfernt die Fachdaten und anschließend das Konto |
| Berichtigung (Art. 16) | Profil, Hunde- und Trainingsbearbeitung in der App |
| Einschränkung, Widerspruch (Art. 18, 21) | formlos per E-Mail |

---

## 7. Datenschutz-Folgenabschätzung (Art. 35)

Nicht erforderlich. Es werden keine besonderen Kategorien nach Art. 9
verarbeitet, es findet keine systematische Bewertung von Personen und keine
umfangreiche Beobachtung öffentlich zugänglicher Bereiche statt. Die
Standortdaten entstehen ausschließlich durch eine vom Nutzer selbst gestartete,
zeitlich begrenzte Aufzeichnung.

Diese Einschätzung ist neu zu treffen, falls eine Hintergrundaufzeichnung,
eine automatische Standorterfassung oder eine Auswertung über mehrere Personen
hinweg dazukommt.

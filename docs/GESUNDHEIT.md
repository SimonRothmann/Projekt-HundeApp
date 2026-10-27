# Gesundheit & Impfungen

Eingeführt mit Paket G1 (2026-10-27, Fassung 0.41). Auftrag des Betreibers (2026-10-23): „Impfungen (z. B. Tollwut),
Entwurmung, Tierarzttermine je Hund, mit Push-Erinnerung vor Ablauf.“

## Zweck

Ein Merkzettel je Hund, kein Nachweis: Die Besitzer:in trägt ein, was im Impfpass steht, und Dogity erinnert rechtzeitig.
Wer einen Impfnachweis zeigen muss, zeigt den Impfpass. Es gibt deshalb **keine Datei-Uploads** (kein Foto des Impfpasses) -
Dogity hat keinen Dateispeicher, und das soll so bleiben.

## Festlegungen

Vom Chef-Entwickler getroffen; der Betreiber kann sie ändern.

1. **Sichtbarkeit: nur Besitzer:innen und Mitbesitzer:innen.** Betreuende Trainer:innen (auch mit Betreuung) und Fremde sehen
   nichts - serverseitig durchgesetzt: Sie bekommen auf alle vier Endpunkte „nicht gefunden“ (404), auch beim Lesen, und am Hund
   (`DogDto.Gesundheit`) kommt der Stand nur bei Besitzer:innen an; `SupervisedDogDto` hat das Feld gar nicht. Begründung:
   Gesundheit ist privat. Die Trainingspause (Paket P1) war die Ausnahme, weil der Plan der Trainer:in davon abhängt - hier
   hängt nichts davon ab. Es sind Daten über das Tier, keine Gesundheitsdaten der Person im Sinne von Art. 9 DSGVO; trotzdem
   nur für Besitzer:innen sichtbar.
2. **Keine medizinische Logik.** Dogity rechnet keine Impfintervalle aus. Die Person trägt „gültig bis“ bzw. „nächste fällig“
   selbst ein, wie es der Tierarzt im Impfpass vermerkt hat. Die Oberfläche bietet nur Schnellwahl-Knöpfe (+1 Jahr, +3 Jahre
   bei Impfungen; +3 Monate bei Entwurmung und Parasitenschutz), die das Feld vorfüllen und überschrieben werden können. Ein
   Satz in der Oberfläche sagt es: „Maßgeblich ist, was der Tierarzt im Impfpass einträgt.“ Die Begründung: Intervalle hängen
   von Präparat, Hersteller, Land und Tier ab (Tollwut: ein bis drei Jahre); jede feste Zahl in der App wäre irgendwann
   falsch und sähe trotzdem verbindlich aus.
3. **Erinnerungen nur per Glocke + Push, nie per Mail**, über den vorhandenen Erinnerungsdienst (`ErinnerungService`).

## Datenmodell

Eine Entität für alle Einträge: `DogHealthRecord` (Tabelle `dog_health_records`, Soft Delete mit globalem Query-Filter, der den
Filter des Hundes mitzieht).

| Feld | Bedeutung |
| --- | --- |
| `DogId` | der Hund (Cascade) |
| `Kind` | `Vaccination`, `Deworming`, `ParasiteProtection`, `VetAppointment`, `Other` - als Text gespeichert wie `DogPause.Reason`; im JSON numerisch wie alle Enums der API |
| `Title` | Pflicht, höchstens 100 Zeichen; bei Impfungen schlägt die Oberfläche Tollwut, SHPPi, Leptospirose, Zwingerhusten, Borreliose vor (frei änderbar) |
| `Date` | gegeben am bzw. Tag des Termins |
| `Time` | nur beim Tierarzttermin, dort optional |
| `DueDate` | gültig bis bzw. nächste fällig, **einschließlich** („läuft heute ab“); beim Tierarzttermin immer leer |
| `Note` | höchstens 500 Zeichen |
| `CreatedByUserId` | wer es angelegt hat; bei der Kontolöschung der Person geleert, wenn der Hund bei den anderen bleibt |

Regeln im Service (`DogHealthService`, dieselben im Frontend, `lib/gesundheit.ts`): Datum höchstens 25 Jahre zurück und 2 Jahre
voraus; `DueDate` nicht vor `Date` und höchstens 10 Jahre danach; Uhrzeit nur beim Termin; Termin ohne `DueDate`.

**„Aktuell“ und Verlauf.** Eine erneuerte Impfung ist einfach ein neuer Eintrag. Je Hund, Art und Titel (ohne Rand-Leerzeichen,
Groß-/Kleinschreibung egal) ist der Eintrag mit dem jüngsten `Date` „aktuell“ (bei gleichem Datum der zuletzt angelegte); die älteren
bleiben im Verlauf. Tierarzttermine sind aktuell, solange sie heute oder später sind. Die Funktion ist `Gesundheitsstand.Aktuelle`.

**Zustand einer Frist** (`Gesundheitsstand.Zustand`, rein, „heute“ in Vereinszeit): `Overdue` ab dem Tag nach `DueDate`, `DueSoon`
bei höchstens 30 Tagen bis `DueDate` (auch am Tag selbst), sonst `Ok`; ohne `DueDate` immer `Ok`.

## API

Alle unter `/api/dogs/{id}/gesundheit`, Rechteprüfung wie bei den Pausen (Besitzer:in/Mitbesitzer:in, sonst 404; zusätzlich muss der Hund
noch da sein):

- `GET` - alle Einträge, jüngste zuerst, mit `isCurrent`
- `POST` - anlegen
- `PUT /{eintragId}` - ändern
- `DELETE /{eintragId}` - löschen (weich)

**Gesundheitsstand am Hund.** `DogDto.Gesundheit` (`DogHealthSummaryDto`: Anzahl überfällig, Anzahl bald fällig, der Eintrag mit der frühesten
Frist, der nächste Termin) wird dort mitgeliefert, wo Besitzer:innen ihre Hunde ohnehin laden: `GET /api/dogs` (damit auch `/api/dashboard`)
und `GET /api/dogs/{id}`. Eine einzige Abfrage für alle Hunde (`HealthSummariesAsync`), kein N+1; für Trainer:innen immer `null`.

## Erinnerungen

Regeln als reine Funktionen in `Erinnerungsregeln.GesundheitsErinnerungen`; der Dienst (`ErinnerungService.GesundheitAsync`) besorgt die Daten.
Versandfenster wie bei allen: 08:00-21:59 Vereinszeit.

| Art (`ReminderType`) | Wann | Text (Beispiel) |
| --- | --- | --- |
| `HealthDueAhead` | `DueDate` liegt 1 bis 14 Tage in der Zukunft | „Bello: Die Impfung Tollwut ist bis 18.11.2026 gültig.“ / „Bello: Entwurmung ist am 07.11.2026 fällig.“ |
| `HealthDueToday` | `DueDate` ist heute | „Bello: Die Impfung Tollwut läuft heute ab.“ / „Bello: Entwurmung ist heute fällig.“ |
| `HealthVetDayBefore` | Tierarzttermin morgen, ab 18:00 am Vortag | „Morgen 10:30: Tierarzt mit Bello (Impfung).“ (ohne Uhrzeit: „Morgen: Tierarzt mit …“) |

- Englische Fassungen über `Meldung.Von` und `Uebersetzung.Englisch`.
- **„In den letzten 14 Tagen“, nicht „genau 14 Tage vorher“:** Wer einen Eintrag erst zehn Tage vor Ablauf anlegt oder dessen Tick an genau dem
  Tag ausfiel, wird trotzdem noch einmal erinnert; am Tag selbst kommt nur die zweite Stufe. Nach Ablauf kommt nichts mehr - das Überfällige zeigt
  die App selbst (rot im Bereich Gesundheit, Hinweis auf der Startseite).
- **Nur der jüngste Eintrag** je Hund, Art und Titel erinnert: Eine erneuerte Impfung beendet die Erinnerung der alten.
- **Doppelversand-Schutz** über `sent_reminders` (Person, Art, Eintrag, Tag): Der Tag ist der der Frist bzw. des Termins. Wird das Datum eines Eintrags
  nachträglich geändert, kommt vor dem neuen Datum erneut eine Erinnerung.
- **Empfänger:** alle Besitzer:innen und Mitbesitzer:innen des Hundes (aktive Zeilen in `dog_owners`, keine offenen Einladungen) mit eingeschaltetem
  Schalter (`UserPreference.ReminderHealth`, Vorgabe an; Profil → Funktionen) **und** eingeschaltetem Modul. Trainer:innen bekommen nichts.
- Gelöschte Einträge, gelöschte und archivierte Hunde erinnern nicht.
- Link in der Meldung: `/dogs/{id}?tab=gesundheit`.

## Oberfläche

- **Hundeseite:** Bereich „Gesundheit“ als **dritter Reiter** neben „Plan“ und „Tagebuch“, nur für Besitzer:innen und nur bei eingeschaltetem
  Modul. Entscheidung: Er passt bei 375 px, weil die drei Segmente ungleich breit sind (`grid-cols-[0.65fr_1.75fr_1.1fr]`, das Tagebuch mit seiner
  längeren Beschriftung „Tagebuch (12 Tage)“ bekommt am meisten, „Plan“ am wenigsten); ein Menüpunkt im Verwalten-Sheet hätte den Link der Startseite und der Erinnerung
  (`?tab=gesundheit`) nirgends hingeführt. Ein Link auf den Reiter ohne Berechtigung fällt auf den Standard-Reiter zurück
  (`verfuegbarerReiter`).
- **Inhalt:** „Als Nächstes“ (überfällig rot, bald fällig gelb, der nächste Tierarzttermin), darunter die aktuellen Einträge je Art, der Verlauf
  aufklappbar, „Eintrag hinzufügen“ mit Formular (Art, Titel mit Vorschlägen, Datum, Uhrzeit nur bei Tierarzt, gültig bis mit Schnellwahl, Notiz).
  Jeder Zustand steht in drei Formen: Farbe, Symbol und Wort („Überfällig“, „Bald fällig“, „In Ordnung“).
- **Startseite:** ein dezenter Hinweis je Hund (`GesundheitsHinweise`), nur wenn etwas überfällig oder in höchstens 14 Tagen fällig ist oder ein
  Tierarzttermin in höchstens 2 Tagen ansteht („Bello: Tollwut gültig bis 03.11.“); ein Tipp führt in den Bereich.
- **Modul:** „Gesundheit“ (`gesundheit`) ist über den vorhandenen Mechanismus (`UserDisabledModule`) abschaltbar, Vorgabe an. Abgeschaltet = Reiter,
  Hinweis auf der Startseite und Erinnerungen weg; die Daten bleiben.
- Alle Texte über `t()`/`en.ts`. Die Titelvorschläge sind übersetzt (englisch „Rabies“ usw.) und werden so gespeichert, wie sie in der Sprache der
  Person stehen; bei einem Sprachwechsel zwischen zwei Impfungen derselben Art gelten „Tollwut“ und „Rabies“ als verschiedene Titel.

## Datenschutz

Neue Datenkategorie „Gesundheitseinträge des Hundes“: Datenschutzerklärung (Abschnitte Hunde, Erinnerungen, „Wer deine Daten sonst sieht“, Speicherdauer;
deutsch und englisch), `STAND`/`STAND_EN`, Verarbeitungsverzeichnis 2.2 und 2.5c2. `AccountDataService`: Export (`Gesundheit`, auch geteilter Hunde),
Kontolöschung (die Einträge eigener Hunde gehen endgültig mit, auch weich gelöschte; an geteilten Hunden bleiben sie bei den anderen, nur
`CreatedByUserId` wird geleert), dazu Schalter und versendete Erinnerungen. Einen eigenen Pfad zum endgültigen Löschen eines Hundes gibt es nicht
(Hunde werden weich gelöscht, endgültig nur mit dem Konto).

## Demo

Siehe `docs/DEMO_DATEN.md`, Szenario „Gesundheit“: Max' Bello und Emma.

## Nicht Teil dieses Pakets

Kein Foto vom Impfpass; keine Impfintervall-Vorschläge; keine Erinnerung an Überfälliges nach dem Tag selbst; keine Erinnerung per E-Mail;
Gruppentraining und Hundeliste zeigen den Stand nicht (die Hundeliste bekommt das Feld, nutzt es aber noch nicht).

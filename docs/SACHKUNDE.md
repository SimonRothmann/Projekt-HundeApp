# Sachkunde-Fragentrainer

Status: **umgesetzt** (2026-09-01). Lernen, Fehlerspeicher und Neustart stehen;
die Prüfungssimulation ist bewusst zurückgestellt (siehe „Offen").

## Warum

Der Sachkundenachweis ist Voraussetzung für die BH/VT — er steht so schon im
Beschreibungstext der BH im `SportCatalogSeeder`. Wer sich darauf vorbereitet,
lernt Wochen bevor er ein Trainingstagebuch braucht. Das macht den Trainer zum
natürlichsten Einstieg in die App, den es gibt: er ist ohne Anmeldung nutzbar,
er beantwortet eine Frage, die täglich gesucht wird, und er endet genau dort,
wo das Tagebuch anfängt.

Technisch war der Ausschlag, dass die Leitner-Mechanik bereits gebaut war
(`ExerciseMastery`, siehe `docs/SMART_TRAINING_PLAN.md`). Ein Fragentrainer ist
der Lehrbuchfall dafür.

## Herkunft der Fragen

Die Kataloge zur BH/VT-Sachkundeprüfung des **Südwestdeutschen
Hundesportverbands e.V. (swhv)**, Fassungen für Erwachsene und für Jugend, vom
swhv öffentlich zum Download bereitgestellt. Übernahme auf ausdrückliche
Entscheidung des Auftraggebers (2026-09-01: „die sind öffentlich und damit
zugänglich, jedes Mitglied der App darf die nutzen") — vergleichbar mit der
Freigabe der FCI-Prüfungsordnung im Sportkatalog.

Herausgeber, Quelle und Stand stehen am Katalog und werden in der Oberfläche
genannt.

| Katalog | Code | Fragen | Aufbau |
|---|---|---|---|
| Erwachsene | `SWHV-BHVT-ERW` | 72 | Komplex A 23 · B 19 · C 10 · D 13 · E 7 |
| Jugend | `SWHV-BHVT-JGD` | 40 | ein Block; unter 15 Jahren, 15 Fragen in der Prüfung |

Komplexe: **A** Verhalten und Umgang · **B** Zucht, Aufzucht und Gesundheit ·
**C** Recht · **D** Kynologie, Verbände und Ausbildung · **E** Prüfungswesen.
Die Überschriften im PDF nennen nur den Buchstaben; die Bezeichnungen sind aus
den Fragen des jeweiligen Komplexes abgeleitet und dienen der Navigation.

## Import

`scripts/import-sachkunde.py <ordner-mit-den-pdfs>` erzeugt
`backend/src/Dogity.Infrastructure/Persistence/Seed/Data/sachkunde-swhv.json`
und `frontend/public/sachkunde/a2.jpg`. Braucht `poppler` (`pdftotext`,
`pdfimages`).

Erscheint eine neue Fassung: Skript erneut laufen lassen, `STAND` hochsetzen,
deployen. Der `SachkundeSeeder` gleicht anhand der Fragennummer ab — er ändert
vorhandene Zeilen, statt sie zu ersetzen, damit der Lernstand der Nutzer
erhalten bleibt.

Die PDFs sind erstaunlich sauber maschinenlesbar: `x` markiert die richtige
Antwort, `□` die übrigen. 106 der 112 Fragen kommen vollautomatisch durch.
Sechs sind Sonderfälle und als solche markiert, nicht geraten:

- **Zuordnung** (A2 Körperhaltungen, A18 Rassemerkmale, A23 Talente)
- **Freitext mit Musterlösung** (D5 Sinne, D6 Krankheiten, D8 Temperatur messen)

Dazu zwei Bildfragen: **A2** (eine Zeichnung mit fünf nummerierten Haltungen, an
der Frage) und **Jugend 30** („Welcher Hund zeigt eine Spielhaltung?" — drei
einzelne Zeichnungen, je eine an einer Antwort).

**Zuordnungen werden zugeordnet**, nicht selbst eingeschätzt: je Begriff eine
Zeile mit den wählbaren Schlüsseln, geprüft auf Knopfdruck und nur ganz — eine
Zuordnung stimmt, wenn alle Begriffe stimmen. Bei A2 sind die Schlüssel die
Ziffern aus der Abbildung, bei A18/A23 die Buchstaben mit ihrer Beschriftung
(„A langhaarig", „B extrem hochbeinig", …). A2 zeigt fünf Körperhaltungen als
Zeichnung; ohne das Bild ist die Frage sinnlos, deshalb wird es aus dem PDF
mitgezogen.

> Der erste Anlauf hat diese drei Fragen als Karte zum Selbsteinschätzen
> gebaut — Lösung aufdecken, „gewusst"/„nicht gewusst". Das war falsch: die
> Fragestellung lautet „Ordnen Sie den aufgelisteten Stimmungen die abgebildeten
> Körperhaltungen zu", und **aufgelistet war nichts**. Man konnte die Aufgabe
> gar nicht versuchen, nur die Lösung ansehen. Die Struktur lag beim Import
> schon vor und wurde zu einem Lösungssatz zusammengefaltet, statt sie zu einer
> Aufgabe zu machen.

Die wählbaren Schlüssel leitet die Oberfläche aus den Begriffen ab, wenn keine
Beschriftungen im Katalog stehen (A2). Das verrät nichts: die Zuordnung ist
eineindeutig, jeder Schlüssel kommt genau einmal vor — gesucht ist die
Reihenfolge, nicht die Menge.

**Freitextfragen** (D5, D6, D8) bleiben Selbsteinschätzung: nachdenken, Lösung
aufdecken, „gewusst"/„nicht gewusst". Die kann niemand automatisch prüfen.

Drei Fallen, die das Skript kennt und deshalb prüft:

- Die Spaltenbreite im PDF wird nicht durchgehalten. „c) Aufforderung zum Spiel
  3" steht mit nur EINEM Leerzeichen da — ein zu strenges Muster übersieht die
  Zeile, und die Lösung ist still unvollständig. Ist eine Zuordnung
  durchbuchstabiert, prüft das Skript die Folge auf Lücken und bricht ab.
- Eine Fragennummer steht als `C. 3:` statt `C 3:` im Original.
- Eine Zuordnung mit weniger als zwei Begriffen oder mit einem doppelt
  vergebenen Schlüssel ist nicht lösbar — das Skript bricht ab, statt eine
  kaputte Aufgabe zu seeden.
- Die Ankreuzkästchen der Jugendfassung kommen als **Steuerzeichen** aus
  `pdftotext` (U+0088 für das leere). Das leere Kästchen stand in der ersten
  Fassung in **jedem** der 79 Antworttexte — im Terminal unsichtbar, in der App
  sichtbar. Alle Steuerzeichen fliegen jetzt raus.
- Bildunterschriften landen als zusätzliche Antworten im Text. Bei Jugend 30
  waren das eine Zeile „1  3" und ein zweites „2" — fünf Antworten statt drei.
  Eine reine Zahlenzeile gilt nur noch als Antwort, wenn diese Zahl nicht schon
  dasteht.
- Die Reihenfolge der eingebetteten Bilder ist **nicht** die Reihenfolge auf der
  Seite. Bei Jugend 30 gehört die Unterschrift 1 zur zweiten eingebetteten
  Zeichnung. In die Zeichnungen sind zudem noch die Zahlen 3, 4 und 2
  eingebrannt — Reste derselben Vorlage, aus der auch A2 stammt. Maßgeblich sind
  die Unterschriften des PDF; die Zuordnung ist gegen die gerenderte Seite 5
  geprüft und im Skript festgehalten.

## Lernen

Der Ablauf ist dem der Führerschein-Trainer nachgebaut:

- **eine Frage je Bildschirm**, Antwort antippen, sofort auflösen. Bei
  Einfachauswahl ist Antippen zugleich Abgeben — ein Tipp statt drei;
- **falsch beantwortet heißt: die Frage kommt wieder.** Und zwar zweifach —
  noch in derselben Runde (vier Fragen später, im Browser eingereiht) und an den
  Folgetagen (der Server setzt das Leitner-Fach auf 1 und die Wiedervorlage auf
  jetzt);
- **ist alles durch, endet die Runde ausdrücklich.** Kein leerer Bildschirm,
  sondern „Alles durch" mit dem Angebot, von vorne anzufangen.

Drei Modi: **Lernen** (fällige zuerst, dann noch nie gesehene), **Fehler** (nur
was zuletzt falsch war) und **Alle** (der Katalog der Reihe nach).

Wiedervorlage je Fach: **1, 2, 4, 9, 21 Tage**. Kürzer als beim Trainingsplan
([2, 4, 7, 14, 28]): eine Übung baut man über Monate auf, die Sachkunde lernt
man in den Wochen vor der Prüfung. Ab Fach 4 gilt eine Frage als gekonnt.

## Ohne Anmeldung

Katalog und Fragen sind öffentlich lesbar (`[AllowAnonymous]`), der Lernstand
nicht. Wer nicht angemeldet ist, geht den Katalog der Reihe nach durch; die
Auswertung passiert im Browser nach derselben Regel wie im Backend, gespeichert
wird nichts. Ein Hinweis verweist auf die Anmeldung.

Die Seiten sind serverseitig gerendert und stehen in der Sitemap — dieselbe
Überlegung wie bei den Prüfungsordnungen.

## API

| Endpunkt | |
|---|---|
| `GET /api/sachkunde/catalogs` | Kataloge mit Fragenzahl je Komplex (anonym) |
| `GET /api/sachkunde/catalogs/{code}/questions?section=` | alle Fragen, wahlweise ein Komplex (anonym) |
| `GET /api/sachkunde/catalogs/{code}/session?mode=&limit=` | die nächsten Fragen |
| `GET /api/sachkunde/catalogs/{code}/progress` | Lernstand, auch je Komplex |
| `POST /api/sachkunde/questions/{id}/answer` | Antwort abgeben |
| `POST /api/sachkunde/catalogs/{code}/reset` | von vorne anfangen |

Über richtig/falsch entscheidet der **Server**, wo er es kann: bei
Auswahlfragen muss die Auswahl die richtigen Antworten genau treffen (eine
zusätzlich angekreuzte falsche ist ein Fehler, nicht „fast richtig"), bei
Zuordnungen müssen alle Schlüssel stimmen. Nur die offenen Freitextfragen
werden selbst eingeschätzt.

Die Antwort trägt je nach Fragetyp `selectedOptionIds`, `assignments`
(Begriffs-Id → Schlüssel) oder `selfAssessedCorrect`.

## Verwaltung: Fragen von Hand überarbeiten

`/admin` → „Sachkunde-Fragen". Alle 112 Fragen zum Durchsehen, mit Filter nach
Katalog und Komplex, Volltextsuche (auch über Antworten und Musterlösungen) und
zwei Schaltern: **nur auffällige** und **nur bearbeitete**.

**Wer hier speichert, hat ab dann das Sagen.** Die Frage bekommt `EditedAt`, und
der Seeder lässt sie beim nächsten Start in Ruhe. Ohne das wäre jede Korrektur
beim nächsten Deploy wieder weg — der Seeder schreibt Text und Antworten sonst
bei jedem Hochfahren aus der Katalogdatei. Dasselbe Muster wie beim
trainergepflegten Trainingsplan (`Goal.PlanManagedByTrainerId`).

„Katalogfassung zurückholen" nimmt die Marke zurück. Der Text bleibt zunächst
stehen; die Vorlage kommt erst beim nächsten Start der App wieder.

**Auffälligkeiten** sind Hinweise, kein Urteil: Steuerzeichen, Trennstrich mitten
im Wort, fehlendes Leerzeichen, Leerzeichen vor Satzzeichen, doppeltes
Leerzeichen, unpaarige Klammer — dazu „keine eindeutige Lösung" und „weniger als
zwei Antworten". Zwei Fälle sind bewusst ausgenommen, weil sie im Deutschen
richtig sind: die Auslassung („Gehorsams- und Straßenverkehrsteil") und
Aufzählungszeichen („a) … b) …").

Beim Speichern wird geprüft, ob die Frage danach überhaupt noch lösbar ist:
Auswahlfragen brauchen mindestens zwei Antworten und genau eine (bzw. mindestens
eine) richtige, Zuordnungen mindestens zwei Begriffe mit eindeutigen Schlüsseln,
Freitextfragen eine Musterlösung. Genau daran hatte es beim ersten Anlauf der
Zuordnungen gefehlt.

## Offen

- **Prüfungssimulation.** Fehlt bewusst. Die Bewertungsregel („2 Punkte je
  richtige Antwort, 2 Punkte Abzug je falsch angekreuzte, bestanden ab 70 %")
  steht auf Vereinsseiten, **nicht in einem der vier PDFs des swhv**. Der
  Jugendkatalog nennt seine Regeln selbst (15 Fragen, genau eine richtige
  Antwort), der Erwachsenenbogen nicht. Ohne die Durchführungsbestimmung würde
  die Simulation falsch rechnen — das ist schlimmer als keine Simulation.
- **Ein Detail dazu:** Im Netz heißt es, beim Erwachsenenbogen könnten mehrere
  Antworten richtig sein. In der vorliegenden Fassung hat **jede** Frage genau
  eine markierte Lösung. Entweder ist die Aussage veraltet, oder der
  Lösungsbogen bildet es nicht ab. Das Datenmodell kann beides
  (`MultipleChoice`), die Oberfläche auch.
- **Anbindung ans Ziel.** Naheliegender nächster Schritt: Wer ein Ziel „BH/VT"
  anlegt, sieht den Lernstand am Ziel („Sachkunde 62 % · 14 Fragen im
  Fehlerspeicher"). Setzt voraus, dass die Zulassungsvoraussetzungen strukturiert
  am Katalog stehen statt als Fließtext.
- **Lernstand für Trainer.** Vor einem Prüfungstermin ist „wer ist theoretisch
  so weit?" die Frage, die tatsächlich gestellt wird. Nur mit Freigabe durch die
  Mitglieder.

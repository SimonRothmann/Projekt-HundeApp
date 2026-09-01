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

Beide werden als Karte gelernt: nachdenken, Lösung aufdecken, selbst
einschätzen. Das ist ehrlicher als eine erfundene Auswahlliste — und genau so
arbeitet man mit diesen Fragen auch auf Papier. A2 zeigt fünf Körperhaltungen
als Zeichnung; ohne das Bild ist die Frage sinnlos, deshalb wird es aus dem PDF
mitgezogen.

Zwei Fallen, die das Skript kennt und deshalb prüft:

- Die Spaltenbreite im PDF wird nicht durchgehalten. „c) Aufforderung zum Spiel
  3" steht mit nur EINEM Leerzeichen da — ein zu strenges Muster übersieht die
  Zeile, und die Lösung ist still unvollständig. Ist eine Zuordnung
  durchbuchstabiert, prüft das Skript die Folge auf Lücken und bricht ab.
- Eine Fragennummer steht als `C. 3:` statt `C 3:` im Original.

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

Über richtig/falsch entscheidet bei Auswahlfragen der **Server**, nicht der
Client: die Auswahl muss die richtigen Antworten genau treffen, eine zusätzlich
angekreuzte falsche ist ein Fehler und nicht „fast richtig". Nur Zuordnung und
Freitext werden selbst eingeschätzt — dort kann niemand automatisch prüfen.

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

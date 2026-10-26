# Sachkunde-Fragentrainer

Status: **umgesetzt** (2026-09-01). Lernen, Fehlerspeicher und Neustart stehen;
die **Übungsprüfung** (Prüfungssimulation) kam am 2026-10-23 dazu (Fassung 0.39,
siehe „Übungsprüfung").

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
| Jugend | `SWHV-BHVT-JGD` | 40 | ein Block; unter 15 Jahren, 15 Fragen in der Prüfung (Übungsprüfung: 15 zufällig aus den 40, siehe dort) |

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
deployen. Die Größe der Übungsprüfung (`uebungspruefung.fragen` je Katalog) ist
keine Angabe aus den PDFs, sondern eine Festlegung des Betreibers - sie steht
deshalb als Konstante im Skript (`UEBUNGSPRUEFUNG_ERW/_JGD`) und geht beim
Neuerzeugen der JSON nicht verloren. Der `SachkundeSeeder` gleicht anhand der Fragennummer ab — er ändert
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
man in den Wochen vor der Prüfung.

### Zwei Zahlen im Lernstand

- **richtig** — Fragen, deren letzte Antwort stimmte. Bewegt sich mit jeder
  Antwort und trägt die Anzeige.
- **sitzt sicher** — Fragen ab Fach 4, also mehrfach richtig an verschiedenen
  Tagen. Braucht Zeit und steht deshalb hinten.

> Anfangs stand nur die zweite Zahl in der Oberfläche. „Sitzt sicher" verlangt
> drei richtige Antworten, und wegen der Wiedervorlage kann man die nicht am
> selben Tag geben — der Lernstand zeigte also auch nach zwanzig richtig
> beantworteten Fragen weiter **„0 von 72"**. Rechnerisch richtig, als
> Rückmeldung unbrauchbar.
>
> Dazu kam, dass die Oberfläche den Stand nur beim Laden einer Runde holte: Der
> Balken stand die ganze Runde still, selbst wenn sich etwas bewegt hätte. Jede
> Antwort bringt den Stand jetzt gleich mit.

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
| `POST /api/sachkunde/catalogs/{code}/reset` | von vorne anfangen (lässt die Prüfungsergebnisse stehen) |
| `POST /api/sachkunde/catalogs/{code}/exam` | Übungsprüfung ziehen, ohne Lösungen (anonym) |
| `POST /api/sachkunde/catalogs/{code}/exam/submit` | Übungsprüfung abgeben und werten (anonym; angemeldet wird gespeichert) |
| `GET /api/sachkunde/catalogs/{code}/exams` | die letzten 10 Ergebnisse samt Kurzbilanz „x von den letzten 5 bestanden“ (angemeldet) |

`GET /api/sachkunde/catalogs` trägt je Katalog `exam` (Fragenzahl, mögliche Punkte,
Bestehensgrenze) oder `null`.
Über richtig/falsch entscheidet der **Server**, wo er es kann: bei
Auswahlfragen muss die Auswahl die richtigen Antworten genau treffen (eine
zusätzlich angekreuzte falsche ist ein Fehler, nicht „fast richtig"), bei
Zuordnungen müssen alle Schlüssel stimmen. Nur die offenen Freitextfragen
werden selbst eingeschätzt.

Die Antwort trägt je nach Fragetyp `selectedOptionIds`, `assignments`
(Begriffs-Id → Schlüssel) oder `selfAssessedCorrect`.

## Verwaltung: Fragen von Hand überarbeiten

`/admin/sachkunde` (Admin → „Sachkunde-Fragen"). Alle 112 Fragen zum Durchsehen, mit Filter nach
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

## Übungsprüfung

Seit 2026-10-23 (Fassung 0.39). Eine Prüfungssimulation nach dem Muster der
Führerschein-Apps: Fragen zum Ankreuzen am Stück, **keine Auflösung zwischendurch**,
Auswertung erst bei der Abgabe.

### Warum jetzt, und warum „Übung“

Bisher fehlte sie bewusst: Die Wertungsregel („2 Punkte je richtige Antwort, 2 Punkte
Abzug je falsch angekreuzte, bestanden ab 70 %“) steht auf Vereinsseiten, **nicht in einem
der vier PDFs des swhv** - ohne die Durchführungsbestimmung rechnete die Simulation
womöglich falsch. Der Betreiber hat am 2026-10-23 entschieden, die verbreitete Regel
zu nehmen, wenn es **deutlich als Übung** gekennzeichnet ist. Daher:

- Vor dem Start und auf dem Ergebnis steht: „Übungsprüfung – keine offizielle Prüfung.
  Gewertet nach der verbreiteten Regel: 2 Punkte je richtige Antwort, 2 Punkte Abzug je
  falsch angekreuzte, bestanden ab 70 %. Dein Verband oder Verein kann anders werten.“
- Das Ergebnis heißt **„Übung bestanden“ / „Übung nicht bestanden“**, nie nur „Bestanden“.

### Regeln (Betreiber-Entscheidungen 2026-10-23)

| | |
|---|---|
| Umfang | Erwachsene **25** Fragen, Jugend **15** (zufällig aus den 40) |
| Bestanden | ab **70 %** der möglichen Punkte, ganzzahlig gerechnet (`punkte * 100 >= max * 70`): Erwachsene 35 von 50, Jugend 21 von 30 |
| Punkte | **2 je richtig angekreuzter Antwort, 2 Abzug je falsch angekreuzter** |
| Mindestwert | eine Frage bringt **nie weniger als 0** (kein Minus je Frage) |
| Höchstwert | 2 je richtiger Antwort der Frage - heute immer 2, weil jede Frage genau eine hat; die Rechnung ist allgemein, falls künftig eine Frage mehrere hat |
| Zuordnung | zählt wie eine Frage: 2 Punkte, wenn **alle** Begriffe stimmen, sonst 0 (wie im Lernmodus „nur ganz“) |
| Freitext | D5, D6, D8 kommen **nicht** vor - sie lassen sich nicht automatisch werten |
| Zeit | keine Begrenzung (es gibt keine Quelle dafür); die Oberfläche zeigt die vergangene Zeit dezent |

**Annahmen und was sie tragen.** Die Punktregel ist die von Vereinsseiten, nicht aus den
swhv-PDFs. Die 15 Fragen der Jugendprüfung folgen der Katalogbeschreibung des Herausgebers
(15 Fragen für unter 15-Jährige, zufällig aus den 40; der Betreiber hat „alle 40“ verworfen);
die Beschreibung bleibt unverändert. Die Zahl steht deshalb als **Daten** am Katalog
(`quiz_catalogs.ExamQuestionCount`, aus `sachkunde-swhv.json` → `uebungspruefung.fragen`,
vom Seeder bei jedem Start gesetzt) und lässt sich ohne Codeänderung anpassen. Der
Mindestwert 0 je Frage ist ebenfalls eine Festlegung: Ohne ihn könnte eine einzelne
Fehlentscheidung mehr kosten, als die Frage wert war. Das Datenmodell kennt
`MultipleChoice`; sollte eine Frage mehrere richtige Antworten bekommen, rechnet die
Wertung ohne Änderung weiter.

### Welche Fragen drankommen

Reine Funktion `SachkundePruefung.WaehleFragen` (Zufall als Parameter):

1. nur wertbare Fragen (SingleChoice, MultipleChoice, Assignment; nicht gelöscht);
2. verlangt der Katalog mehr, als wertbar sind (Testfall mit konstruierten Daten), kommen alle dran;
3. sonst **anteilig nach Komplex** (Verfahren der größten Reste; Jugend: ein Block, also 15 zufällig aus 40, Gleichstand zugunsten des
   früheren Komplexes - die Verteilung hängt nicht vom Zufall ab), innerhalb des Komplexes
   zufällig. Erwachsene: wertbar A 23 · B 19 · C 10 · D 10 · E 7 → **A 8 · B 7 · C 4 · D 4 · E 2**
   (als Test festgehalten, auch gegen die echten Katalogdaten);
4. die Reihenfolge in der Prüfung ist gemischt, die der Antworten bleibt die des Katalogs (a/b/c).

### Ablauf und Server

- `POST …/exam` zieht die Prüfung und liefert sie **ohne Lösungen**: keine `isCorrect`-Kennzeichen,
  keine Musterlösung, bei Zuordnungen nur die wählbaren Schlüssel. Dazu Fragenzahl, mögliche
  Punkte, Bestehensgrenze. Katalog ohne Übungsprüfung: 404.
- `POST …/exam/submit` nimmt je Frage `questionId` + `selectedOptionIds` bzw. `assignments`
  (leere Auswahl = unbeantwortet, 0 Punkte), wertet **serverseitig** und liefert Punkte, Höchstpunkte,
  bestanden, Punkte je Komplex und je Frage richtig/falsch samt richtiger Lösung.
- Der Server **merkt sich die gezogene Prüfung nicht** (kein Zustand, der bei anonymer Nutzung
  aufzuräumen wäre). Er prüft stattdessen die Form der Abgabe: richtige Anzahl (Prüfungsgröße, bzw.
  alle wertbaren, falls weniger), nur Fragen dieses Katalogs, keine doppelt, nichts Unwertbares,
  Antworten gehören zur jeweiligen Frage - sonst 400 mit verständlicher Meldung. Wer sich die
  leichtesten Fragen aussucht, betrügt nur sich selbst; die Zusammenstellung (Komplexverteilung) wird
  deshalb nicht erzwungen.
- **Anonym**: wird gewertet, nichts gespeichert. **Angemeldet**: das Ergebnis wird gespeichert
  (`quiz_exam_attempts`), und **jede nicht voll richtig beantwortete Frage** (auch eine
  unbeantwortete) kommt in den **Fehlerspeicher** - gleiche Wirkung wie eine falsche Antwort im
  Lernmodus (Fach 1, sofort wieder fällig). **Richtig beantwortete ändern den Lernstand nicht**: Eine
  Prüfung soll nicht nebenbei Fächer hochstufen, und nach einer Prüfung ohne Wiederholung hätte
  „richtig“ nichts bewiesen.
- Die beiden anonymen Endpunkte sind **gedrosselt** (Richtlinie `pruefung`, 30 je Minute und IP, im
  Muster von `invite`): anders als die anonymen Lesezugriffe laden sie bei jedem Aufruf den ganzen
  Katalog (72 bzw. 40 Fragen mit Antworten). Was sich ohne Katalog entscheiden lässt (leere,
  überlange oder doppelte Abgabe), wird **vor** dem Laden abgewiesen.
- Der Verlauf: **eigener Endpunkt** `GET …/exams` statt eines Feldes im Lernstand. Der Lernstand wird
  bei jeder einzelnen Antwort neu berechnet und mitgeschickt; ein Prüfungsfeld dort kostete bei jeder
  Antwort eine zusätzliche Abfrage für etwas, das sich nur nach einer Prüfung ändert. Der Endpunkt
  liefert die letzten 10 Ergebnisse (neueste zuerst) und die Kurzbilanz „x von den letzten 5
  bestanden“ (`passedOfLastFive`/`lastFiveCount`) - die Grundlage für das spätere Paket
  „Prüfungsreife“.

**Doppelabgabe geschlossen (Fassung 0.40, Paket PR1).** Das Ziehen (`POST …/exam`) liefert zusätzlich eine
zufällige **Abgabe-Kennung** (`submissionId`, Guid, vom Server erzeugt, nicht gespeichert); die Abgabe schickt sie
mit (im Browser mit dem Stand der laufenden Prüfung im `sessionStorage`, sie überlebt also ein Neuladen).
Angemeldet wird sie am Ergebnis gespeichert (`quiz_exam_attempts.SubmissionId`, eindeutiger Index
`(UserId, SubmissionId)` mit Filter „Kennung gesetzt und nicht weich gelöscht"). Kommt dieselbe Kennung desselben
Nutzers noch einmal, liefert der Server das **gespeicherte** Ergebnis (Punkte, Bestehen, Komplexe aus der Speicherung;
die Lösung je Frage aus der erneuten Wertung) und fasst den Fehlerspeicher nicht erneut an. Zwei gleichzeitige
Abgaben: Die Unique-Verletzung (SQLSTATE 23505) wird abgefangen, die Transaktion der langsameren hat nichts
gespeichert, sie liefert das Ergebnis der schnelleren - kein 500. Fehlt die Kennung (alte Clients während des
Ausrollens, vor dem Ausrollen gezogene Prüfung), wird wie bisher angenommen. Anonym ändert sich nichts. Die Kennung
ist ein technisches Merkmal am vorhandenen Ergebnis, keine neue Datenart (siehe `docs/VERARBEITUNGSVERZEICHNIS.md`).

### Gespeicherte Daten

Neue Entität `QuizExamAttempt` (`quiz_exam_attempts`): Nutzer, Katalog, Zeitpunkt, Punkte, mögliche
Punkte, bestanden, Zahl der Fragen und der voll richtigen, Punkte je Komplex als JSON-Spalte
(begründet im Code und in `DATABASE.md`). **Die einzelnen Antworten werden nicht gespeichert.** Weicher
Löschfilter wie überall; Datenexport („Uebungspruefungen“) und Kontolöschung (endgültig) sind in
`AccountDataService` berücksichtigt, Datenschutzerklärung (deutsch und englisch) und
`VERARBEITUNGSVERZEICHNIS.md` nachgezogen.

**„Lernstand zurücksetzen“ (`…/reset`) löscht die Prüfungsergebnisse nicht.** Begründung: Das
Zurücksetzen gehört zum Lernen („ich fange die Karten von vorne an“) - die Ergebnisse sind ein Verlauf
(„wie lief es zuletzt?“), und wer von vorne lernt, will gerade sehen, ob er besser wird. Sie gehen mit
der Kontolöschung.

### Oberfläche

Auf der Katalogseite (`/sachkunde/[code]` öffentlich, `/lernen/[code]` in der App) steht über dem
Trainer eine Karte „Übungsprüfung“ mit der Kurzinfo („25 Fragen · bestanden ab 35 von 50 Punkten“),
angemeldet mit dem letzten Ergebnis. Danach: Startseite mit Hinweis → eine Frage je Bildschirm
(Erwachsene: Mehrfachauswahl wie auf dem Papierbogen, **ohne Hinweis, wie viele richtig sind**; Jugend:
genau eine, weil der Katalog das selbst sagt; Zuordnungen mit `quiz-assignment.tsx` im Prüfmodus ohne
Sofortprüfung; Bildfragen mit Bild) → Vor/Zurück, Fragenleiste (umbrechend, beantwortet/offen
markiert), „Abgeben“ mit Rückfrage bei offenen Fragen → Ergebnis mit großem „Übung bestanden“ /
„Übung nicht bestanden“, Punkten, Balken je Komplex, den falschen Fragen mit eigener und richtiger
Antwort, „Falsche Fragen üben“ (angemeldet: Modus Fehler; anonym: Hinweis auf die Anmeldung) und
„Neue Übungsprüfung“; angemeldet der Verlauf.

Eine **laufende Prüfung übersteht ein Neuladen** (`sessionStorage`, nur Fragen und bisherige
Antworten, nichts im URL; mit `try/catch` gegen gesperrten Speicher). Nach der Abgabe wird der Stand
gelöscht, damit ein Neuladen die Prüfung nicht zum zweiten Mal öffnet - das Ergebnis steht dann (nur
angemeldet) im Verlauf. Weil die öffentliche Katalogseite statisch gebaut und 24 Stunden
zwischengespeichert wird (`lib/public-sachkunde.ts`), kann ihre Katalogantwort aus der Zeit
vor dem Ausrollen stammen und das Feld `exam` gar nicht kennen (fehlt es, anders als `null` = „keine
Übungsprüfung“): dann fragt `KatalogBereich` die Kataloge selbst nach. Die Reihenfolge
Backend/Frontend beim Ausrollen ist damit egal; nur das Backend muss die Übungsprüfung kennen,
damit der Einstieg erscheint.

## Offen

- **Offizielle Wertung.** Die Übungsprüfung rechnet nach der auf Vereinsseiten verbreiteten Regel
  (siehe „Übungsprüfung“), nicht nach einer Durchführungsbestimmung des swhv. Liegt sie vor, ist es
  eine Änderung der Konstanten in `SachkundePruefung` (Punkte, Prozent) und der Texte - dann kann das
  „Übung“ aus dem Ergebnis verschwinden.
- **Ein Detail dazu:** Im Netz heißt es, beim Erwachsenenbogen könnten mehrere Antworten richtig sein.
  In der vorliegenden Fassung hat **jede** Frage genau eine markierte Lösung. Entweder ist die Aussage
  veraltet, oder der Lösungsbogen bildet es nicht ab. Das Datenmodell kann beides (`MultipleChoice`),
  die Oberfläche und die Wertung der Übungsprüfung auch.
- ~~**Prüfungsreife** / **Anbindung ans Ziel**~~ erledigt (Fassung 0.40, Paket PR1, siehe
  `docs/SMART_TRAINING_PLAN.md` „Prüfungsreife"): Die Prüfungsordnung trägt das Feld
  `Regulation.TheoryCatalogCode` (gesetzt nur für die FCI-BH/VT, deren Ablauf den Sachkundenachweis enthält; nicht für
  Ordnungen, die eine bestandene BH/VT nur als Zulassung verlangen, und nicht für die österreichische und die
  Schweizer BH - dort richtet sich der Nachweis nach Landesrecht bzw. eigener Ordnung, und unser Katalog ist der des
  swhv). Am Ziel erscheint eine Theoriezeile (sicher = letzte 2 Übungsprüfungen bestanden, wackelig = abgelegt, aber
  nicht beide bestanden, offen = keine), nur für die ansehende Person selbst, und nur, wenn sie die Sachkunde nutzen kann (Land Deutschland, Modul nicht abgeschaltet).
- **Lernstand für Trainer.** Vor einem Prüfungstermin ist „wer ist theoretisch
  so weit?" die Frage, die tatsächlich gestellt wird. Nur mit Freigabe durch die
  Mitglieder.

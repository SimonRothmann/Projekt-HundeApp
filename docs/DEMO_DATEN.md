# Demo-Daten und Demo-Szenarien

Status: **umgesetzt** (2026-09-30, ergänzt 2026-10-16: Englisch, Mitbesitz, Österreich,
Vereinsantrag, Hoopers, Nachtfährte; 2026-10-18: Schweiz; 2026-10-22: Trainingspausen). Gilt nur für Development (lokal und die
Test-Umgebung); Produktion bekommt nichts davon.

## Zwei Seeder, zwei Aufgaben

- `DemoDataSeeder` legt die Grundlage an: fünf Konten, den Demo-Verein, die
  Welpengruppe, einen Hund je Person. Er läuft **einmal** und bricht ab, sobald
  `trainer@dogity.test` existiert. Auf der Test-Umgebung gibt es die Konten seit
  Juli, deshalb kommt dort nichts mehr an, was man ihm hinzufügt.
- `DemoSzenarienSeeder` ergänzt die Konten um Szenarien, mit denen sich **alle
  Funktionen** von Hand ausprobieren lassen. Er läuft bei **jedem Start**
  (`Program.cs`, nur Development, nach dem `DemoDataSeeder`).

Der Szenarien-Seeder:

- legt je Szenario nur Fehlendes an. Erkannt wird es an festen Merkmalen (Notiztext
  eines Ziels, Kommentar einer Fährte, Telefonnummer plus Hundename einer Anmeldung).
  Zweimal starten erzeugt also nichts doppelt.
- fasst nur die Demo-Konten und den Demo-Verein an; fehlt ein Konto, entfällt das
  Szenario. Ohne Demo-Konten (Produktion) passiert gar nichts.
- geht über dieselben Dienste wie die App (Rechte, Benachrichtigungen,
  Fährten-Auswertung durch den `GpsTrackEvaluator`) statt Kennzahlen selbst zu setzen.
- legt **Termine rollend** an: Bei jedem Start gibt es wieder künftige Dienstage.
- legt nichts wieder an, was ein Tester in der Demo gelöscht oder entschieden hat (Ziele,
  Trainings, Fährten, Anmeldungen, Hunde). Eine abgelehnte oder freigegebene
  Beitrittsanfrage von Nina Neuling kommt ebenfalls nicht zurück, ebenso wenig
  eine abgelehnte oder zurückgezogene Mitbesitz-Einladung oder ein entschiedener
  Vereinsantrag. Nur Termine und die Nachtfährte sind davon (teilweise) ausgenommen,
  weil sie rollend nachwachsen.
- setzt **Einstellungen nur, wenn sie leer sind**: Lisas Sprache und Toms Land
  entstehen nur, solange die Person noch nichts gewählt hat. Was ein Tester im
  Profil auf eine andere Sprache bzw. ein anderes Land umstellt, bleibt. Die
  Rückstellung auf „Automatisch“ bzw. „Keine Angabe“ gilt dagegen als „leer“: Beim
  nächsten Start steht wieder Englisch bzw. Österreich da.
- hält die **Nachtfährte rollend**: Sie hat keinen Ablauf und liegt auf „gestern Abend“
  (22 Uhr Ortszeit). Solange niemand sie abgelaufen hat und ihr Tag nicht „gestern“ ist,
  rückt sie beim Start auf gestern 22 Uhr (Datum der Einheit und alle Punktzeiten um
  dieselbe Differenz). Ist sie abgelaufen oder gelöscht, bleibt sie, wie sie ist. Fasst
  sie nur an, was erkennbar dem Seeder gehört: Hat Bello gestern schon ein Training
  (App-Regel: ein Tag, eine Einheit), wird sie nicht angelegt; trägt ein Tester in ihrer
  Einheit etwas ein (Übung, Notiz, Dauer, Feedback) oder liegt am Zieltag schon eine
  andere Einheit, wird sie nicht verschoben.
  Gedacht zum Testen am Tag nach einem Neustart: „Erneut ablaufen“ gilt bis 24 Stunden
  nach dem Ende des Legens, nach 22 Uhr am Folgetag ist die Grenze überschritten.
- hält die **Läufigkeit von Ninas Hündin rollend**: Sie beginnt vor fünf Tagen und endet
  21 Tage nach ihrem Beginn (erkannt an der festen Notiz). Rollend, weil sie sonst nach gut zwei Wochen vorbei
  wäre und auf der Test-Umgebung nirgends mehr eine laufende Pause zu sehen wäre. Ist sie
  genau bis zu ihrem Ende durchgelaufen, beginnt beim nächsten Start eine neue. Eine gelöschte,
  vorzeitig beendete oder geänderte Pause gilt als Entscheidung des Testers und kommt nicht
  zurück; ebenso weicht der Seeder einer eigenen Pause im selben Zeitraum. Läufig kann nur eine
  Hündin sein: hat Nina nur einen Rüden, entfällt das Szenario. Die Verletzungspause bei Bello
  entsteht einmal (wie die Ziele) und liegt in der Vergangenheit.
- ruft für die Fährten keinen Wetterdienst auf und reiht keinen Wetterauftrag ein (der Test
  `Seeder_FragtKeinWetterAnUndReihtKeinesEin` prüft das mit der echten Anreicherung); die
  Temperaturen stehen in denselben Feldern wie beim Wetter-Feature.
- rechnet „heute“ in der Vereinszeit (Europe/Berlin), nicht in UTC.
- darf keine Kennzahl verfälschen: Dem Admin-Konto legt er nichts an.

Ein fehlgeschlagenes Szenario wird protokolliert und stoppt weder die anderen noch
den Start des Servers.

## Welches Demo-Konto zeigt welche Funktion

Passwort für alle Konten: `Demo1234!` (Knöpfe auf der Login-Seite der Test-Umgebung; die
Beschriftung nennt die Besonderheit, z. B. „Mitglied 2 (EN)“, „Interessent (AT)“ und „Mitglied (CH)“).

| Konto | Was dort zu sehen ist |
| --- | --- |
| **Admin** (`admin@dogity.test`) | Nutzerverwaltung, Kennzahlen (`/api/admin/stats`), verwaiste Daten. Hat bewusst keine eigenen Demo-Daten. |
| **Trainer** (`trainer@dogity.test`) | Einladungslink des Vereins; offene Beitrittsanfragen von Tom Neuling (von Hand) und Nina Neuling (über den Link, mit Benachrichtigung); Welpengruppe mit Anmeldeformular-Link und sieben Anmeldungen (Formular, Import, von Hand; Wurftage 8 bis 20 Wochen; eine mit Notiz; vier bezahlt; Anwesenheit 0 bis 3 Mal; die jüngste von heute, mit Benachrichtigung); Gruppentermine (zwei künftige, einer davon an anderem Treffpunkt, ein abgesagter, drei vergangene); Absage von Lisa mit Benachrichtigung; Feedback zu Bellos Trainings und die Rückfrage von Max mit Benachrichtigung. |
| **Mitglied 1** (`mitglied1@dogity.test`, Max) | **Bello:** eine **Nachtfährte** ohne Ablauf, gestern 22 Uhr gelegt („Erneut ablaufen“ über Mitternacht, rollend; siehe oben), dazu vier Winkelfährten mit je einem ausgewerteten Ablauf (drei Gegenstände, Abweichung sinkt von Fährte zu Fährte, Temperaturen beim Legen und Suchen), frisches Trainer-Feedback ohne Reaktion (Benachrichtigung führt auf den Eintrag), Feedback mit „Verstanden" und Rückfrage. **Emma** (zweiter Hund): zwei erreichte Prüfungen (BH ohne Punkte, FCI-UPr 1 mit 86 Punkten) als Leistungen, ein aktives Ziel FCI-UPr 2 in rund fünf Wochen mit Plan und angefangener Woche (Zielkarte auf der Startseite). **Wauwau:** ein Ziel FCI-FPr 1 mit Prüfungsdatum gestern (Zustand „Ergebnis eintragen") - bewusst nicht bei Emma, weil die Startseite je Hund nur das nächste Ziel zeigt und ein überfälliges Vorrang hat. Beim nächsten Gruppentermin zugesagt. Emma ist außerdem der Hund der **Mitbesitz-Einladung an Lisa** (Max ist der Einladende). **Bello** hat außerdem eine überstandene **Verletzungspause** (vor rund zehn Wochen, 14 Tage) im Verlauf unter „Verwalten → Trainingspause“. |
| **Mitglied 2** (`mitglied2@dogity.test`, Lisa) | **Englische Oberfläche** bei Land Deutschland: Glocke, Push und Mails auf Englisch, Prüfungsordnungen weiter deutsch. Dazu die **Mitbesitz-Einladung** von Max für Emma (Einladungskarte, Benachrichtigung auf Englisch; Mitbesitz erst nach ihrer Zusage). Hund (Luna) ohne Prüfungsziel; beim nächsten Gruppentermin abgesagt. |
| **Interessent** (`interessent@dogity.test`, Tom) | Land **Österreich** (AT): Katalog mit den FCI- und den ÖKV-Ordnungen (BH/VT, Agility, Hoopers, Breitensport), ohne VDH-Ordnungen und ohne Turnierhundsport, Sachkunde ausgeblendet, Tagebuch und Fährte funktionieren. Stellt einen **Vereinsantrag** „Hundefreunde Wien“, den der Admin unter Vereine freigibt oder ablehnt. Kein Verein, offene Beitrittsanfrage; Dashboard-Hinweis „Tritt einem Verein bei". |
| **Mitglied (CH)** (`schweiz@dogity.test`, Sandro Schweizer) | Land **Schweiz** (CH, nur gesetzt, solange noch keins gewählt ist): Katalog mit den FCI- und den SKG-Ordnungen, ohne VDH-Ordnungen. Hund „Bläss“ (Appenzeller Sennenhund; hat Sandro schon einen Hund, bekommt der das Ziel) mit Ziel **SKG BH 1** (Sportart BHCH, Begleithund der SKG) und Plan, Prüfung in rund zwei Monaten. Keine Vereinszugehörigkeit. Das Konto entsteht nur, wenn es fehlt. |
| **Neuling** (`neuling@dogity.test`, Nina) | **Hoopers:** Hund „Flitzer“ (Mischling, gut ein Jahr; hat Nina schon einen Hund, bekommt der das Ziel) mit Ziel H0 und Plan; die Prüfung liegt nach dem 1.1.2027, dem Beginn der Ordnung. Ebenfalls offene Beitrittsanfrage, aber über den Einladungslink (Quelle „Einladungslink"). Zeigt die Wartesicht nach einem Beitritt per Link. **Flitzer ist läufig** (seit fünf Tagen, Ende 21 Tage nach dem Beginn; Band auf der Hundeseite, Pause statt Wochenfortschritt auf der Zielkarte, Plan steht still). Die Trainer:in sieht Flitzer nur, wenn sie ihn betreut - dann „Pause bis …“ ohne Grund. |

Der Link selbst: als Trainer unter dem Verein, Vorschau anonym unter `/v/<code>`;
das Anmeldeformular der Welpengruppe unter `/anmeldung/<code>`.

## Erinnerungen (seit Fassung 0.37)

Die automatischen Erinnerungen (Termin am Vortag, Fährte ohne Ablauf, offene Rückmeldungen für
Trainer:innen) entstehen auch für Demo-Konten - sie brauchen keine eigenen Demo-Daten. Beispiele: Die
Nachtfährte von Mitglied 1 (gestern 22 Uhr gelegt, ohne Ablauf) löst am nächsten Morgen ab 8 Uhr
die Erinnerung aus, solange die 24 Stunden laufen; die Gruppenmitglieder bekommen am Vorabend ab 18 Uhr
„Morgen …: Welpengruppe“ zum nächsten Gruppentermin (Lisa hat abgesagt: nichts); der Trainer bekommt sonntags
um 18 Uhr die Zusammenfassung der Trainings, die auf Rückmeldung warten. Wer das Verhalten ausprobiert,
findet die Meldungen in der Glocke; Push nur, wenn auf dem Gerät eingeschaltet.

## Prüfen

- Tests: `DemoSzenarienSeederTests` (zweimal ausführen = keine Duplikate, ohne
  Demo-Konten passiert nichts, alle Szenarien sind da, gespeicherte Sprache und
  gespeichertes Land bleiben, Entschiedenes kommt nicht zurück, das Schweizer Konto
  mit Land, Hund und Ziel SKG BH 1 (ein umgestelltes Land und ein gelöschtes Ziel bleiben so), die Nachtfährte
  rückt auf gestern und bleibt bei Ablauf oder fremden Einträgen liegen, bei einem
  schon vorhandenen Training gestern entsteht sie nicht, ein anderer offener
  Vereinsantrag von Tom wird still übergangen).
- Trainingspausen: `DemoSzenarienSeederTests.Trainingspausen_*` (Flitzer läufig und Bello verletzt,
  rollend nach dem natürlichen Ende, vorzeitig Beendetes und Gelöschtes kommt nicht zurück, eigene
  Pause des Testers geht vor, bei einem Rüden keine Läufigkeit).
- Lokal: Backend zweimal starten und die Zeilenzahlen vergleichen; über die API
  sichtbar sind die Daten z. B. unter `/api/dashboard` (als Mitglied 1) und
  `/api/notifications` (als Trainer).
- `scripts/e2e-test.py` legt eigene Daten mit dem Präfix `e2e-` an und rührt die
  Demo-Szenarien nicht an. Sein Vorher-/Nachher-Vergleich stört sich nicht an ihnen,
  weil der Seeder nur beim Start läuft.

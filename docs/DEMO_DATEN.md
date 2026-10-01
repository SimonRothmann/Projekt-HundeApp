# Demo-Daten und Demo-Szenarien

Status: **umgesetzt** (2026-09-30). Gilt nur für Development (lokal und die
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
- legt nichts wieder an, was ein Tester in der Demo gelöscht hat (Ziele, Trainings,
  Fährten, Anmeldungen). Eine abgelehnte oder freigegebene Beitrittsanfrage von
  Nina Neuling kommt ebenfalls nicht zurück. Nur Termine sind davon ausgenommen,
  weil sie rollend nachwachsen.
- ruft für die Fährten keinen Wetterdienst auf; die Temperaturen stehen in denselben
  Feldern wie beim Wetter-Feature.
- darf keine Kennzahl verfälschen: Dem Admin-Konto legt er nichts an.

Ein fehlgeschlagenes Szenario wird protokolliert und stoppt weder die anderen noch
den Start des Servers.

## Welches Demo-Konto zeigt welche Funktion

Passwort für alle Konten: `Demo1234!` (Knöpfe auf der Login-Seite der Test-Umgebung).

| Konto | Was dort zu sehen ist |
| --- | --- |
| **Admin** (`admin@dogity.test`) | Nutzerverwaltung, Kennzahlen (`/api/admin/stats`), verwaiste Daten. Hat bewusst keine eigenen Demo-Daten. |
| **Trainer** (`trainer@dogity.test`) | Einladungslink des Vereins; offene Beitrittsanfragen von Tom Neuling (von Hand) und Nina Neuling (über den Link, mit Benachrichtigung); Welpengruppe mit Anmeldeformular-Link und sieben Anmeldungen (Formular, Import, von Hand; Wurftage 8 bis 20 Wochen; eine mit Notiz; vier bezahlt; Anwesenheit 0 bis 3 Mal; die jüngste von heute, mit Benachrichtigung); Gruppentermine (zwei künftige, einer davon an anderem Treffpunkt, ein abgesagter, drei vergangene); Absage von Lisa mit Benachrichtigung; Feedback zu Bellos Trainings und die Rückfrage von Max mit Benachrichtigung. |
| **Mitglied 1** (`mitglied1@dogity.test`, Max) | **Bello:** vier Winkelfährten mit je einem ausgewerteten Ablauf (drei Gegenstände, Abweichung sinkt von Fährte zu Fährte, Temperaturen beim Legen und Suchen), frisches Trainer-Feedback ohne Reaktion (Benachrichtigung führt auf den Eintrag), Feedback mit „Verstanden" und Rückfrage. **Emma** (zweiter Hund): zwei erreichte Prüfungen (BH ohne Punkte, FCI-UPr 1 mit 86 Punkten) als Leistungen, ein aktives Ziel FCI-UPr 2 in rund fünf Wochen mit Plan und angefangener Woche, ein Ziel mit Prüfungsdatum gestern (Zustand „Ergebnis eintragen"). Beim nächsten Gruppentermin zugesagt. |
| **Mitglied 2** (`mitglied2@dogity.test`, Lisa) | Hund (Luna) ohne Prüfungsziel; beim nächsten Gruppentermin abgesagt. |
| **Interessent** (`interessent@dogity.test`, Tom) | Kein Verein, offene Beitrittsanfrage; Dashboard-Hinweis „Tritt einem Verein bei". |
| **Neuling** (`neuling@dogity.test`, Nina) | Ebenfalls offene Beitrittsanfrage, aber über den Einladungslink (Quelle „Einladungslink"). Zeigt die Wartesicht nach einem Beitritt per Link. |

Der Link selbst: als Trainer unter dem Verein, Vorschau anonym unter `/v/<code>`;
das Anmeldeformular der Welpengruppe unter `/anmeldung/<code>`.

## Prüfen

- Tests: `DemoSzenarienSeederTests` (zweimal ausführen = keine Duplikate, ohne
  Demo-Konten passiert nichts, alle Szenarien sind da).
- Lokal: Backend zweimal starten und die Zeilenzahlen vergleichen; über die API
  sichtbar sind die Daten z. B. unter `/api/dashboard` (als Mitglied 1) und
  `/api/notifications` (als Trainer).
- `scripts/e2e-test.py` legt eigene Daten mit dem Präfix `e2e-` an und rührt die
  Demo-Szenarien nicht an. Sein Vorher-/Nachher-Vergleich stört sich nicht an ihnen,
  weil der Seeder nur beim Start läuft.

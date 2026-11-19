# Gastzugang: App ausprobieren ohne Anmeldung

Status: **umgesetzt** (2026-11-19), noch nicht auf Produktion.

## Warum

Wer Dogity nur ansehen will, gibt ungern gleich E-Mail und Passwort her - und
eine leere App zeigt nicht, was sie kann. Der Knopf „App ausprobieren - ohne
Anmeldung" (Startseite und Anmeldeseite) legt deshalb ein **Gastkonto mit
fertigen Beispieldaten** an und öffnet die App. Nach **vier Stunden** ist alles
samt Konto gelöscht.

## Was ein Gast bekommt

- einen Hund (Ayla, Malinois) mit rund drei Wochen Trainings samt Übungen und
  Bewertungen, einem Ziel mit Trainingsplan und einer ausgewerteten Fährte
- einen **erfundenen Verein** („Hundesportverein Musterstadt (Demo)"), in dem
  der Gast **Trainer:in mit Verwaltung** ist: fünf erfundene Mitglieder (je mit
  Hund), die Gruppe „Junghunde" mit Mitgliedern und einem kommenden Termin und
  eine offene Beitrittsanfrage, damit sich das Freigeben ausprobieren lässt

Die Inhalte entstehen über dieselben Dienste wie in der App (Trainings, Ziele,
Fährten-Auswertung, Gruppen, Terminplanung); nur Hund, Mitglieder und Verein
werden direkt angelegt (`GastkontoService`). Rollen, Plan und Statistik des Gastes
verhalten sich damit wie bei einem echten Konto.

## Wie es technisch hängt

- `users.GuestUntil` und `clubs.GuestUntil` (beide leer = echt). Daran erkennen
  Abschottung, Statistik und Löschdienst, was zum Gast gehört.
- Die **erfundenen Mitglieder** sind ebenfalls Gastkonten, aber ohne Passwort:
  Niemand kann sich bei ihnen anmelden. Auch der Gast selbst hat keins - er
  bekommt seine Sitzung direkt vom Server (`POST /api/auth/guest`, Antwort wie
  beim Login). E-Mail-Adressen aller Gastkonten: `gast-{zufall}@gast.invalid`.
- Ein Gast ist ein Konto mit den Rollen USER und TRAINER, die Einführung
  (Erststart) gilt als erledigt.
- Die Anlage ist eng gedrosselt und hat eine **Obergrenze gleichzeitig aktiver
  Gäste**; ist sie erreicht, antwortet der Server mit 503 und einem Hinweis. Die
  Zahlen stehen im Code (`Program.cs`, `Gastkonto.MaxAktiv`), nicht hier.
- `KontoStatus` behandelt ein abgelaufenes Gastkonto sofort wie ein gelöschtes
  (401), auch bevor der Löschdienst es entfernt hat. Das Frontend landet dann auf
  der Anmeldeseite.

## Abschottung: Gäste und echte Konten sehen einander nie

- **Vereinsliste:** Echte Konten sehen keinen Gast-Verein; ein Gast sieht nur
  seinen eigenen. Die Admin-Vereinsliste kennt keine Gast-Vereine.
- **Konten finden oder verknüpfen** (Mitglied oder Trainer:in zum Verein oder zur
  Gruppe hinzufügen, Mitbesitz eines Hundes): `IUserLookupService.FindByEmailAsync`
  bekommt die suchende Person mit und findet nur Konten derselben Seite. Alles
  andere antwortet wie bei einer unbekannten Adresse - die Antwort verrät nicht,
  ob es sie gibt.
- **Beitritt per Verein oder Gruppe** über die Grenze: „nicht gefunden".
- Gästen nicht erlaubt: E-Mail und Passwort ändern, Konto löschen (es löscht sich
  selbst), Vereinsantrag stellen, Einladungs- und Anmeldelink des Gast-Vereins
  erzeugen (er wäre von außen erreichbar). Antwort: „Im Demo-Modus nicht möglich."
- **Mail:** Die Mail-Warteschlange nimmt keine Mail an `@gast.invalid` an.
  Benachrichtigungen in der App (Glocke) gibt es weiter.
- Öffentliche Seiten (Sitemap, Prüfungsordnungen, Anmeldeformulare) zeigen
  keine Vereine oder Gruppen eines Gastes.

## Keine Statistik zählt Gäste

Admin-Kennzahlen, „Nutzung" und Nutzerliste lassen Gastkonten, ihre Hunde,
Trainings, Fährten und die Gast-Vereine mit ihren Gruppen aus
(`AdminService`, `AdminUsageService`, `UserLookupService`,
`RefreshTokenService.CountAccountsWithTokenSinceAsync`). Der Warmstart wählt
seine Beispieldaten nicht von einem Gast. Die „Daten gelöschter Konten"
(`VerwaisteKontodaten`) kennen Gastkonten dagegen als vorhanden - sonst
hielten sie deren Daten für verwaist.

## Löschen

`GastkontoBackgroundService` läuft alle zehn Minuten (und gleich nach dem Start):

1. Gastkonten mit abgelaufenem `GuestUntil`: dieselbe Folge wie beim Löschen
   eines Kontos (`PurgeAsync`, Konto, Anmelde-Tokens, `KontoStatus`).
2. Gast-Vereine mit abgelaufenem `GuestUntil`: Gruppen, Termine, Mitglieder,
   Rollen, Übungen und alles, was am Verein hängt - echtes Löschen, keine
   Soft-Delete-Reste.

Ein Lauf, der teilweise scheitert, wird beim nächsten Mal fortgesetzt. Geprüft
auf einer echten Postgres-Datenbank: Nach dem Löschlauf stimmen die
Zeilenzahlen aller Tabellen wieder mit dem Stand vor der Anlage überein.

## Offen

- **Gast in echtes Konto übernehmen:** bewusst nicht Teil von G1. „Eigenes Konto
  anlegen" meldet den Gast ab und öffnet die Registrierung; die Demo-Daten
  gehen mit dem Ablauf verloren.
- **Datenschutzerklärung und Verarbeitungsverzeichnis** nennen den
  Gastzugang noch nicht; sie brauchen vor Produktion das OK des Betreibers.
- **Erinnerungen** (`ErinnerungBackgroundService`) und die Plan-Neuberechnung
  laufen für Gastkonten wie für alle; bei sehr vielen Gästen kostet das
  Rechenzeit.

# Verbände, Sprachen, Module

Analyse zu drei Wünschen, die zusammen gehören: Vereine sollen sich selbst
verwalten, die App soll wenigstens englisch können, und Nutzer:innen sollen
Sportarten und Funktionen ab- und anwählen können.

Stand der Untersuchung: 2026-09-03, Commit `82d9fc6`.

---

## Der gemeinsame Kern

Die drei Wünsche sehen unabhängig aus, haben aber dieselbe Ursache. Die App
geht heute an genau einer Stelle von etwas aus, das nicht mehr stimmt:

> Es gibt **einen** Verband (SWHV/VDH), **einen** globalen Katalog und
> **eine** Person, die ihn pflegt.

Daraus folgt alles Weitere:

- Vereine können sich nicht selbst verwalten, weil "Verein anlegen" beim
  globalen Admin liegt.
- Es gibt keine Sprachumschaltung, weil der Katalog deutsch ist und niemand
  ihn anders bräuchte.
- Es gibt keine Modulauswahl, weil jeder alles sieht, was der eine Verband
  anbietet.

**Zwischen "global" und "Verein" fehlt eine Ebene: der Verband.** Sie muss
nicht sofort gebaut werden, aber jede der drei Maßnahmen sollte sie
vorbereiten, statt sie zu verbauen.

---

## 1. Vereine: Selbstverwaltung

### Ist-Stand — deutlich weiter als gedacht

Vereinstrainer:innen dürfen heute schon selbst:

| Kann ein Vereinstrainer heute | |
|---|---|
| Beitrittsanfragen freigeben/ablehnen | ✓ |
| Mitglieder direkt per E-Mail aufnehmen | ✓ |
| Mitglieder zu Trainer:innen befördern | ✓ |
| Trainingsgruppen anlegen und leiten | ✓ |
| Vereinseigene Sportarten und Übungen pflegen | ✓ |
| Gruppentrainings planen, Trainings bewerten | ✓ |

Das Fundament steht also. `Sport.ClubId` und `Exercise.ClubId` erlauben schon
heute vereinseigene Katalogeinträge, die nur der Verein sieht.

### Die Lücke ist klein — und trügerisch

Nur drei Dinge fehlen:

1. **Verein anlegen** — liegt im `AdminController`, also beim globalen Admin.
2. **Trainer:in entfernen** — `RemoveTrainerAsync` existiert, hängt aber
   ebenfalls nur am Admin-Endpunkt.
3. **Vereinsstammdaten ändern** — Umbenennen gibt es überhaupt nicht,
   für niemanden.

Punkt 2 ist der gefährliche. Würde man ihn einfach freigeben, könnte **jede**
Trainerin **jede** andere entfernen — auch die Gründerin. Bei einem Verein
mit einem Trainer fällt das nicht auf, bei fünf schon.

### Die eigentliche Frage: Rollen innerhalb des Vereins

`ClubTrainer` ist heute eine flache Rolle: Trainer:in oder nicht. Für
Selbstverwaltung braucht es mindestens zwei Stufen — wer den Verein
**verwaltet** (Stammdaten, Trainer:innen berufen und abberufen) und wer
darin **trainiert**.

Das ist die eine Entscheidung, die man **jetzt** treffen sollte, nicht
später: Ein Feld `ClubTrainer.Role` nachzurüsten heißt sonst, jede bestehende
Zeile zu migrieren und jede Berechtigungsprüfung erneut anzufassen. Solange
es zwei Vereine gibt, ist das billig.

### Vorschlag: zwei Schritte

**Schritt A — kleiner Nutzen, kein Risiko**

- `ClubTrainer.Role` einführen (`Verwaltung` | `Training`). Beim Backfill
  bekommen alle bestehenden Trainer:innen `Verwaltung`, damit niemand
  Rechte verliert.
- Trainer:in entfernen und Verein umbenennen für die Rolle `Verwaltung`
  freigeben.
- Vereinsanlage bleibt vorerst beim Admin.

Damit verwaltet sich ein Verein vollständig selbst, sobald er existiert.

**Schritt B — Vereinsregistrierung**

- Ein Nutzer beantragt einen Verein, der Admin gibt frei; der Antragsteller
  wird automatisch erste:r Verwalter:in.
- Freigabe deshalb, weil sonst Vereinsnamen besetzt werden, die es real
  gibt: "Hundesportverein Musterstadt e.V." darf nicht anlegen, wer nicht
  dazugehört. Das ist kein technisches, sondern ein Vertrauensproblem —
  und eine Warteschlange mit einem Klick löst es.

---

## 2. Sprachen

### Der Umfang besteht aus drei Schichten, nicht aus einer

| Schicht | Umfang (gezählt) | Aufwand | Bewertung |
|---|---|---|---|
| Oberfläche (Frontend) | ~500 Zeichenketten + ~300 Textknoten | groß, aber geradeaus | **übersetzen** |
| Servermeldungen | 225 `Failure(...)`/`NotFound(...)` | mittel, **architektonisch** | **übersetzen, aber anders** |
| Kataloginhalte (Datenbank) | 483 Seed-Einträge, 44 Prüfungsordnungen, 112 Fragen + 434 Antworten | sehr groß | **größtenteils NICHT übersetzen** |

### Warum der Katalog nicht übersetzt gehört

Die dritte Schicht ist die größte — und die, bei der Übersetzen ein **Fehler**
wäre:

- **Prüfungsordnungen sind deutsche Verbandsdokumente.** Wer auf die BH
  hintrainiert, braucht den Begriff, der auf dem Leistungsheft und in der
  PO steht. Eine englische "companion dog test" hilft niemandem und führt
  in die Irre.
- **Die Sachkundefragen sind der Fragenkatalog des SWHV.** Übersetzt sind
  sie für die Prüfung wertlos und im Zweifel schädlich: Eine Nuance, die in
  der Übersetzung verrutscht, ist eine falsch gelernte Antwort.

Genau das deckt sich mit der ursprünglichen Beobachtung: *Tagebuch und Fährte
sind sprachunabhängig.* Richtig — die **Werkzeuge** sind universell, der
**deutsche Prüfungskatalog** ist es nicht.

Daraus folgt die Leitlinie:

> **Oberflächensprache ≠ Inhaltssprache.** Die App wird übersetzt, der
> Katalog bleibt in seiner Ursprungssprache.

Für international gebräuchliche Sparten (Agility, Turnierhundsport,
Obedience) kann später ein optionales Namens-/Beschreibungsfeld je Sprache
dazukommen — dort ist es fachlich sauber. Für BH, IGP und Sachkunde nicht.

> **Überholt für die FCI-Ordnungen seit 2026-10-16 (Paket L1, Fassung 0.31, siehe
> Ende dieses Dokuments):** Die Originalfassung der FCI-Prüfungsordnung ist
> Englisch. Dort ist die englische Bezeichnung nicht „eine Übersetzung, die in
> die Irre führt", sondern der Name, der gilt. Für Sachkunde und die deutschen
> Verbandsordnungen (VDH) bleibt die Aussage richtig.

### Servermeldungen: Codes statt Sätze

225 Fehlermeldungen liegen als deutscher Fließtext im `Result`-Objekt und
landen unübersetzt im Toast. Sie über `Accept-Language` serverseitig zu
übersetzen wäre die naheliegende, aber schlechtere Lösung: Der Server müsste
Sprachdateien pflegen, und die Meldung bliebe eine Zeichenkette, die niemand
programmatisch auswerten kann.

Besser: `Result` bekommt neben dem Text einen **Fehlercode**, das Frontend
übersetzt ihn. Das ist mehr Arbeit als ein Suchen-und-Ersetzen — es berührt
jeden Dienst — bringt aber zwei Dinge auf einmal: Übersetzbarkeit und
auswertbare Fehler.

Migration schrittweise möglich: Code optional einführen, Frontend nimmt den
Code wenn vorhanden und sonst den Text. Kein Bruch, kein Stichtag.

### Geltungsbereich: Land vor Sprache

Ergänzung des Auftraggebers (2026-09-03): Nutzer:innen sollen wählen, in
welchem **Geltungsbereich der Prüfungsordnungen** sie leben - nach Land
sortiert. Vorerst nur Deutschland mit Inhalten; andere Länder auswählbar,
aber leer.

Das ist keine Nebenbemerkung zur Sprache, es ist die genauere Fassung
derselben Sache. Die Leitlinie oben lautete "Oberflächensprache ist nicht
Inhaltssprache". Präziser ist:

> **Die Oberfläche hat eine Sprache. Der Katalog hat einen Geltungsbereich.**

Denn beides fällt nicht zusammen:

- Wer in Deutschland trainiert und die App auf Englisch nutzt, braucht
  weiterhin die deutschen Prüfungsordnungen - die BH bleibt die BH.
- Wer in Österreich lebt, spricht dieselbe Sprache, hat aber einen anderen
  Verband und andere Ordnungen.

Land und Sprache sind also zwei getrennte Einstellungen. Sie voneinander
abzuleiten wäre der naheliegende und falsche Weg.

**Was daraus folgt**

Der Katalog braucht eine Zuordnung zum Geltungsbereich. Am sparsamsten am
Regelwerk, nicht an der Sportart: Agility ist international, die
Prüfungsordnung dazu nicht.

```
Regulation.CountryCode   ISO-3166-1 alpha-2, z.B. "DE"; null = international
UserPreference.Country   gewähltes Land, Vorgabe "DE"
```

`null` als "gilt überall" ist wichtig: Sonst müsste jede international
gültige Ordnung für jedes Land dupliziert werden.

**Leere Länder sind ein Merkmal, kein Mangel**

Wer Österreich wählt und einen leeren Katalog vorfindet, darf nicht auf
einem kaputten Bildschirm landen. Die leere Auswahl braucht einen eigenen
Text: dass es diesen Bereich gibt, dass noch keine Ordnungen hinterlegt
sind, und dass Tagebuch, Fährte und Freitext unabhängig davon vollständig
funktionieren. Genau das ist ja der Punkt - die Werkzeuge sind universell,
nur der Prüfungskatalog ist es nicht.

Die Länderliste sollte dabei aus den Daten kommen und nicht im Code stehen,
damit ein neues Land Inhalte bekommt, ohne dass jemand deployt.

**Reihenfolge:** Das Land gehört VOR die Übersetzung. Es entscheidet mit
darüber, was überhaupt übersetzt werden muss - und es ist ungleich billiger.

---

### Was Sprache mit Modulen zu tun hat

Die Sachkunde ist ein deutsches SWHV-Angebot. Wer die App auf Englisch
nutzt, sollte sie gar nicht erst angeboten bekommen — das ist keine
Übersetzungs-, sondern eine **Modulfrage**. Damit hängt Thema 2 direkt an
Thema 3.

---

## 3. Module und Sportarten pro Nutzer

Hier gibt es heute **nichts** — keinerlei Nutzereinstellungen. Dafür ist der
Entwurf am klarsten.

### Zwei Dinge, die man auseinanderhalten muss

Der Wunsch enthält zwei verschiedene Mechanismen, und sie brauchen
unterschiedliche Modelle:

**a) Sportartenauswahl — Positivliste**

"Welche Sportarten mache ich?" Das Trainingsformular bietet dann nur diese
plus Freitext. Eine leere Auswahl heißt "alle" — damit ist der Standard
automatisch an, ohne Sonderfall.

Positivliste ist hier richtig: Wer *IGP und Fährte* ausgewählt hat, will
nicht, dass später automatisch *Obedience* dazukommt, nur weil der Katalog
gewachsen ist. Die Aussage ist "ich mache genau das".

**b) Modulschalter — Negativliste**

"Welche Funktionen will ich nicht sehen?" Fährtenaufzeichnung, Sachkunde,
Gruppentraining, Wetter. Gespeichert wird, was **abgewählt** ist. Damit
erscheint jedes künftig hinzukommende Modul von selbst — Standard an, wie
gewünscht.

Diese Unterscheidung ist der Kern: Bei Sportarten ist die Aussage eine
Auswahl, bei Modulen ein Verzicht. Ein einziges Modell für beide würde
entweder neue Sportarten aufdrängen oder neue Module verstecken.

### Die Fährte als Beispiel für beides

Der große Fährten-Knopf im Tagebuch soll verschwinden, wenn man keine Fährte
läuft. Zwei Wege dorthin:

- **abgeleitet** aus der Sportartenauswahl (FAERTE nicht gewählt → kein
  Knopf), oder
- **eigener Schalter**.

Empfehlung: **beides, in dieser Reihenfolge.** Die Sportartenauswahl setzt
den Standard des Schalters, der Schalter bleibt aber getrennt umstellbar.
Grund: Die GPS-Aufzeichnung taugt auch für Spaziergänge und Laufeinheiten,
nicht nur für die Fährte — wer sie dafür nutzt, soll sie behalten dürfen,
ohne "Fährte" als Sportart anzugeben.

### Die eine offene Frage: pro Nutzer oder pro Hund?

Der Wunsch ist "als Benutzer". Das ist einfacher und deckt den Normalfall.
Aber: Wer einen Fährtenhund und einen Agility-Hund hat, bekommt den
Fährten-Knopf dann bei beiden.

Empfehlung: **pro Nutzer beginnen.** Eine Übersteuerung pro Hund lässt sich
später ergänzen, ohne den Nutzerstandard zu brechen — umgekehrt nicht.
Das ist aber eine fachliche Entscheidung, keine technische.

### Datenmodell (Skizze)

```
UserPreferences        (UserId, Locale, UpdatedAt)
UserSportSelection     (UserId, SportId)        -- leer = alle
UserDisabledModule     (UserId, ModuleKey)      -- leer = alle an
```

Klein, additiv, ohne Migrationsrisiko. `Locale` liegt bewusst hier — dann
hängen Sprache und Module am selben Ort, und die Regel "englische Oberfläche
⇒ Sachkunde standardmäßig aus" ist eine Zeile.

---

## Umsetzungsstand

**Schritt 1 umgesetzt (2026-09-03):** Module und Sportartenauswahl.

- `user_preferences` je Nutzer, dazu `user_disabled_modules` (Negativliste)
  und `user_sport_selections` (Positivliste); `dog_sport_selections` plus
  `dogs.UsesOwnSports` für die Ebene am Hund.
- Endpunkte unter `/api/preferences`, Vererbungsregel ausschließlich in
  `PreferenceService.GetEffectiveDogSportsAsync` - im Frontend wird sie
  angewandt, nicht ein zweites Mal formuliert.
- Einstellungen im Profil; Navigation, Dashboard-Kachel, Trainingsformular
  und Fährtenbereich richten sich danach.
- Beim Bauen aufgefallen: Kinder mit clientseitig vergebener Id müssen
  ausdrücklich über das DbSet angelegt werden. Nur an die Navigationsliste
  eines bereits gespeicherten Elternobjekts gehängt, hält EF sie für
  vorhanden und erzeugt ein UPDATE statt eines INSERT - das traf null Zeilen
  und brach mit einem Nebenläufigkeitsfehler ab.

**Schritt 2 umgesetzt (2026-09-03):** Vereins-Selbstverwaltung, Teil A.

- `ClubTrainer.Role` mit den Stufen `Training` und `Verwaltung`. Ein
  einmaliger Backfill gibt allen bestehenden Zuweisungen `Verwaltung` -
  niemand verliert Rechte, die er vorher hatte. Er greift nur bei Vereinen
  ganz ohne verwaltende Person, damit er spätere bewusste Herabstufungen
  nicht wieder aufhebt.
- Neu für Vereine selbst: umbenennen (`PUT /api/clubs/{id}` - ging bisher
  für niemanden, auch nicht für Admins), Trainer:innen berufen und
  abberufen, Rolle ändern.
- Die Prüfung steht einmal als `CanManageClubAsync` und wird im Dienst
  ausgewertet, nicht am Controller - der Aufruf hängt an zwei Routen.
- Schutz: Die letzte verwaltende Person kann weder entfernt noch
  herabgestuft werden. Sonst bliebe ein Verein zurück, an den nur noch ein
  globaler Admin herankommt - genau die Sackgasse, die die
  Selbstverwaltung vermeiden soll.
- Auch die Beförderung eines Mitglieds zur Trainerin verlangt jetzt
  `Verwaltung`. Vorher genügte dort "ist Trainer", während die
  E-Mail-Zuweisung strenger war - dieselbe Befugnis war je nach Weg
  unterschiedlich streng.

**Schritt 2 Teil B umgesetzt (2026-09-03):** Vereinsregistrierung mit
Freigabe.

- `club_registrations` als EIGENE Tabelle, nicht als Status am Verein: Ein
  noch nicht freigegebener Verein soll gar nicht erst als Verein
  existieren. Sonst müsste jede vorhandene Abfrage - Liste, Beitritt,
  Sichtbarkeit vereinseigener Übungen - um einen Statusfilter ergänzt
  werden, und die eine vergessene Stelle wäre das Leck.
- Die Freigabe legt den Verein an UND macht den Antragsteller zu seiner
  ersten verwaltenden Person. Ohne das entstünde ein Verein, den niemand
  betreuen kann, und die Prüfung hätte nur Arbeit verschoben.
- Prüfungen beim Antrag: Name gefordert, kein zweiter offener Antrag je
  Person (sonst ließe sich die Freigabeliste fluten - genau davor soll sie
  schützen), und kein Antrag auf einen Namen, den es schon gibt
  (Groß-/Kleinschreibung egal). Beim Freigeben wird der Namensvergleich
  wiederholt: Zwischen Antrag und Entscheidung kann derselbe Verein
  anderweitig entstanden sein.
- Ablehnung mit Begründung, die als Benachrichtigung ankommt. Eine
  kommentarlose Ablehnung führt nur zum nächsten, gleichlautenden Antrag.
- Admins werden beim Eingang benachrichtigt, sonst bliebe der Antrag
  liegen, bis zufällig jemand in die Verwaltung schaut.

Offen: Sprachen (Schritt 3). `Locale` ist im Datenmodell schon vorgesehen,
wird aber noch nirgends ausgewertet.

**Paket E1 umgesetzt (2026-10-16, Fassung 0.30):** Englisch ohne Konto,
Lücken der Oberfläche, Servermeldungen.

- Reihenfolge der Sprache: Konto, dann lokale Wahl (`dogity.sprache` im
  localStorage, `lib/i18n/lokale-sprache.ts`), dann Gerät, dann Deutsch. Der
  Umschalter (`components/sprach-umschalter.tsx`) sitzt in der Kopfzeile der
  öffentlichen Seiten und über den Karten von Anmeldung, Registrierung und den
  Passwortseiten. Er setzt nur die lokale Wahl und blendet sich für
  Angemeldete aus - deren Sprache steht im Profil und ginge in der Reihenfolge
  vor, ein Klick wäre wirkungslos oder überschriebe still die Einstellung.
- Das ausgelieferte HTML der Startseite bleibt deutsch (Vorgabe bis zum
  Hydrieren), Metadaten und Vorschaubild ebenso: Die Seite wird so gefunden
  wie vorher. Der sichtbare Inhalt liegt in Client-Komponenten
  (`components/marketing/startseite-inhalt.tsx`), die Daten in
  `startseite-daten.ts` (auch für das JSON-LD).
- Servermeldungen werden im Browser übersetzt, nicht mit Codes: `ApiError`
  übersetzt `message` über das Wörterbuch (`uebersetzeMeldung`), `errors`
  behält die deutschen Originale. Meldungen mit Werten (`höchstens {hoechstens}
  Zeichen`) werden über ihre Platzhalter-Fassung gefunden. Wer Meldungen
  vergleicht, vergleicht `errors` (siehe `istPlanZielFehler`).
- Wächter: `i18n.test.ts` liest die Meldungen aus dem C#-Quelltext
  (Muster A bis E, siehe dort) und verlangt eine englische Fassung;
  `oberflaechen-luecken.test.ts` meldet sichtbaren Text ohne `t()` mit dem
  TypeScript-Compiler. Ausnahmen sind dort als kommentierte Liste geführt.
- Bewusste Eigenschaften: Steht im Profil "automatisch", gilt eine zuvor
  abgemeldet gewählte lokale Sprache weiter (Konto, dann lokale Wahl, dann
  Gerät) - sie lässt sich nur durch Abmelden und neu Wählen ändern. Der
  Standardname "Aktueller Standort" wird in der Oberflächensprache als Ortsname
  gespeichert; nach einem Sprachwechsel kann derselbe Ort in der Liste der
  zuletzt genutzten Orte zweimal stehen.
- Nachzuziehen im Paket "englische Datenschutzerklärung": Der neue
  localStorage-Eintrag `dogity.sprache` (reine Komforteinstellung, wird nicht
  an den Server gesendet) gehört in Datenschutzerklärung und
  Verarbeitungsverzeichnis.

**Paket E2 umgesetzt (2026-10-16, Fassung 0.30):** Benachrichtigungen, Push und
Mails in der Sprache der Empfänger:in.

- Sprache bei der Registrierung: `RegisterRequest.Locale` ("de"/"en"); die
  Oberfläche schickt die gerade angezeigte Sprache mit. Gespeichert wird sie
  vor der Willkommensmail in `UserPreference.Locale`
  (`PreferenceService.SetRegistrationLocaleAsync`); unbekannte oder leere Werte
  speichern nichts. Bestehende Konten ohne Sprache bekommen weiter Deutsch.
  `Sprachen` (Application/Preferences) ist die eine Stelle, die die Sprachen
  des Servers kennt.
- Server-Übersetzung nach dem Muster des Frontends (`Common/Uebersetzung.cs`):
  `Meldung.Von("deutscher Satz mit {platzhaltern}", ("name", wert))`, das
  Wörterbuch `Uebersetzung.Englisch` liefert die englische Fassung, Fehlendes
  bleibt Deutsch. `INotificationService.CreateAsync` nimmt nur noch eine
  `Meldung`; der Service schlägt die Sprache nach und rendert den Satz einmal
  für Glocke, Mail und Push. Satzteile als Werte gibt es nicht - Fälle mit und
  ohne Name oder Hund sind eigene Vorlagen. Daten (`DateOnly`) schreibt der
  Server je Sprache (de 05.09.2026, en 5 Sep 2026, InvariantCulture).
- Der Schutz gegen gleichlautende Meldungen (Rückmeldung zum Feedback) fragt
  `HatUngelesenAsync`, das mit dem Text in der Sprache der Empfänger:in
  vergleicht.
- Mails: `MailVorlagen` nehmen die Sprache als Parameter (Betreff, Anrede,
  Absätze, Knopf, Fußzeile, Abmeldesatz, `<html lang>`); die Testmail bleibt
  deutsch. Die Passwort-Mail schlägt die Sprache der Empfänger:in nach, die
  Willkommensmail bekommt die bei der Registrierung gewählte.
- Push: Text = gerenderte Meldung, `lang` im Payload folgt der Sprache, Titel
  "Dogity". Der Test-Push kommt in der Sprache der Admin-Person.
- Wächter: `UebersetzungTests` liest alle `Meldung.Von("...")` aus dem
  Quelltext und verlangt eine englische Fassung mit denselben Platzhaltern, die
  passenden Werte am Aufruf und keine verwaisten Wörterbuchzeilen.

---

## Reihenfolge

Nach Nutzen je Aufwand, nicht nach Reihenfolge der Nennung:

**1. Module und Sportartenauswahl.** Kleinster Aufwand, sofort spürbar,
keine Abhängigkeit. Legt zugleich `UserPreferences` an, das die Sprache
später braucht.

**2. Vereins-Selbstverwaltung, Schritt A.** Klein, schließt eine echte
Lücke — und die Rollenentscheidung wird billiger, je früher sie fällt.

**3. Sprachen.** Das größte Paket. Sinnvoll erst nach 1., weil die
Modulschalter darüber entscheiden, was überhaupt übersetzt werden muss:
Bleibt die Sachkunde ein deutsches Modul, entfallen 546 Zeilen
Prüfungsinhalt aus dem Übersetzungsumfang.

**4. Vereinsregistrierung, Schritt B.** Erst nötig, wenn tatsächlich
Vereine von außen dazukommen.

Ein Verbandsobjekt braucht es für keinen dieser Schritte. Es genügt, Schritt
1 und 2 so zu bauen, dass sie es nicht verbauen — konkret: Modulschlüssel als
Zeichenketten statt als Enum-Werte, damit ein Verband später eigene Module
mitbringen kann, ohne dass Code geändert wird.

---

## Entschieden (2026-09-03, Auftraggeber)

1. **Sportartenauswahl pro Nutzer UND pro Hund.** Der Nutzerwert ist der
   Standard, der Hund darf ihn übersteuern - wer einen Fährtenhund und einen
   Agility-Hund hat, bekommt an jedem Hund das Passende. Datenmodell dafür:

   ```
   UserSportSelection  (UserId, SportId)   -- leer = alle
   DogSportSelection   (DogId,  SportId)   -- leer = erbt vom Nutzer
   ```

   „Leer" muss dabei *erben* heißen und nicht *nichts*, sonst kann man am
   Hund keine Auswahl auf „alle" zurücksetzen. Eine ausdrückliche Markierung
   am Hund („folgt der Nutzereinstellung") ist deshalb sauberer als das
   Fehlen von Zeilen.

2. **Vereinsanlage mit Freigabe.** Antrag stellen darf jede:r, freigeben der
   Admin; der Antragsteller wird automatisch erste:r Verwalter:in.

3. **Prüfungsordnungen bleiben unübersetzt** - optionale englische
   *Beschreibung* für internationale Sparten später möglich.

4. **Abschaltbare Module** (Startpunkt): Fährte/GPS, Sachkunde,
   Gruppentraining, Wetter, Statistik (seit 0.41 auch Gesundheit).

**Schritt 3 umgesetzt (2026-09-03), ausgeliefert als Version 0.10:** Geltungsbereich und Sprache.

Beides in einem Zug, aber sauber getrennt gebaut - Land zuerst, weil es
mitentscheidet, was überhaupt übersetzt werden muss.

*Geltungsbereich*

- `Regulation.CountryCode` (ISO 3166-1 alpha-2, null = gilt überall) und
  `UserPreference.Country`. Eigene Tabelle `countries` für die wählbaren
  Länder: Aus den Ordnungen ließe sich die Liste nicht ableiten - ein Land
  ohne Inhalte käme darin gar nicht vor, und genau die sollen wählbar sein.
- Ohne Namensfeld. Ländernamen kennt der Browser bereits
  (`Intl.DisplayNames`) und liefert sie in der jeweiligen
  Oberflächensprache; eine eigene Liste wäre eine nachgebaute
  Übersetzungstabelle.
- Der Altbestand wurde einmalig Deutschland zugeordnet - und nur dann, wenn
  ÜBERHAUPT keine Ordnung ein Land trägt. Ohne diese Bedingung holte der
  Seeder bei jedem Start jede bewusst auf "gilt überall" gesetzte Ordnung
  zurück und machte damit eine Eingabe rückgängig.
- Nachgemessen: 44 Ordnungen für DE, 0 für AT. Eine Ordnung testweise auf
  null gesetzt - sie erscheint danach unter DE **und** AT, und die Zählung
  je Land steigt entsprechend. Das ist die Regel, die "null = gilt überall"
  überhaupt erst nützlich macht.

*Sprache*

- Keine Sprache in der Adresse, obwohl die Next-Doku genau das vorschlägt.
  Zwei Gründe: Die App hinter der Anmeldung rendert im Browser und kennt
  ihren Nutzer erst dort; und die öffentlichen Seiten beschreiben deutsche
  Prüfungsordnungen - eine englische Adressvariante mit demselben deutschen
  Inhalt wäre eine Dublette ohne eigenen Wert, und die vorhandene
  Sichtbarkeit wäre der Preis.
- Der Schlüssel ist der deutsche Satz selbst, keine erfundene Kennung. Der
  Quelltext bleibt lesbar, und der Rückfall ist von selbst richtig: Fehlt
  eine Übersetzung, erscheint Deutsch - also genau das, was vorher dastand.
- Weil eine Typprüfung das nicht bemerken kann, liest `i18n.test.ts` den
  Quelltext und meldet jeden Aufruf ohne englische Fassung. Das ist die
  einzige Stelle, an der "vollständig übersetzt" eine überprüfbare Aussage
  ist. Stand: 626 Sätze.
- `uebersetzbar()` markiert Sätze, die als Daten irgendwo stehen und erst
  weit entfernt über `t(variable)` gerendert werden. Ohne diese Markierung
  hielte der Test sie für unbenutzt.

*Was die Sachkunde ausblendet - Land, nicht Sprache*

Der ursprüngliche Entwurf wollte "englische Oberfläche ⇒ Sachkunde aus".
Umgesetzt ist stattdessen "Land ≠ DE ⇒ Sachkunde aus". Der Unterschied ist
inhaltlich: Wer in Deutschland lebt und die App auf Englisch nutzt, macht
trotzdem die deutsche BH und braucht genau diese Fragen. Umgekehrt hilft
der SWHV-Katalog in Österreich auch auf Deutsch nicht weiter. Die
Zugehörigkeit hängt am Geltungsbereich, nicht an der Sprache - dieselbe
Unterscheidung wie beim Katalog selbst.

*Nicht übersetzt, mit Absicht*

Die öffentlichen Seiten (Startseite, Prüfungsordnungen, Sachkunde) bleiben
deutsch. Sie beschreiben deutsche Verbandsinhalte; eine englische Fassung
mit denselben deutschen Begriffen wäre eine Dublette, die der Sichtbarkeit
schadet und niemandem hilft.

*Eigene Fassung statt Anhang an 0.9*

Das Sprachpaket ist als **0.10** ausgewiesen und nicht in 0.9 eingereiht.
Der Grund ist nicht Buchhaltung: 0.9 beschreibt Fährte und
Vereinsverwaltung, das Sprachpaket beschreibt etwas anderes. In einem
Eintrag zusammengelegt stünde eine Fassung da, deren Überschrift nur die
Hälfte trifft.

Dabei ist eine Falle aufgefallen und abgesichert: `0.10` ist als
Zeichenkette KLEINER als `0.9`. Die Reihenfolge der Liste hing bis dahin
allein am Datum - bei zwei Einträgen desselben Tages hätte das nichts
gemerkt. `versionshinweise.test.ts` vergleicht die Fassungen jetzt als
Zahlen und meldet eine falsch einsortierte Fassung im Klartext. Gegenprobe
gemacht: mit einer absichtlich zu kleinen Nummer schlägt der Test fehl,
danach wieder grün.

---

## Paket L1 (2026-10-16, Fassung 0.31): FCI überall, englische Namen nur für FCI

Anlass: Die internationale FCI-Prüfungsordnung („International Utility Dog
Regulations", FCI-IGP, gültig ab 01.01.2025) sagt in ihrem Abschnitt „Validity"
zweierlei: Sie gilt *„for all FCI members and contract partners"*, und sie ist
*„written in the English language"* – bei Zweifeln hat der englische Text
Vorrang. Beides hat der Katalog bisher nicht abgebildet: Er beruhte auf der
deutschen Übersetzung, und der einmalige Lauf des `CountrySeeder` hatte alle
Ordnungen Deutschland zugeordnet (Hoopers war als einzige auf „gilt überall"
stehengeblieben, obwohl es die VDH-Ordnung ist).

*Geltungsbereich*

- Die 14 FCI-Sportarten (BH, IBGH 1-3, IGP 1-3, Fährte, FPr, UPr, SPr, GPr, StöPr,
  IAD) haben `CountryCode` null – sie gelten in jedem Land. Turnierhundsport,
  Agility und Hoopers (VDH) sind `DE`. Die Zuordnung je Sportart steht in
  `FciKatalog`; Details und Zahlen in [PO_VERIFIKATION.md](PO_VERIFIKATION.md).
- Bestehende Datenbanken stellt die Migration `AddCatalogEnglishNamesAndFciScope`
  **einmalig** um (über den Sport-Code der Ordnung). Ein Seeder, der das bei jedem
  Start tut, würde eine spätere bewusste Änderung im Katalog-Editor jedes Mal
  zurückdrehen – dasselbe Prinzip wie beim `CountrySeeder`. Down: alles `DE`,
  Hoopers null (der Zustand davor).
- Frische Datenbanken: Der `SportCatalogSeeder` setzt den Geltungsbereich beim
  Anlegen jeder Ordnung ausdrücklich. Er läuft in `Program.cs` vor dem
  `CountrySeeder`; dessen einmalige DE-Zuordnung greift nur, solange keine
  Ordnung ein Land trägt, und tut danach nichts – sie überschreibt das nicht.
- Die Zählung je Land (`CountryDto.RegulationCount`) zählt „gilt überall" mit,
  wie es diese Regel immer vorgesehen hat. Folge: Ein Land wie Österreich ist
  nicht mehr „leer" (es hat die FCI-Ordnungen), und die Länderwahl im Profil
  erscheint damit für alle – sie hängt an „mindestens zwei Länder mit Inhalt".
  Dass dort ein Land mit nur den internationalen Ordnungen steht, ist gewollt:
  Wer in Österreich trainiert, sieht die FCI-Ordnungen und nichts Deutsches.
- Unverändert: Die Sachkunde hängt am Land DE, nicht am Katalog. Die öffentlichen
  Prüfungsordnungs-Seiten zeigen weder Land noch filtern sie danach.
- Nebenbefund: Der Katalog-Editor schickte beim Speichern einer Ordnung keinen
  `countryCode` mit; das Backend liest „leer" als „gilt überall". Jedes Speichern
  im Editor hätte die Ordnung damit still international gemacht. Der Editor
  schickt den vorhandenen Wert jetzt mit.

*Englische Namen*

- Neue, optionale Spalte `NameEn` an `Sport`, `Regulation` und `Exercise` (Länge
  wie `Name`: 100/100/150). Gepflegt nur für die FCI-Ordnungen; Vereinsübungen,
  Freitext und nationale Ordnungen haben keinen und zeigen auch auf Englisch den
  deutschen Namen.
- Die Antworten liefern den englischen Namen **serverseitig** mit (`nameEn`,
  `exerciseNameEn`, `sportNameEn`, `regulationNameEn`): Katalog, Tagebuch,
  Trainingsplan, Gewichtung, Statistik, Traineransicht. Ein Nachschlagen im
  Frontend über die Übungs-Id hätte den Katalog an jeder dieser Stellen
  nachladen müssen – und offline hätte er gefehlt.
- Das Frontend wählt mit `katalogName({ name, nameEn }, sprache)` bzw.
  `uebungName(...)` (`lib/katalog-name.ts`): Englisch → `nameEn ?? name`, sonst
  `name`. Der deutsche Name bleibt der Schlüssel (Gruppierung, Doppelprüfung,
  Speichern); die englische Fassung ist nur Anzeige. In der Übungssuche zählt der
  englische Name als Suchtext mit.
- Die öffentlichen Seiten `/pruefungsordnungen/**` bleiben deutsch.
- Der Seeder (`FciKatalog`) trägt die Namen nur ein, wo `NameEn` leer ist, und ist
  damit idempotent; im Katalog-Editor (Sportart, Ordnung, Übung) gibt es das
  Feld „Englischer Name", Grenze in `Textlaengen.cs` und `textlaengen.ts`.

*Begründung der Ausnahme*

Die Leitlinie „Oberflächensprache ≠ Inhaltssprache" bleibt für Sachkunde und die
deutschen Verbandsordnungen richtig. Für die FCI-Ordnungen ist die Rechnung
umgekehrt: Der englische Name ist der des Originals, und wer international
startet, braucht ihn. Übungsbeschreibungen und Bewertungskriterien bleiben
deutsch – das ist unsere eigene Beschreibung, keine Übersetzung der FCI-Ordnung.

> **Überholt seit 2026-10-18 (Paket E4, Fassung 0.34, siehe unten):** Auch die
> Beschreibungen und Bewertungstexte der FCI-Ordnungen gibt es jetzt auf
> Englisch. Es bleiben unsere eigenen Texte, keine Kopie der FCI-Ordnung.

---

## Paket L2 (2026-10-17, Fassung 0.32): Österreich und Sportarten nach Land

*Sportarten nach Land*

- Vorher grenzte nur `GetRegulationsAsync` nach Land ein. Jetzt auch die
  Sportartenliste (`GET /api/sports`) - für angemeldete Personen, nach demselben
  Satz wie bei den Ordnungen: Eine Sportart ist sichtbar, wenn mindestens eine
  ihrer Ordnungen im Land gilt (`CountryCode` null oder = Land) **oder** sie gar
  keine Ordnung hat (freie Sportart, Vereinssportart).
- Land der Person: das gespeicherte; ohne Wahl „DE“ - wie im Frontend
  (`VORGABE_LAND`), jetzt auch im Backend (`Country.Vorgabe`). Beim Aufruf ohne
  Anmeldung (öffentliche Seiten) und mit `?alle=true` gibt es keine Eingrenzung.
- `alle=true` benutzen Admin-Katalogpflege, Vereinskatalog-Pflege und die
  Sportartenseite des Admins. Es ist keine Zugriffsbeschränkung: Sportarten und
  Ordnungen sind öffentlich, die Eingrenzung ist eine Erleichterung.
- Bestehendes bleibt benutzbar: Übungen, Ordnungen und Ordnungsdetails einer
  nicht sichtbaren Sportart sind per Id weiter abrufbar, Ziele und Trainings
  zeigen ihre Namen aus den eigenen Antworten. Nur neu wählbar ist die Sportart
  nicht mehr. Eine bereits gespeicherte Sportartenwahl (Profil, Hund) bleibt
  unverändert gespeichert, die Liste zeigt sie nur nicht.
- Beim Anlegen eines Ziels werden die Ordnungen jetzt nach Land gefiltert (bisher
  alle). Ein Kürzel von einer Prüfungsordnungs-Seite wird gegen den ganzen
  Katalog aufgelöst (die Kürzel entstehen aus allen Ordnungen) und die
  angesteuerte Ordnung bleibt in der Auswahl.

*Österreich*

- Neu im Katalog sind 16 Ordnungen mit `CountryCode` „AT“ - Einzelheiten und
  Prüfprotokoll in [PO_VERIFIKATION.md](PO_VERIFIKATION.md), Abschnitt „Österreich“.
  Wer Österreich wählt, sieht die FCI-Ordnungen und die des ÖKV, aber keine des
  VDH; die Sportart Breitensport gibt es nur dort, Turnierhundsport nur in
  Deutschland.
- Keine Schemaänderung und keine Migration: Der Seeder legt die Daten beim Start
  an (idempotent), bestehende Datenbanken bekommen sie beim nächsten Hochlauf.

---

## Paket L3 (2026-10-17, Fassung 0.32): Schweiz

*Stand der Länder*

| Land | Inhalt im Katalog |
|---|---|
| **Deutschland** (`DE`) | VDH: Turnierhundsport (12), Agility inkl. A0 (5), Hoopers H0-H3 (4), dazu die internationalen FCI-Ordnungen |
| **Österreich** (`AT`) | ÖKV: BH/VT (1), Agility LK 1-3 und Jumping (4), Hoopers H1-H3 (3), Breitensport (8), dazu FCI |
| **Schweiz** (`CH`) | SKG: Begleithund 1-3 (3, eigene Sportart `BHCH`), Agility Klasse 1-3 und Jumping (4), dazu FCI |
| übrige Länder | nur die internationalen FCI-Ordnungen |

*Schweiz*

- Neu im Katalog sind 7 Ordnungen mit `CountryCode` „CH“ - Einzelheiten und
  Prüfprotokoll in [PO_VERIFIKATION.md](PO_VERIFIKATION.md), Abschnitt „Schweiz“.
  Der Seeder steht in `SportCatalogSeeder.Schweiz.cs`, nach dem Muster von
  Österreich (partielle Klasse, idempotent, Land je Ordnung ausdrücklich gesetzt).
- Der Begleithund der SKG ist eine **eigene Sportart** („Begleithund (SKG)“), keine
  Ordnung unter der BH: Er hat drei Abteilungen zu je 100 Punkten (Fährte bzw.
  Revier, Unterordnung, Führigkeit) und andere Übungen als die deutsche/FCI-BH.
  Sie erscheint durch die Filterregel von Paket L2 von selbst nur bei Land
  Schweiz (nur dort gilt eine ihrer Ordnungen); für Deutschland, Österreich und
  übrige Länder bleibt sie unsichtbar, per Id und auf den öffentlichen Seiten
  aber lesbar.
- Agility läuft unter der bestehenden Sportart `AGILITY`; sie bleibt damit auch
  in der Schweiz sichtbar. Hoopers gibt es für die Schweiz nicht (kein
  SKG-Reglement gefunden) und ist dort nicht sichtbar.
- Keine Schemaänderung und keine Migration: Der Seeder legt die Daten beim Start
  an. Nationale Ordnungen bleiben deutsch (kein `NameEn`).
- Öffentliche Prüfungsordnungs-Seiten: Die Familie „Schweiz – SKG“ bündelt alle
  Ordnungen mit dem Präfix „SKG“ (`lib/public-catalog.ts`).

---

## Paket E4 (2026-10-18, Fassung 0.34): englische Texte für die FCI-Ordnungen

Entscheidung des Betreibers (2026-10-18): Die FCI-Ordnungen zeigen auf Englisch
nicht nur ihre Namen (Paket L1), sondern auch ihre Beschreibungen und
Bewertungstexte. Nationale Ordnungen (VDH, ÖKV, SKG) bleiben deutsch. Die
Texte sind weiterhin „eigene Beschreibungen, keine Kopie offizieller
Prüfungsordnungen“ (Hinweis auf der Sportartenseite): übertragen wurden unsere
deutschen Texte, in der Terminologie der englischen FCI-IGP 2025 (heeling, basic
position, verbal command, build-up, blind, helper, hold and bark, back transport
usw.). Britisches Englisch, geschlechtsneutral („the handler“).

*Datenmodell* (Migration `AddCatalogEnglishTexts`, nur Spalten, alle optional)

| Spalte | Länge | Gegenstück |
|---|---|---|
| `Exercise.ScoringCriteriaEn` | 1000 | `ScoringCriteria` |
| `Exercise.DescriptionEn` | unbegrenzt | `Description` (hat auch keine Grenze) |
| `Regulation.DescriptionEn` | 2000 | `Description` |
| `RegulationExercise.ScoringNotesEn` | 1000 | `ScoringNotes` |

Grenzen in `Textlaengen.cs` und `textlaengen.ts`, `maxLength` an den Feldern,
Prüfung in den Services („Die englische Beschreibung ist zu lang …“). Die
Antworten (`ExerciseDto`, `RegulationDto`, `RegulationExerciseDto`) liefern die
englischen Felder mit – Katalog, Ordnungsdetail, Übungslisten (Eintragen, Plan).
`Exercise.Description` ist die Ausnahme von der Regel: Sie wird heute nirgends
angezeigt (der Katalog zeigt Kategorie und Bewertung), das englische Gegenstück
ist vorbereitet und im Editor pflegbar. Beim FCI-Katalog ist sie leer.

*Kategorie ohne Spalte:* `Exercise.Category` ist bei den FCI-Übungen eine kleine
feste Menge (Abteilung A/B/C, Unterordnung, Verhalten, Fährte, Stöbern,
Ausdauer) und wird nur in der Übungsliste des Katalogs angezeigt. Sie wird im
Frontend über `en.ts` übersetzt (`katalogKategorie`, „Phase B“, „Obedience“ …;
„Fährte“ nutzt das vorhandene „Track“) – eine Spalte samt Editor-Feld lohnt für
acht Wörter nicht. Übersetzt wird nur bei Übungen mit englischem Namen und einem
dieser Werte; die Kategorie nationaler Übungen und frei vergebene Kategorien
bleiben deutsch.

*Inhalt und Seeder*

- Die Texte liegen in `Persistence/Seed/Data/fci-texte-en.json` (eingebettete
  Ressource, geladen von `FciTexteEn`, Muster: `sachkunde-swhv.json`): 141
  Bewertungstexte von Übungen, 27 Ordnungsbeschreibungen, 204 Hinweise von
  Ordnungs-Übungen. Schlüssel: Sportart-Code + deutscher Übungs-/Ordnungsname
  (+ Übung bei den Hinweisen).
- `SportCatalogSeeder.SetzeEnglischeNamenAsync` trägt sie ein, **nur wo das
  englische Feld leer ist** (Admin-Eingabe bleibt, zweiter Lauf ändert nichts).
  Ein Eintrag, der fehlt, lässt das Feld leer – das Frontend fällt auf Deutsch
  zurück.
- **Bewertungskriterien: Schlüssel auch der deutsche Text.** Der Seeder legt die
  Bewertung einer Übung nur an, überschreibt sie aber nie. Lokal und vermutlich
  auch auf Prod tragen deshalb 12 FCI-Übungen noch eine ältere deutsche Fassung
  als eine frische Datenbank (z. B. BH „Fußarbeit“, IBGH „Freifolge“, UPr/GPr
  „Klettersprung“). Die Datei hat zu jeder dieser Übungen beide Fassungen als
  eigene Einträge, und der Seeder setzt das Englisch nur, wenn der deutsche Text
  der Übung genau dem Eintrag entspricht. Ein vom Admin geänderter Text bekommt
  kein Englisch, das nicht dazu passt. (Beschreibungen und Hinweise der Ordnungen
  schreibt der Seeder bei jedem Start selbst – dort gibt es nur eine Fassung.)
- **Altübungen** ohne aktuelle Ordnung (BH „Ablegen mit Abrufen“, „Sitz aus der
  Bewegung“) sind ebenfalls übersetzt. Die zwei ÖKV-eigenen BH-Übungen („Freilaufen
  mit Heranrufen“, „Ortsabhängige Begegnung“) und die ÖKV-BH-Ordnung samt deren
  Hinweisen bleiben deutsch.
- **Seitenangaben:** Der einzige Verweis in den FCI-Texten („FCI-PO 2025, S. 36“,
  Gegenstände verweisen) steht auf der englischen PDF auf derselben Seite 36 („Page
  36 von 82“, Abschnitt „Indicating of the articles“) und ist als „p. 36“ übernommen.
- Wird ein deutscher Seed-Text geändert, gehört der englische Text in der Datei
  nachgezogen. Das ist abgesichert: Jeder Eintrag der Datei trägt den deutschen
  Quelltext (`description` bzw. `scoringNotes`; bei Übungen `scoringCriteria`),
  der Seeder trägt Englisch nur zu genau diesem Text ein und leert bei
  Ordnungsbeschreibung und Hinweis das englische Feld, sobald er den deutschen
  Text neu schreibt. Eine veraltete englische Zahl bleibt also auch auf Prod
  nicht stehen - es erscheint Deutsch, bis die Datei nachgezogen ist, und
  `KatalogTexteEnTests` (Quelltext = aktueller Seed-Text, jede Zeile hat ein
  Englisch) schlägt bei der Änderung fehl. Von Hand eingetragenes Englisch bleibt
  stehen, solange sich der deutsche Text nicht ändert.

*Frontend*

- `katalogText(de, en, sprache)` (`lib/katalog-name.ts`): Englisch → englischer
  Text, sonst oder bei leerem Feld der deutsche. Eingesetzt bei den
  Rahmenbedingungen (Sportartenseite, Ziel anlegen), den Bewertungshinweisen
  einer Ordnungs-Übung, den Bewertungskriterien der Übung (Sportartenseite und
  Eintragen). Lange Texte brechen mit `[overflow-wrap:anywhere]` um.
- Katalog-Editor (Sportartenseite und „Neue Übung“): optionale englische Felder
  für Rahmenbedingungen, Beschreibung, Bewertungskriterien und Bewertungshinweise;
  beim Anlegen einer Übung nur im globalen Katalog, wie beim englischen Namen.
- Die öffentlichen Seiten `/pruefungsordnungen/**` bleiben deutsch.

*Prüfung:* `KatalogTexteEnTests` (Vollständigkeit je FCI-Text, nichts Englisches
bei nationalen Ordnungen, Zahlen des deutschen Textes im englischen, Spaltenlängen,
Idempotenz, Admin-Eingaben, ältere deutsche Fassungen, Antworten, Editor-Rechte)
und `katalog-name.test.ts`.

---

## Paket O1 (2026-10-21, Fassung 0.35): Obedience

Auftrag des Betreibers (2026-10-18): Obedience mit den FCI-Klassen 1 bis 3 und den
nationalen Einsteigerklassen von VDH, ÖKV und SKG. Quellen, Prüfprotokoll,
Entscheidungen und offene Punkte: [PO_VERIFIKATION.md](PO_VERIFIKATION.md), Abschnitt
„Obedience“.

*Stand der Länder*

| Land | Inhalt im Katalog |
|---|---|
| **Deutschland** (`DE`) | VDH: Turnierhundsport (12), Agility inkl. A0 (5), Hoopers H0-H3 (4), **Obedience Beginner und Senioren (2)**, dazu die internationalen FCI-Ordnungen |
| **Österreich** (`AT`) | ÖKV: BH/VT (1), Agility LK 1-3 und Jumping (4), Hoopers H1-H3 (3), Breitensport (8), **Obedience Beginner und Senior (2)**, dazu FCI |
| **Schweiz** (`CH`) | SKG: Begleithund 1-3 (3, eigene Sportart `BHCH`), Agility Klasse 1-3 und Jumping (4), **Obedience Beginners (1)**, dazu FCI |
| übrige Länder | nur die internationalen FCI-Ordnungen, jetzt 30 statt 27 (**FCI-Obedience 1-3**) |

*Aufbau*

- Eine neue Sportart `OBEDIENCE` für alle acht Ordnungen (FCI-Obedience 1-3 mit
  `CountryCode` null und englischen Namen/Texten; VDH „DE“, ÖKV „AT“, SKG „CH“, deutsch).
  22 Übungen, 78 Verknüpfungen; „Freifolge“ & Co. sind je eine Übung der Sportart und
  hängen an allen Ordnungen, die sie führen - was je Klasse abweicht, steht in den Hinweisen
  der Ordnungs-Übung (Seitenangabe). Nur „Verhalten gegenüber anderen Hunden“, „Sprung über
  eine Hürde“ und „Bringen auf ebener Erde“ sind rein national und haben keinen englischen
  Namen.
- Die Sportart steht in `FciKatalog.InternationaleSportarten` (wie die BH mit ihrer
  ÖKV-Ordnung): Sie ist in jedem Land sichtbar, das Land steht an der Ordnung. Dafür
  bekam `RegulationSeed` das Feld `Ueberall` (FCI-Ordnung = überall, unabhängig vom Code der
  Sportart). `SeedSportAsync` sucht nur noch die **globale** Sportart (`ClubId` null): Eine
  Vereinssportart mit demselben Code wird nicht mehr zum Ziel des Seeds. Gibt es in einem
  Bestand schon eine globale „Obedience“ unter anderem Code, wird sie mitgenutzt statt doppelt
  angelegt.
- Englisch: nur die FCI-Ordnungen („FCI Obedience Class 1-3“), ihre Beschreibungen und Hinweise
  sowie die 19 Übungen, die sie führen (Name und Bewertung); die gemeinsamen Übungen zeigen
  den englischen Namen und die englische Bewertung auch unter den nationalen Ordnungen, deren
  Hinweise bleiben deutsch (wie bei der ÖKV-BH in E4). `fci-texte-en.json` wuchs um 19
  Bewertungstexte, 3 Beschreibungen und 29 Hinweise (die Datei hat jetzt 164 Einträge für
  Übungen, 30 für Ordnungen und 233 für Hinweise; bei den Übungen zählen die älteren
  Fassungen mit).
- Kategorien der Übungen: „Gruppenübung“, „Freifolge und Positionen“, „Distanzarbeit“,
  „Apportieren und Suchen“, „Gesamteindruck“ (Übersetzung wie die der FCI-Kategorien, `en.ts`).
- Keine Schemaänderung und keine Migration: Der Seeder legt die Daten beim Start an. Eine
  vorhandene Sportartenwahl eines Nutzers bekommt Obedience nicht automatisch dazu
  (`UserSportSelection` ist eine Positivliste, der Seeder legt keine Zeilen an).
- Öffentliche Prüfungsordnungs-Seiten: Neue Familie „Obedience“ für FCI-Obedience und die
  VDH-Klassen (`lib/public-catalog.ts`); die Klassen von ÖKV und SKG stehen in deren
  Länder-Familien.

*Prüfung:* `ObedienceKatalogTests` (je Ordnung Übungen, Punkte aus den Tabellen der Quellen,
Summe 320, Land, Quelle, Version, Seitenangaben; gemeinsame Übungen; Länderfilter; Englisch nur
für FCI; Zahlen im englischen Text; Idempotenz; Bestand, Vereinssportart, Sportartenwahl),
`public-catalog.test.ts` und `katalog-name.test.ts`. Die bestehenden Tests zu Ländern und
Englisch wurden erweitert (Obedience-Ordnungen in den Erwartungslisten, nationale Übungen
nach der Ordnung statt nach der Sportart), nicht abgeschwächt.

---

## Paket RO1 (2026-10-22, Fassung 0.38): Rally Obedience

Auftrag des Betreibers (2026-10-22): Rally Obedience als Sportart - FCI International Class,
VDH und ÖKV, Vorgehen wie bei Obedience. Quellen, Prüfprotokoll, Gruppierung, Entscheidungen und
offene Punkte: [PO_VERIFIKATION.md](PO_VERIFIKATION.md), Abschnitt „Rally Obedience“.

*Stand der Länder*

| Land | Inhalt im Katalog |
|---|---|
| **Deutschland** (`DE`) | VDH: Turnierhundsport (12), Agility inkl. A0 (5), Hoopers H0-H3 (4), Obedience Beginner und Senioren (2), **Rally Obedience RO B, 1, 2, 3, S (5)**, dazu die internationalen FCI-Ordnungen |
| **Österreich** (`AT`) | ÖKV: BH/VT (1), Agility LK 1-3 und Jumping (4), Hoopers H1-H3 (3), Breitensport (8), Obedience Beginner und Senior (2), **Rally Obedience RO B, 1, 2, 3, S (5)**, dazu FCI |
| **Schweiz** (`CH`) | unverändert; kein SKG-Reglement für Rally Obedience gefunden, es gilt die FCI-Klasse |
| übrige Länder | nur die internationalen FCI-Ordnungen, jetzt 31 statt 30 (**FCI-Rally Obedience International**) |

*Aufbau*

- Eine neue Sportart `RALLYO` für alle elf Ordnungen (die FCI-Ordnung mit `CountryCode` null und
  englischen Namen/Texten; VDH „DE“, ÖKV „AT“, deutsch). 24 Übungen, 221 Verknüpfungen. Die
  Übungen sind **Schildgruppen** (89 FCI-, 132 VDH-, 105 ÖKV-Schilder auf 24 Gruppen verteilt); je
  Ordnung nennt der Hinweis der Ordnungs-Übung die Schilder der Klasse mit Nummer, Bezeichnung und
  Seite. Gewertet wird über Abzüge vom Startwert 100: Die Übungen tragen 0 Punkte.
- Englisch: nur die FCI-Ordnung („FCI Rally Obedience International Class“), ihre Beschreibung und
  22 Hinweise sowie die 22 Gruppen, die sie führt (Name und Bewertung). Die Schilder stehen mit ihren
  englischen Originalbezeichnungen auch im deutschen Text. „Bogen und Zickzack“ und „Mehrschildübung“
  kennen nur VDH und ÖKV und bleiben deutsch. `fci-texte-en.json` wuchs um 22 Bewertungstexte,
  1 Beschreibung und 22 Hinweise (jetzt 186 Einträge für Übungen, 31 für Ordnungen und 255 für
  Hinweise).
- Wiederverwendet statt neu gebaut: `RegulationSeed.Ueberall`, die Suche nach der globalen Sportart
  und die Mitnutzung einer gleichnamigen globalen Sportart aus Paket O1 (jetzt als
  `GlobaleSportCodeAsync` für beide Sportarten), die Englisch-Bindung an den deutschen Text
  (`FciTexteEn`) und die Länder-Tests.
- Kategorien: „Positionen“, „Wendungen und Tempo“, „Figuren“, „Schritte und Seitenwechsel“, „Hürde“,
  „Parcours“ und „Distanzarbeit“ (aus dem Obedience); Übersetzung wie die der FCI-Kategorien (`en.ts`).
- Keine Schemaänderung und keine Migration; eine vorhandene Sportartenwahl bekommt Rally Obedience
  nicht automatisch dazu.
- Öffentliche Prüfungsordnungs-Seiten: Neue Familie „Rally Obedience“ für die FCI-Ordnung und die
  VDH-Klassen (`lib/public-catalog.ts`); die ÖKV-Klassen stehen bei „Österreich – ÖKV“.

*Prüfung:* `RallyObedienceKatalogTests` (Ordnungen mit Land, Quelle, Version und Seiten; Parcours-,
Alters- und Notenangaben je Klasse; Gruppen je Ordnung; alle Schilder je Quelle genau einmal; Punkte 0;
Englisch nur für FCI; Zahlen und englische Schildnummern; Länderfilter; Idempotenz; Bestand,
Vereinssportart, Sportartenwahl, Abgrenzung zu Obedience), `public-catalog.test.ts` und
`katalog-name.test.ts`. Die bestehenden Tests zu Ländern und Englisch wurden erweitert (Rally-
Ordnungen in den Erwartungslisten, Zählungen), nicht abgeschwächt.


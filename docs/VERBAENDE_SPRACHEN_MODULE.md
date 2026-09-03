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

Offen: Vereins-Selbstverwaltung (Schritt 2), Sprachen (Schritt 3),
Vereinsregistrierung (Schritt 4). `Locale` ist im Datenmodell schon
vorgesehen, wird aber noch nirgends ausgewertet.

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
   Gruppentraining, Wetter, Statistik.

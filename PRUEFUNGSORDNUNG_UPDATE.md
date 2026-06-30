# Prüfungsordnung aktualisieren (Runbook für Claude)

Anleitung für den Fall, dass eine neue Revision einer Prüfungsordnung
(BH/VT, IBGH, IGP, FH, ...) erscheint und der Übungskatalog
(`SportCatalogSeeder.cs`) entsprechend nachgezogen werden soll - z.B.
weil die FCI/VDH eine neue Version veröffentlicht hat.

Dieser Prozess wurde erstmals im Juni 2026 angewendet, um die UTI-REG-IGP-
de-2025-PDF einzupflegen (BH/IBGH/IGP-Korrekturen + FCI-FH 1-3 neu). Das
Ergebnis lässt sich nicht vollständig automatisieren, da die PDFs Fließtext
und Tabellen mischen und unterschiedlich strukturiert sind - das
Einordnen ("ist das eine eigene Übung oder nur eine Bewertungshinweis-
Variante?", "Pflicht oder Kür?", "welche Punktzahl gehört zu welcher
Stufe?") braucht Lesen mit Verständnis, kein Parsing-Skript. Dieses
Dokument macht den Ablauf trotzdem wiederholbar, indem es genau
festlegt, was bei jedem Durchlauf zu tun ist.

## Voraussetzung: rechtliche Klärung

**Nicht ohne explizite Genehmigung des Auftraggebers (VDH-Vorstand)
echten Prüfungsordnungstext übernehmen.** Der Katalog enthält für die
meisten Sportarten bewusst KEINE Inhalte aus offiziellen
Prüfungsordnungen (Urheberrecht), nur eigene, fachlich angelehnte
Übungsnamen (siehe Klassenkommentar in `SportCatalogSeeder.cs`). Bisherige
Ausnahmen mit expliziter Genehmigung: IGP 1-3, BH, IBGH 1-3, FCI-FH 1-3.
Bei einer neuen Sportart/Prüfungsordnung vor dem Einpflegen kurz
nachfragen, falls die Genehmigung nicht bereits im Auftrag enthalten ist.

## Ablauf

### 1. PDF einlesen

Read-Tool mit `pages`-Parameter in 15-20-Seiten-Blöcken (siehe
Tool-Beschreibung "Maximum 20 pages per request"). Reihenfolge:

1. Inhaltsverzeichnis/erste Seiten - Struktur verstehen (welche
   Prüfungsstufen, welche Abteilungen, wie heißen sie in der PDF).
2. Alle Seiten der betroffenen Prüfung(en) lesen, inkl. der
   Punktetabellen (meist am Anfang jeder Abteilung) UND der
   Übungsbeschreibungen (liefern oft Detailwerte wie Bringholzgewicht,
   Hürdenhöhe, die in `ScoringNotes` gehören).

### 2. Pro Prüfung eine Tabelle bauen (Übung → Stufe → Punkte)

Für jede Prüfungsstufe (z.B. FCI-IGP 1/2/3) eine Liste:
`Übungsname (exakt wie im Fließtext der Tabellenüberschrift) | Punkte | Pflicht/Kür`.

Stolperfallen aus dem letzten Durchlauf, auf die explizit achten:

- **Dieselbe Übung kann in mehreren Stufen mit identischem Namen
  vorkommen** (z.B. "Sitz aus der Bewegung" in IGP1/2/3) - das ist
  normal, jede Stufe ist eine eigene `Sport`-Zeile in unserem Modell,
  braucht also jeweils eine eigene `Exercise`-Deklaration unter dieser
  Sportart (keine Sportart-übergreifende Wiederverwendung nötig).
- **Eine Übung kann innerhalb EINER Prüfung mehrfach mit gleichem Namen,
  aber unterschiedlicher Punktzahl auftauchen** (z.B. "Abwehr eines
  Angriffs aus der Bewachungsphase" kommt in der IGP-Abteilung C an zwei
  Stellen vor). Unser Modell erlaubt nur eine `Exercise` pro
  `(SportId, Name)` - in diesem Fall künstlich unterscheiden, z.B. mit
  einem Klammerzusatz wie "(nach Fluchtversuch)" / "(Schlussphase)", der
  die tatsächliche Position im Ablauf beschreibt.
- **Mehrere Prüfungsordnungen können dieselbe Sportart teilen** (z.B.
  "Fährte" mit den Stufen A/B/C plus jetzt FH 1-3). Das ist der
  ursprüngliche Bug, der zu diesem ganzen Update geführt hat (siehe
  TODO.md "zufällig wirkend") - jede Prüfungsstufe braucht eine eigene
  `Regulation`/`RegulationVersion`, NICHT eine gemeinsame mit allen
  Übungen aller Stufen.
- **Summen nachrechnen.** Jede Abteilung/Prüfung hat eine in der PDF
  angegebene Gesamtpunktzahl (meist 100, manchmal je Abteilung separat,
  am Gesamtergebnis erkennbar) - die eigene Tabelle muss exakt darauf
  aufsummieren. Wenn nicht: Tabelle nochmal gegen die PDF prüfen, nicht
  einfach plausibel auffüllen.

### 3. In `SportCatalogSeeder.cs` eintragen

- **Neue/fehlende Übungen**: als `ExerciseSeed`-Einträge zum
  `SeedSportAsync`-Aufruf der betroffenen Sportart hinzufügen (am Ende
  anhängen, bestehende Einträge nicht löschen - siehe nächster Punkt).
- **Korrektur bestehender, falsch benannter/bepunkteter Übungen**: NICHT
  die bestehenden `ExerciseSeed`-/`RegulationExerciseSeed`-Einträge
  direkt umbenennen oder löschen. Stattdessen:
  1. Die korrekten Übungen zusätzlich deklarieren (auch wenn der Name
     dem alten ähnelt).
  2. Eine NEUE `RegulationVersion` mit höherem `ValidFrom`-Datum anlegen
     (z.B. altes Label "2025" → neues Label "2025-2" oder das tatsächliche
     Versionsdatum aus der PDF), die ausschließlich auf die korrekten
     Übungen verweist.
  3. Die alte, falsche Version NICHT löschen - sie wird durch
     `GetRegulationDetailAsync`s "neueste Version per `ValidFrom`"-Logik
     automatisch nicht mehr verwendet, bleibt aber als Historie/wegen
     möglicher Altreferenzen (Trainingspläne, Tagebucheinträge) bestehen.
  Grund: kein Hard-Delete von Daten, auf die in einer bereits laufenden
  Installation (lokal oder produktiv) schon verwiesen werden könnte.
- Bestehende `RegulationExercise`-Zeilen werden bei jedem Seed-Lauf
  automatisch aktualisiert (siehe `SeedRegulationAsync`s
  Upsert-Logik) - eine Punktkorrektur an einer bereits existierenden
  `RegulationExerciseSeed`-Zeile (gleicher Name, gleiche
  `RegulationVersion`) reicht also aus, dafür ist KEINE neue Version
  nötig. Eine neue Version ist nur nötig, wenn sich die Übungs-NAMEN
  ändern oder Übungen ENTFERNT werden müssen (sonst blieben alte und
  neue gemeinsam in der gleichen Version stehen).

### 4. Konsistenz prüfen

Vor dem Build: jeder in einem `RegulationExerciseSeed` referenzierte
Übungsname muss exakt (Groß-/Kleinschreibung, Leerzeichen) einem
`ExerciseSeed`-Eintrag derselben Sportart entsprechen. Seit diesem
Update wirft `SeedRegulationAsync` bei einer fehlenden Referenz eine
`InvalidOperationException` mit Klartext-Fehlermeldung (Sportart +
gesuchter Name) - ein einfacher `dotnet build` reicht nicht, der Fehler
zeigt sich erst beim nächsten Backend-Start in Development (Seeder läuft
nur dort, siehe `Program.cs`). Zum Prüfen ohne Server-Neustart kann
weiterhin dieses Skript verwendet werden (passt Sport-Variablennamen/
Regulation-Label/Version bei Bedarf an):

```bash
cd backend
python3 << 'PYEOF'
import re
with open('src/Dogity.Infrastructure/Persistence/Seed/SportCatalogSeeder.cs') as f:
    content = f.read()

def get_exercise_names(varname):
    m = re.search(rf'var {varname} = await SeedSportAsync.*?\[(.*?)\]\);', content, re.DOTALL)
    return set(re.findall(r'new\("([^"]+)"', m.group(1)))

def get_regulation_exercise_names(label, version):
    m = re.search(rf'new RegulationSeed\("{re.escape(label)}", "{re.escape(version)}".*?\[(.*?)\]\)\);', content, re.DOTALL)
    return re.findall(r'new\("([^"]+)"', m.group(1))

# Beispiel - Sport-Variable, Regulation-Label und Version-Label anpassen:
available = get_exercise_names("igp1")
used = get_regulation_exercise_names("FCI-IGP 1", "2025-2")
missing = [n for n in used if n not in available]
print(f"{len(used)} referenziert, fehlend: {missing}")
PYEOF
```

### 5. Backend neu starten und live verifizieren

```bash
# Backend-Dev-Server neu starten, damit der Seeder erneut läuft
# (.NET-Pfad ggf. anpassen, siehe README.md "Besonderheiten auf macOS")
export PATH="/opt/homebrew/opt/dotnet@9/libexec:$PATH"
cd backend/src/Dogity.Api && dotnet run --launch-profile https
```

Danach per curl gegen `/api/sports/{sportId}/regulations` und
`/api/sports/regulations/{regulationId}` prüfen, dass:

- die neue/korrigierte Prüfungsordnung mit der erwarteten Übungsliste
  und Punktsumme erscheint,
- alte, falsch benannte Prüfungsordnungen (falls eine neue Version
  angelegt wurde) nicht mehr als "aktuell" zurückkommen,
- ein `POST /api/goals` mit der neuen `regulationId` einen Trainingsplan
  generiert, der ausschließlich die Pflichtübungen dieser Prüfung
  enthält (siehe `TrainingPlanGenerator`).

### 6. Backend-Tests, Build, Commit

```bash
cd backend && dotnet test
cd ../frontend && npx tsc --noEmit && npm run lint && npm run build
```

TODO.md-Eintrag ergänzen (was wurde korrigiert/ergänzt, mit welcher
Quelle/Genehmigung), dann committen und pushen.

## Abgrenzung: Admin-PDF-Import-Tool

Es gibt bereits ein laufendes Admin-Feature
(`IRegulationImportService`/`regulation-import-section.tsx`,
`pdftotext`-basiert) zum Scannen einer lokal abgelegten PDF nach neuen
Übungs-Kandidaten (Name + Punktzahl) und gezieltem Freigeben einzelner
Vorschläge in eine bestehende `RegulationVersion`. Das eignet sich für
**punktuelle Ergänzungen einer bestehenden Version** durch einen Admin
über die UI. Für eine **vollständige Revision** (neue Version, mehrere
Abteilungen, Pflicht/Kür-Unterscheidung, Versions-Supersession wie oben)
ist der manuelle Ablauf in diesem Dokument der richtige Weg - das
Import-Tool kennt weder Abteilungen noch Pflicht/Kür noch
Versions-Supersession und würde alles flach in die aktuelle Version
mischen.

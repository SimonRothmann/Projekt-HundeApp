# Dogity Design System

Version:
1.0

---

# Design Vision

Dogity soll sich an modernen Premium-SaaS-Produkten orientieren.

Referenzen:

- Apple Fitness
- Garmin Connect
- Linear
- Notion
- Vercel
- Strava

Das Design muss vermitteln:

- Vertrauen
- Präzision
- Fortschritt
- Gemeinschaft
- Klarheit

---

# Grundprinzipien

## Mobile First

Primäre Nutzung:

Smartphone

Situation:

- Hundeplatz
- draußen
- eine Hand frei
- Handschuhe möglich

Daher:

- große Bedienelemente
- wenige Klicks
- klare Navigation

---

# Layout

Mobile:

Bottom Navigation - höchstens fünf Ziele (Material: drei bis fünf):

Home · Hunde · Statistiken · Profil

Trainer:innen zusätzlich „Trainer", Admins „Admin". Keine Aktionen in der
Leiste (Apple HIG: Tabs navigieren, sie lösen nichts aus) - häufige
Handgriffe wie „Training erfassen" und „Fährte legen" stehen als Kacheln
auf der Startseite. Selten Besuchtes (Sportarten, Verein) liegt im Profil.
Entschieden 2026-09-10 nach der Messung der Wege (7 Punkte zu je 54 px).

Einzige Ausnahme: Wer Admin und Trainer:in zugleich ist, hat sechs Punkte.
Das ist nur der Betreiber - dafür kein Umbau, sondern kleinere Symbole
(18 statt 20 px) und eine schmalere Pille; Beschriftung 11 px.


---

Desktop:

Sidebar Navigation



Dashboard

Training

Hunde

Gruppen

Kalender

Statistik

Administration


---

# Komponenten

## Buttons

Varianten:

Primary

Secondary

Danger

Ghost


Regeln:

- Mindestgröße 44px
- klare Aktion
- keine überladenen Menüs


---

# Cards

Hauptdarstellung.


Beispiele:

Hund Card:


Foto

Name

Rasse

Aktueller Status

Nächstes Ziel


---

Training Card:


Datum

Übungen

Bewertung

Kommentar


---

# Farben

Basis:

Apple-orientiert (siehe Design Vision "Apple Fitness") - reduziert, neutrale
Graustufen als Grundlage, ein klar erkennbarer Akzentton statt mehrerer
konkurrierender Markenfarben. Ausreichend Kontrast für WCAG AA, aber bewusst
kein maximaler Kontrast (kein reines Schwarz auf Weiß bzw. Weiß auf
Schwarz) - Apple selbst nutzt für Fließtext/Flächen gedämpfte Grautöne statt
der vollen Extreme, das wirkt ruhiger und weniger "blockig". Zustands- und
Theme-Wechsel (Hover, Light/Dark) blenden weich über, statt hart umzuschalten.

Beispiele:

Primär:

Systemblau (wie iOS/macOS "System Blue"), etwas zurückhaltender in Sättigung
als das reine #007AFF

Sekundär/Neutral:

Helles, leicht kühles Grau (Light Mode) bzw. gestuftes Dunkelgrau statt
reinem Schwarz (Dark Mode) - analog zu Apples "Grouped Background", Karten/
Flächen heben sich durch sanfte Stufung statt durch harte Kanten ab

Akzent:

Systemgrün (Erfolg/Fortschritt), ergänzt um Orange und Violett für
Diagramme/Status (analog Apples Health-/Fitness-App-Palette)


---

# Gliederung einer Seite

Drei Ebenen, mehr nicht. Sie unterscheiden sich in Größe UND Art, nicht nur
in der Schriftstärke - eine Seite, auf der alles gleich aussieht, hat keine
Gliederung, auch wenn jede Zeile für sich lesbar ist.

| Ebene | Bauteil | Aussehen |
|---|---|---|
| Abschnitt der Seite | `SectionHeading` | Eingefärbtes Symbol im Kästchen + Titel; kommt NUR am Abschnittsanfang vor |
| Karte | `CardHeader` als eingefärbtes Band | Kennung der Karte (Datum, Name) plus Kennzahlen, farbig vom Inhalt abgesetzt |
| Block in einer Karte | `BlockLabel` | Kleine Versalienzeile mit Symbol, z.B. ÜBUNGEN, FÄHRTE, TRAINER-FEEDBACK |

Zusammengehörende Inhalte bekommen eine eigene Fläche (`bg-surface` mit
`border-surface-border`), nicht nur einen Abstand. Ein Zitat oder eine fremde
Stimme (Tages-Kommentar, Trainer-Feedback) trägt zusätzlich eine farbige Kante
links.

Im Dark Mode nie `bg-muted/…` für solche Flächen: `muted` liegt dort nur knapp
über `card`, die Fläche verschwindet auf dem Telefon. Die `surface`-Tokens
nutzen stattdessen weiße Transparenz.

# Dark Mode

Pflicht.

Abgestimmt nach Material 3 (Google), Apple Human Interface Guidelines,
Radix Colors und WCAG 2.2 (geprüft 2026-09-10). Die Werte stehen in
`frontend/src/app/globals.css`, Block `.dark`.

**Regeln**

1. **Höher = heller.** Seite < Karte < Popover/secondary. Auf Dunkel
   verschwinden Schatten, Helligkeit bleibt sichtbar (M3: surface 6,
   container 12-17, highest 22; Apple: "base" dunkler, "elevated" heller).
   Nie etwas DUNKLER als die Seite hinterlegen, um es abzuheben - das ergibt
   Schwarz auf Schwarz.
2. **Grau leicht zur Markenfarbe getönt** (Radix Colors: zu Indigo/Iris
   gehört „Slate"). Nur ein Hauch, Chroma ≈ 0.012. Beides Extreme wurde auf
   Test gesehen und verworfen: kräftig getöntes Navy ließ die blauen Akzente
   verschwimmen, völlig neutrales Grau wirkte neben dem Indigo fremd
   („das Grau beißt sich mit dem Blau").
3. **Zwei Indigotöne statt einem** (Radix Themes, GitHub, Linear):
   `--primary` ist das kräftige Indigo für Knöpfe und Flächen, mit weißer
   Schrift; `--primary-text` das helle Indigo für Links, Symbole und
   Hervorhebungen im Text. **Für Text und Symbole immer
   `text-primary-text`, nie `text-primary`.** Ein einziger Ton kann nicht
   beides: weiße Schrift verlangt ein dunkleres, lesbarer Text auf dunklem
   Grund ein helleres Blau. Ein heller Knopf mit dunkler Schrift
   (Material 3) war lesbar, wirkte neben dem Grau aber blass.
4. **Kontrast mit Reserve.** Text mindestens 4,5:1, besser 7:1 (Apple).
   Eingabefeld-Ränder, Fokusringe und bedeutungstragende Grafik 3:1
   (WCAG 1.4.11). Dekorative Kartenränder sind davon ausgenommen.
5. **Akzente zurücknehmen.** Leuchtende Farben, die im hellen Modus passen,
   wirken auf Dunkel grell - Helligkeit und Chroma anpassen, Akzente für
   Aktionen und Status aufheben.

**Gemessen** (WCAG-Kontrast, vorher → nachher)

| Paar | vorher | nachher |
|---|---|---|
| Knopfschrift auf Primär | 3,86:1 (durchgefallen) | 4,89:1 |
| Primär als Text auf Karte | 4,52:1 | 7,89:1 (`--primary-text`) |
| Nebentext auf Karte | 7,23:1 | 7,83:1 |
| Eingabefeld-Rand auf Karte | 1,51:1 (durchgefallen) | 3,30:1 |
| Helligkeitsstufe Karte über Seite | ΔL 0,05 | ΔL 0,07 |


---

# Typografie

Modern:

Inter

oder

Geist


---

# Icons

Lucide Icons


---

# Animationen

Dezent:

- Seitenwechsel
- Fortschrittsanzeige
- Erfolgsmeldungen


Keine Spielerei.


---

# Accessibility

Pflicht:

WCAG AA

- Kontraste
- Tastaturbedienung
- Screen Reader


---

# Spezialkomponenten


## Trainingsfortschritt


Darstellung:


BH Vorbereitung

████████░░ 80%


---

## Übungsstatus



✓ sicher

⚠ verbessern

❌ problematisch


---

## Fährtenkarte


Darstellung:

- Route
- Start
- Ende
- Länge

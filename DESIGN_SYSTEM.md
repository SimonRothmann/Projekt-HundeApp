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

Bottom Navigation



Home

Training

Hund

Gruppe

Profil


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
| Karte | `CardHeader` mit `border-b` | Kennung der Karte (Datum, Name) plus Kennzahlen, durch eine Linie vom Inhalt getrennt |
| Block in einer Karte | `BlockLabel` | Kleine Versalienzeile mit Symbol, z.B. ÜBUNGEN, FÄHRTE, TRAINER-FEEDBACK |

Zusammengehörende Inhalte bekommen eine eigene Fläche (`bg-muted/40` mit
feinem Rand), nicht nur einen Abstand. Ein Zitat oder eine fremde Stimme
(Tages-Kommentar, Trainer-Feedback) trägt zusätzlich eine farbige Kante links.

# Dark Mode

Pflicht.


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

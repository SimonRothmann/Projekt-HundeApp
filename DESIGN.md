---
version: 2
name: Dogity
description: Ruhige, mobile Trainings-App für den Hundesport - ein kräftiges Indigo als einzige Markenfarbe auf kühlem Slate-Grau, Farben nur nach Rolle, große Treffflächen für die Hand auf dem Hundeplatz, hell und dunkel gleichwertig.

# Werte in Hex sind Näherungen. Maßgeblich sind die OKLCH-Werte in
# frontend/src/app/globals.css (:root = hell, .dark = dunkel).
colors:
  primary: "#5146ee"            # Knöpfe, aktive Flächen, Fortschritt
  on-primary: "#fdfdff"
  primary-text: "#5146ee"       # Indigo als Text/Symbol; dunkel #abaeff
  background: "#f9fafb"         # Seite; dunkel #0f1015
  card: "#ffffff"               # Karte; dunkel #1f2027
  popover: "#ffffff"            # Fenster, Menüs; dunkel #25262d
  foreground: "#0f182b"         # Text; dunkel #eeeef2
  muted: "#f1f5fa"              # ruhige Fläche (hell); dunkel #2a2b32
  muted-foreground: "#60697b"   # Nebentext; dunkel #b2b3bc
  surface: "muted 75 %"         # Fläche IN einer Karte; dunkel Weiß 6 %
  surface-border: "#dfe6ef"     # dunkel Weiß 14 %
  border: "#dfe6ef"             # Trennlinien, Kartenrand; dunkel Weiß 13 %
  input: "#848d9c"              # Rand von allem, was man antippt; dunkel Weiß 36 %
  ring: "#5146ee"               # Fokus (hell /85, dunkel /50-/60)
  destructive: "#c5002b"        # Fehler, Löschen, überfällig; dunkel #fa6863
  erfolg: "#009966"             # Fläche/Rand (mit /10, /40); dunkel #00bc7d
  erfolg-text: "#007a55"        # Text/Symbol; dunkel #00d492
  warnung: "#fe9a00"            # Fläche/Rand (mit /10, /40)
  warnung-text: "#973c00"       # Text/Symbol; dunkel #ffd230
  accent: "#00bbcb"             # Cyan - nur Verlauf, Diagramm
  accent-2: "#e937e1"           # Fuchsia - nur Verlauf, Diagramm

typography:
  page-title:   { fontFamily: Plus Jakarta Sans, fontSize: 24px, fontWeight: 600, letterSpacing: -0.025em }   # text-2xl font-semibold tracking-tight
  public-title: { fontFamily: Plus Jakarta Sans, fontSize: 30px, fontWeight: 800, letterSpacing: -0.025em }   # öffentliche Seiten, ab sm 36px
  section:      { fontFamily: Plus Jakarta Sans, fontSize: 18px, fontWeight: 600, letterSpacing: -0.025em }   # SectionHeading
  card-title:   { fontFamily: Plus Jakarta Sans, fontSize: 16px, fontWeight: 600 }
  body:         { fontFamily: Plus Jakarta Sans, fontSize: 14px, fontWeight: 400, lineHeight: 1.43 }          # text-sm, Standard in Karten
  body-lg:      { fontFamily: Plus Jakarta Sans, fontSize: 16px, fontWeight: 400 }                            # text-base, Formularfelder auf Touch
  label:        { fontFamily: Plus Jakarta Sans, fontSize: 14px, fontWeight: 500 }                            # Knöpfe, Chips, Beschriftungen
  caption:      { fontFamily: Plus Jakarta Sans, fontSize: 12px, fontWeight: 400 }                            # text-xs, Nebenzeilen
  block-label:  { fontFamily: Plus Jakarta Sans, fontSize: 12px, fontWeight: 600, letterSpacing: 0.05em, textTransform: uppercase }
  micro:        { fontFamily: Plus Jakarta Sans, fontSize: 10px, fontWeight: 400 }                            # nur Achsen-/Balkenbeschriftung, Zählerpunkt

rounded:
  sm: 7px      # rounded-sm
  md: 10px     # rounded-md - Knopf, Eingabe, Hinweisfläche
  lg: 12px     # rounded-lg - Fläche in einer Karte, Symbolkästchen
  xl: 17px     # rounded-xl - Karte, Segment-Umschalter, Liste als Karte
  full: 9999px # Chip, Badge, Avatar, Punkt

spacing:
  base: 4px    # Tailwind-Raster
  card: 16px   # --card-spacing, kompakte Karte 12px
  page-x: 16px # px-4 am Handy, md:px-8
  stack: 24px # gap-6 zwischen Abschnitten einer Seite

components:
  button-primary:   { backgroundColor: "{colors.primary}", textColor: "{colors.on-primary}", rounded: "{rounded.md}", height: 32px, touchMin: 44px }
  button-outline:   { backgroundColor: "{colors.background}", borderColor: "{colors.input}", rounded: "{rounded.md}" }
  button-ghost:     { backgroundColor: transparent, hover: "{colors.muted}" }
  button-destructive: { backgroundColor: "{colors.destructive} /10", textColor: "{colors.destructive}" }
  card:             { backgroundColor: "{colors.card}", ring: "foreground /10", rounded: "{rounded.xl}", padding: "{spacing.card}", shadow: sm, hover: md }
  surface:          { backgroundColor: "{colors.surface}", borderColor: "{colors.surface-border}", rounded: "{rounded.lg}" }
  chip:             { borderColor: "{colors.input}", selected: "primary /10 + {colors.primary-text}", rounded: "{rounded.full}", height: 44px }
  input:            { borderColor: "{colors.input}", rounded: "{rounded.md}", height: 40px, touchMin: 44px, fontSizeTouch: 16px }
  hinweis-warnung:  { backgroundColor: "{colors.warnung} /10", borderColor: "{colors.warnung} /40", textColor: "{colors.warnung-text}", rounded: "{rounded.md}" }
  hinweis-erfolg:   { backgroundColor: "{colors.erfolg} /10", borderColor: "{colors.erfolg} /40", textColor: "{colors.erfolg-text}" }
  hinweis-fehler:   { backgroundColor: "{colors.destructive} /10", borderColor: "{colors.destructive} /40", textColor: "{colors.destructive}" }
  bottom-nav:       { backgroundColor: "{colors.background} /80 + blur", items: 4-6, iconSize: 20px }
---

# Dogity Design

Diese Datei ist die eine Quelle dafür, wie Dogity aussieht und sich anfühlt.
Sie folgt dem DESIGN.md-Format (Google Stitch, Sammlung
[awesome-design-md](https://github.com/VoltAgent/awesome-design-md), MIT):
oben die Tokens, darunter Rollen, Regeln und Grenzen. Wer an der Oberfläche
arbeitet, Mensch oder Agent, liest sie zuerst. Die Werte selbst stehen in
`frontend/src/app/globals.css`, die Bausteine in `frontend/src/components/ui/`.

## Überblick

Dogity wird auf dem Hundeplatz benutzt: draußen, oft in der Sonne, eine Hand
an der Leine, manchmal mit Handschuhen. Daraus folgt fast alles:

- **Mobil zuerst, wie eine native App.** 375 px Breite ist der Maßstab, nie
  waagerechtes Scrollen. Unten eine Leiste mit vier Zielen (Home, Hunde,
  Statistiken, Profil), ab `md` eine Seitenleiste links.
- **Große Treffflächen.** Alles Antippbare ist auf Touch mindestens 44 × 44 px
  (`coarse:min-h-11`), auch wenn es mit der Maus kleiner aussieht.
- **Ruhe statt Effekt.** Eine Markenfarbe (Indigo), kühles Slate-Grau als
  Grund, Farbe nur, wenn sie etwas bedeutet. Weiche Übergänge (200 ms), keine
  Spielerei.
- **Hell und dunkel gleichwertig.** Beide Modi sind gemessen, keiner ist
  „abgeleitet“.

Referenzen für das Gefühl: Apple Fitness und Health (Ruhe, Status in Farbe),
Linear und Radix (zwei Töne je Akzent, getöntes Grau), Strava und Garmin
(Daten zuerst).

## Farben

Jede Farbe im Code hat eine **Rolle**. Tailwind-Rohfarben wie
`text-amber-700` sind verboten. Der Test `lib/design-farben.test.ts` lässt
sie nicht durch.

### Marke
- **Primär** (`primary`): Indigo. Knöpfe, aktive Reiter, Fortschritt,
  gewählte Chips (`primary/10`). Pro Ansicht EIN gefüllter Primärknopf. Was
  daneben steht, ist `outline`, `secondary` oder `ghost`.
- **Primär als Text** (`primary-text`): Links, Symbole, Hervorhebungen im
  Text. **Immer `text-primary-text`, nie `text-primary`.** Hell sind beide
  gleich, dunkel nicht: Weiße Schrift auf dem Knopf braucht ein dunkleres
  Indigo, Text auf dunklem Grund ein helleres (Radix, GitHub, Linear).
- **Cyan / Fuchsia** (`accent`, `accent-2`): nur im Markenverlauf
  (`text-gradient`, Aurora oben auf der Seite) und in Diagrammen
  (`chart-1` bis `chart-5`). Nie auf Knöpfen, nie als Status.

### Flächen
Höher heißt heller. Die Seite (`background`) liegt unter der Karte (`card`),
und die Karte liegt unter Fenster und Menü (`popover`). Zusammengehöriges
innerhalb einer Karte bekommt eine eigene Fläche: `bg-surface` mit
`border-surface-border`, also Übung, Fährte, Kommentar. Im Dark Mode nie
`bg-muted/…` für solche Flächen, denn `muted` liegt dort nur knapp über
`card` und verschwindet auf dem Telefon.

### Text
`foreground` für Inhalt, `muted-foreground` für Nebenzeilen. Beide liegen auf
Karte und Seite über 7:1.

### Status
Wie beim Primär gibt es je Status einen Flächenton und einen Textton:

| Rolle | Fläche / Rand | Text / Symbol | Wofür |
|---|---|---|---|
| Erfolg | `bg-erfolg/10`, `border-erfolg/40` | `text-erfolg-text` | geschafft, sicher, angenommen, gezählt |
| Warnung | `bg-warnung/10`, `border-warnung/40` | `text-warnung-text` | wackelig, bald fällig, Pause, offline |
| Fehler | `bg-destructive/10`, `border-destructive/40` | `text-destructive` | überfällig, abgelehnt, Löschen |

Gemessen: Erfolgstext hat 5,4:1 auf Weiß und 8,4:1 auf dunkler Karte,
Warnungstext 7,1:1 bzw. 11,2:1, beides auch auf der eigenen /10-Fläche über
4,5:1. Zum Vergleich: Das früher verbreitete `text-emerald-600` lag bei
3,7:1. Rein dekorativ, etwa der Pokal einer Vereinssportart, darf der
Flächenton auch als Symbolfarbe dienen (`text-warnung`).

**Ampel der Fährte** (`ampel-gruen/-gelb/-rot`, `ampel-balken-*`): eigene
Tokens, weil sie die **farbsichere Darstellung** kennen
(`data-farben="sicher"`, Palette von Paul Tol: Blau, Gelb, Rot, auch bei
Rot-Grün-Schwäche unterscheidbar). Was eine Bewertung als Farbe zeigt, nutzt
die Ampel. Was nur einen Zustand meldet, nutzt Erfolg, Warnung oder Fehler.
Farbe trägt nie allein: Immer steht ein Symbol oder ein Wort dabei.

### Kontrast
- Text mindestens 4,5:1, besser 7:1.
- Ränder von Eingaben, Fokusringe und bedeutungstragende Grafik mindestens
  3:1 (WCAG 1.4.11). Dekorative Kartenränder sind davon ausgenommen.
- Alles Antippbare mit Rand (Eingabe, Auswahl, Outline-Knopf, Chip) nutzt
  `border-input`. `border` bleibt leise und ist nur für Trennlinien und
  Karten da.
- Fokusring: hell `ring-ring/85`, dunkel `dark:ring-ring/50` bzw. `/60`. Wer
  einen neuen Baustein mit Fokusring baut, setzt beide Klassen.
- Nicht per `opacity` ausgrauen, was man lesen muss, denn das drückt die
  Schrift unter 4,5:1. Ein abgesagter Termin bekommt `line-through
  text-muted-foreground` und ein `Badge variant="destructive"`.

Gemessene Paare (OKLCH → sRGB → WCAG):

| Paar | Wert |
|---|---|
| Weiße Schrift auf Primär (dunkel) | 4,9:1 |
| `primary-text` auf Karte (dunkel) | 7,9:1 |
| Nebentext auf Karte (dunkel) | 7,8:1 |
| `input`-Rand auf Karte / Seite (hell) | 3,4:1 / 3,2:1 |
| `input`-Rand auf Karte (dunkel) | 3,3:1 |
| Fokusring auf Karte / Seite (hell) | 3,5:1 / 3,4:1 |
| `destructive` als Text auf Weiß | 6,2:1 |
| Weiße Schrift auf `destructive` (hell) | 6,0:1 |

## Typografie

**Plus Jakarta Sans** (über `next/font`, Fallback Inter, dann System) für
alles. Hierarchie entsteht durch Größe und Art, nicht nur durch Stärke.

| Stufe | Klassen | Wo |
|---|---|---|
| Seitentitel | `text-2xl font-semibold tracking-tight` | `h1` jeder App-Seite |
| Öffentlicher Titel | `text-3xl font-extrabold tracking-tight text-balance sm:text-4xl` | Startseite, Impressum, Datenschutz, Prüfungsordnungen, Sachkunde |
| Abschnitt | `SectionHeading` (`text-lg font-semibold`, Symbol im Kästchen) | Anfang eines Abschnitts |
| Kartentitel | `CardTitle` | Karte |
| Block | `BlockLabel` (`text-xs font-semibold uppercase tracking-wider`) | Block in einer Karte: ÜBUNGEN, FÄHRTE |
| Fließtext | `text-sm` | Standard in Karten |
| Nebenzeile | `text-xs text-muted-foreground` | Datum, Ort, Hinweis |
| Kleinst | `text-[10px]` | nur Diagrammachsen, Balkenwerte, Zähler am Glockenpunkt |

Gewichte: 400 für Text, 500 (`font-medium`) für Bedienelemente, 600
(`font-semibold`) für Titel. 700 und 800 gibt es nur auf öffentlichen Seiten
und im Schriftzug.

Lange deutsche Wörter: Überschriften trennen automatisch (`hyphens: auto`).
Freie Texte (Namen, Notizen) bekommen `[overflow-wrap:anywhere]`, damit
„Begleithundeprüfung“ bei großer Schrift nicht über 375 px hinausragt.

Die Schriftgröße lässt sich einstellen (Profil → Darstellung: 100 / 112,5 /
125 %). Weil alles in `rem` gerechnet ist, wachsen Knöpfe und Abstände mit.
Darum keine festen Pixelhöhen für Bedienelemente. Formularfelder haben auf
Touch mindestens 16 px, sonst zoomt iOS beim Antippen.

## Layout

### Abstände
- 4-px-Raster (Tailwind). In Karten `--card-spacing` 16 px, kompakt 12 px.
- Seitenrand 16 px am Handy (`px-4`), 32 px ab `md`.
- Zwischen Abschnitten einer Seite `gap-6`, innerhalb eines Abschnitts
  `gap-3` bis `gap-4`, in einer Zeile `gap-2`.
- Unten Luft für die Leiste (`pb-28`) und die Safe Area
  (`env(safe-area-inset-bottom)`).

### Gliederung einer Seite
Drei Ebenen, mehr nicht. Sie unterscheiden sich in Größe UND Art, nicht nur
in der Schriftstärke. Eine Seite, auf der alles gleich aussieht, hat keine
Gliederung.

| Ebene | Baustein | Aussehen |
|---|---|---|
| Abschnitt der Seite | `SectionHeading` | eingefärbtes Symbol im Kästchen und Titel, nur am Abschnittsanfang |
| Karte | `CardHeader` als eingefärbtes Band | Kennung der Karte (Datum, Name) und Kennzahlen |
| Block in einer Karte | `BlockLabel` | kleine Versalienzeile mit Symbol |

Ein Zitat oder eine fremde Stimme (Tageskommentar, Trainer-Feedback) trägt
zusätzlich eine farbige Kante links.

### Responsiv
- **Handy (< 768 px):** eine Spalte, Kopfleiste mit Schriftzug, Glocke und
  Theme oben, Leiste unten. Fenster (`Sheet`) kommen von unten, höchstens
  90 vh hoch.
- **Ab `md`:** Seitenleiste links (240 px), keine Leiste unten, mehr Rand.
- Prüfen bei 375 px, hell UND dunkel, in Chromium UND Safari (iOS-Simulator):
  Safari zeichnet Datumsfelder und Toasts anders.
- Die Leiste unten zeigt höchstens fünf Ziele, Trainer:innen und Admins
  bekommen je eines dazu. Sechs Punkte gibt es nur für Admin und Trainer:in
  zugleich, also den Betreiber, dann mit 18-px-Symbolen und 11-px-Schrift.
  Die Leiste navigiert nur. Aktionen wie „Training eintragen“ stehen auf der
  Seite.

## Ebenen und Tiefe

| Stufe | Token | Wo |
|---|---|---|
| ruhig | `--shadow-sm` | jede Karte (zentral über `[data-slot=card]`), Primärknopf |
| gehoben | `--shadow-md` | Karte bei Hover |
| schwebend | `--shadow-lg` | Fenster, Menü, Toast |
| Glanz | `--shadow-glow` | Primärknopf bei Hover |

Im Dark Mode tragen Schatten kaum, die Helligkeit trägt: Was höher liegt,
ist heller. Nie etwas DUNKLER als die Seite hinterlegen, um es abzuheben,
sonst steht Schwarz auf Schwarz. Grau ist leicht zum Indigo getönt
(Chroma ≈ 0,012). Kräftiges Navy ließ die Akzente verschwimmen, neutrales
Grau wirkte fremd.

## Formen

`--radius` ist 12 px. Davon abgeleitet:
- `rounded-md` für Knopf, Eingabe und Hinweisfläche
- `rounded-lg` für eine Fläche in einer Karte und das Symbolkästchen
- `rounded-xl` für Karte, Segment-Umschalter und eine Liste als Karte
- `rounded-full` für Chip, Badge, Avatar und Punkt

Ineinander liegende Formen werden nach innen kleiner, nie größer.

## Bausteine

Immer zuerst den vorhandenen Baustein aus `components/ui/` nehmen und ihn
nicht nachbauen.

- **Button** (`button.tsx`): `default` (Primär, einer pro Ansicht),
  `outline`, `secondary`, `ghost` (leise Aktion in Listen), `destructive`
  (rote Schrift auf zartem Rot, nicht vollflächig), `link`. Größen `xs` bis
  `lg` und `icon*`, auf Touch immer mindestens 44 px. Beim Drücken skaliert
  er auf 97 %.
- **Card** (`card.tsx`): Ring `foreground/10`, `rounded-xl`, Schatten
  zentral. `size="sm"` für dichte Listen.
- **Chip** (`chip.tsx`): jede Auswahl in Pillenform. `kompakt` für dichte
  Reihen, auf Touch trotzdem 44 px. Gewählt heißt Indigo-Rand,
  `primary/10`-Fläche und optional ein Haken.
- **Segment-Umschalter** (`segment-umschalter.tsx`): zwei bis vier
  gleichrangige Ansichten, `bg-muted p-1 rounded-xl`.
- **Badge** (`badge.tsx`): kurze Zustände. Für Status `variant="outline"`
  mit Status-Tokens oder `destructive`.
- **Input / Select / Switch**: `border-input`, `h-10`, auf Touch 44 px und
  16 px Schrift.
- **Sheet** (`sheet.tsx`): Fenster von unten am Handy, höchstens `90vh`, mit
  Safe-Area-Rand. Der Schließen-Knopf oben rechts.
- **Rückfrage** (`rueckfrage.tsx`, `useRueckfrage`): statt `confirm()`.
  Gefährliches mit `gefaehrlich: true`.
- **Toast** (sonner): kurz. Bei Fehlern die Meldung des Servers. Während einer
  Aufzeichnung oben unter der Kopfleiste, damit nichts über „Beenden“ liegt.
- **SectionHeading / BlockLabel**: siehe Gliederung.
- **Aktionen-Menü** (`aktionen-menue.tsx`): selten Gebrauchtes hinter „⋯“.

## Symbole, Bewegung, Zugänglichkeit

- **Symbole:** nur Lucide, 16 px in Text und Knöpfen (`size-4`), 14 px in
  Nebenzeilen (`size-3.5`). Reine Symbolknöpfe brauchen ein `aria-label`.
  Dekorative Symbole bekommen `aria-hidden`.
- **Bewegung:** dezent. Farbwechsel blenden 200 ms über, Aufklappen
  (`animate-aufklappen`) 200 ms. Ladeflächen pulsieren nur mit
  `motion-safe:`. Keine Animation um ihrer selbst willen.
- **Zugänglichkeit:** WCAG AA ist Pflicht. Sichtbarer Tastaturfokus auf
  allem Bedienbaren (zentral in `globals.css`), Bedienung per Tastatur,
  Beschriftungen für Screenreader, `aria-busy` beim Laden.

## Do's and Don'ts

### Do
- Farben nach Rolle: `primary`, `primary-text`, `erfolg(-text)`,
  `warnung(-text)`, `destructive`, `muted-foreground`, `surface`.
- Einen Primärknopf pro Ansicht, der Rest ruhiger.
- Jede antippbare Fläche mit mindestens 44 px auf Touch.
- Status immer mit Symbol oder Wort, nie Farbe allein.
- Jede Änderung bei 375 px prüfen, hell und dunkel, Chromium und Safari.
- Vorhandene Bausteine nutzen und neue Varianten in den Baustein selbst
  legen.

### Don't
- Keine Tailwind-Rohfarben (`text-emerald-600`, `bg-amber-500/10`). Der Test
  schlägt fehl.
- Nie `text-primary` für Text, sondern `text-primary-text`.
- Kein `bg-muted/…` für Flächen in Karten, sondern `bg-surface`.
- Keine waagerechte Scrollleiste, keine festen Breiten über 375 px.
- Kein `opacity` auf Lesbares, kein `confirm()`, kein `alert()`.
- Cyan und Fuchsia nicht als Status oder auf Knöpfen.
- Keine Aktionen in der unteren Leiste.
- Keine neuen Schriften und keine Gewichte über 600 in der App.

## Anleitung für Agenten

Kurzreferenz: Indigo `primary` für die eine Hauptaktion, `primary-text` für
Links und Symbole. Grau-Slate als Grund. Status über `erfolg`, `warnung` und
`destructive`, je mit `-text` für Schrift. Karten `rounded-xl` mit Ring,
Flächen in Karten `bg-surface`. Schrift Plus Jakarta Sans, Seitentitel
`text-2xl font-semibold tracking-tight`, Fließtext `text-sm`.

Prompt-Bausteine:
- „Baue die Ansicht mit `Card`, gliedere mit `SectionHeading` und
  `BlockLabel`, Status nach DESIGN.md ‚Status‘, prüfe 375 px hell und
  dunkel.“
- „Eine Warnung als Hinweisfläche: `rounded-md border border-warnung/40
  bg-warnung/10 px-3 py-2 text-sm text-warnung-text`, mit Symbol vorne.“

## Bekannte Lücken

- **App-Symbol und PWA-Farbe:** `public/icon.svg` und `manifest.ts`
  (`theme_color #3d86f0`) sind noch Systemblau aus der Zeit vor dem Indigo,
  `layout.tsx` meldet `themeColor #0b0f1a` (Navy der ersten dunklen Fassung).
  Ob das Symbol Indigo wird, entscheidet der Betreiber.
- **Spendenknopf** (`support-button.tsx`): Ko-fi-Rot als fremde Markenfarbe,
  bewusst so belassen.
- **Karte und Teilbild** (`track-map.tsx`, `opengraph-image.tsx`,
  `qr-code.tsx`): feste Hex-Werte, weil Canvas und MapLibre keine
  CSS-Variablen lesen.

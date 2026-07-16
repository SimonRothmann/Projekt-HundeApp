# Entwicklungsroadmap


# Aktuelle Umsetzungsreihenfolge (Audit 2026-07-16)

Integrierte Sequenz aus technischen Schulden und offenen Feature-Wünschen
(Details je Schritt in TODO.md, Abschnitt "Roadmap: Technische Schulden ×
Features"). Reihenfolge ist strikt, jeder Schritt einzeln deploybar:

1. **Sicherheits-Sofortfix:** Passwort-Reset-Tokens nicht mehr in Prod-Logs
2. **Quick-Wins:** `AsSplitQuery` (GPS-Query) + Offline-Queue-4xx-Handling
3. **Katalog fachlich korrekt:** IGP-1-3-Punktwerte gegen offizielle VDH-PO
   prüfen + fehlende Prüfungsstufen (FPr, UPr, GPr, SPr, StöPr, IGP-FH, IAD)
   in einem Seeder-Durchgang
4. **Content-Security-Policy:** erst Report-Only, dann scharf
5. **Hundeseite schnell + wartbar:** Backend (`hasGpsTrack`-Flag,
   Zeitraum-Pagination) → Frontend (Page-Zerlegung, Lazy-Loading) - zweistufig
6. **JWT-Härtung Stufe 1:** SecurityStamp-Validierung + kürzere Token-Laufzeit
7. **E-Mail-Versand** (bewusst ganz hinten, Entscheidung Auftraggeber):
   konfigurationsgetriebener Umschalter + Provider-Anbindung; bis dahin trägt
   der Admin-Benachrichtigungs-Workflow den Passwort-Reset

Zurückgestellt: Cookie-Auth/Refresh-Tokens (erst vor öffentlicher Beta),
Test-Env → Staging, PDF-Export (Druckansicht deckt ab).


# Phase 0

## Fundament

Dauer:

2-4 Wochen


Ziel:

Projektbasis.


Implementieren:

- Repository
- CI/CD
- Docker
- Grundarchitektur
- Datenbank
- Auth


---

# Phase 1

# MVP

Dauer:

3 Monate


## Benutzer

✓ Registrierung

✓ Login

✓ Profil


---

## Hunde

✓ Hunde verwalten

✓ Fotos


---

## Training


✓ Trainingstagebuch

✓ Übungen

✓ Bewertungen


---

## Sport


✓ BH

✓ IBGH1-3

✓ Fährte


---

## Offline


✓ PWA

✓ lokale Speicherung

✓ Synchronisation


---

# Phase 2

## Trainer Plattform


Dauer:

2-3 Monate


Features:

- Gruppen
- Trainerrollen
- Feedback
- Trainingspläne


---

# Phase 3

## Vereinsplattform


Features:

- Vereine
- Mitgliederverwaltung
- Termine
- Prüfungen


---

# Phase 4

## Community


Features:

- Nachrichten
- Teilen
- Kommentare
- Trainingsgruppen


---

# Phase 5

## Intelligenz


Features:

- automatische Trainingsanalyse
- Vorschläge
- KI-Assistent


---

# Phase 6

## Marketplace


Features:

- Trainer suchen
- Seminare
- Veranstaltungen


---

# Priorität

Immer:

1. Nutzerwert
2. Stabilität
3. Wartbarkeit
4. Skalierung
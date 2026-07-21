# Entwicklungsroadmap


# Aktuelle Umsetzungsreihenfolge (Audit 2026-07-16, Stand 2026-07-17)

Integrierte Sequenz aus technischen Schulden und Feature-Wünschen (Details je
Schritt in TODO.md). Fast alles erledigt und auf Prod:

1. ✅ **Sicherheits-Sofortfix:** Passwort-Reset-Tokens nicht mehr in Prod-Logs
2. ✅ **Quick-Wins:** `AsSplitQuery` (GPS-Query) + Offline-Queue-4xx-Handling
3. ✅ **Katalog fachlich korrekt:** IGP-1-3-Punktwerte gegen offizielle FCI-PO
   geprüft + 6 fehlende Prüfungsfamilien (FPr, UPr, GPr, SPr, StöPr, IAD, IGP-FH)
4. ⏳ **Content-Security-Policy:** Report-Only auf Prod - scharf schalten nach
   Beobachtungswoche (ab ~2026-07-23), EINZIGER offener Punkt
5. ✅ **Hundeseite schnell + wartbar:** `hasGpsTrack`-Flag + Zeitraum-Pagination
   + Page-Zerlegung + GPS-Lazy-Loading
6. ✅ **Refresh-Token-Rotation** (statt nur JWT-Härtung): eingeloggt bleiben ohne
   Neu-Login, kurzer Access-Token (60 min), widerrufbare Sessions
7. ✅ **Tagebuch auf Tages-Ebene** + Übungs-/Tages-/Ablauf-Kommentare

**E-Mail-Versand: Prio sehr sehr niedrig / zurückgestellt** (Nutzer:
irrelevant, 2026-07-17). Der In-App-Benachrichtigungs-Workflow deckt den
Passwort-Reset vollständig.

Zurückgestellt: Cookie-Auth für den Refresh-Token (spätere Härtung),
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
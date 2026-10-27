---
name: mobile-app-first-no-horizontal-scroll
description: "Grundregel Dogity-Frontend — jede Änderung muss sich wie eine native App verhalten (Mobile-App-Design-first, kein horizontaler Scroll)"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-07-23T21:10:10.279Z
---

Bei jeder Frontend-Änderung an Dogity gilt **Mobile-App-Design-first als Grundregel**: Die Seite muss sich wie eine native App verhalten. Insbesondere darf es **keinen horizontalen Scroll** geben — Inhalte skalieren immer auf die Gerätebreite, brechen um oder kürzen sich ab, statt breiter als der Viewport zu werden.

**Why:** Der Nutzer misst die App-Qualität daran, dass sie sich wie eine echte App anfühlt; horizontaler Scroll ist für ihn ein sofortiges Ausschlusskriterium („kaum eine App hat horizontalen Scroll"). Wurde mehrfach als Feedback wiederholt (Trainingsplan-Kommentare, Notiz-Editor).

**How to apply:**
- Jede neue/geänderte UI zuerst bei ~375 px Viewport denken und prüfen; nichts darf über die Viewport-Breite hinausragen.
- Textfelder/Inputs für längere Inhalte: umbrechen (textarea) statt einzeilig + horizontal quetschen. Lange Anzeige-Texte per `truncate`/`break-words` bändigen. Flex/Grid-Kinder brauchen `min-w-0`, damit `truncate` greift und `min-content` nicht die Seite aufbläht.
- Verifikation nicht dem In-App-Browser blind glauben: dessen Layout-Viewport kann sich fälschlich auf >375 aufblähen. Zuverlässiger Test: den Container hart auf 375 px zwingen und prüfen, ob ein Blatt-Element rechts übersteht.

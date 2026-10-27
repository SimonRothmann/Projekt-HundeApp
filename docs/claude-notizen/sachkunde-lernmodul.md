---
name: sachkunde-lernmodul
description: "Sachkunde-Fragentrainer (swhv BH/VT) ist umgesetzt; Nutzer hat die Rechtefrage entschieden, Prüfungssimulation bewusst offen"
metadata: 
  node_type: memory
  type: project
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-09-01T21:12:37.242Z
---

Der Sachkunde-Fragentrainer in Dogity ist seit 2026-09-01 umgesetzt und auf master (`d06589d`). Fragenkataloge des swhv zur BH/VT-Sachkundeprüfung, 72 Fragen Erwachsene + 40 Jugend, Lern-App nach dem Muster der Führerschein-Trainer.

**Die Rechtefrage ist entschieden.** Ich hatte auf das Urheberrecht am Fragenkatalog hingewiesen; der Nutzer hat am 2026-09-01 entschieden: „die sind öffentlich und damit zugänglich, jedes Mitglied der App darf die nutzen". Das ist dokumentiert im Kopf des `SachkundeSeeder` — nicht erneut aufwerfen.

**Bewusst NICHT gebaut: die Prüfungssimulation.** Die Bewertungsregel (2 Punkte je richtige Antwort, 2 Abzug je falsch angekreuzte, bestanden ab 70 %) steht nur auf Vereinsseiten, in keinem der vier swhv-PDFs. Ohne die Durchführungsbestimmung des swhv würde die Simulation falsch rechnen. Bevor jemand sie baut: Regel beim swhv bestätigen lassen — und dabei gleich klären, ob beim Erwachsenenbogen mehrere Antworten richtig sein können (im Netz behauptet, in der vorliegenden Fassung hat jede Frage genau eine markierte Lösung).

Details in `docs/SACHKUNDE.md`, Import über `scripts/import-sachkunde.py`. Siehe auch [[smart-training-plan-generator]] (dieselbe Leitner-Mechanik, aber am Hund statt am Nutzer) und [[deployment-workflow-user-deploys]].

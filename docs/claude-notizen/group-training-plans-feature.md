---
name: group-training-plans-feature
description: Notiertes künftiges Dogity-Feature — Gruppentrainings-Wochenpläne (Welpen/Junghunde) für Trainer; Brief in docs/GROUP_TRAINING_PLANS.md
metadata: 
  node_type: memory
  type: project
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-07-24T13:17:35.821Z
---

Vom Nutzer notiertes **nächstes Feature nach dem adaptiven Trainingsplan**: **Gruppentrainings-Pläne**. Voller Brief: `docs/GROUP_TRAINING_PLANS.md`.

Kern: Trainer bekommen **pro Trainingsgruppe** einen **Wochenplan** (Gruppentraining meist 1–2×/Woche) mit **abwechslungsreichen Übungen**. Schwerpunkte je Gruppentyp:
- **Junghunde**: Leinenführigkeit, Ablage, Hinterhandarbeit, Gesamt-Koordination, Alltagstraining, Umwelt-/Ablenkungstraining (z.B. Parkplatz mit spielenden Kindern).
- **Welpen**: andere Schwerpunkte (Sozialisierung, spielerische Grundlagen, Umweltgewöhnung …).

Design-Hooks: bestehende `Group`/`GroupMember` nutzen; `GroupTrainingPlan` je Gruppe/Woche; Gruppentyp-Templates mit gewichteter Themenliste; Abwechslungs-/Slot-Budget-Logik aus [[smart-training-plan-generator]] wiederverwendbar (aber gruppen- statt hundebezogen, keine per-Hund-Mastery).

**Reihenfolge:** erst adaptiver Einzel-Trainingsplan (P1 erledigt), dann dieses Feature.

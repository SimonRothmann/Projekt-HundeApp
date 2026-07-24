# Gruppentrainings-Pläne — Feature-Brief

Status: **notiert / geplant**. Umsetzung **nach** dem adaptiven Trainingsplan-Generator
(siehe `docs/SMART_TRAINING_PLAN.md`). Dies ist eine Ideen-/Anforderungsnotiz, noch kein Detailplan.

## Idee
Trainer erhalten **pro Trainingsgruppe** einen **Wochenplan** für das Gruppentraining
(z.B. Welpengruppe, Junghundegruppe). Gruppentrainings finden meist **1–2× pro Woche**
statt und brauchen je Woche einen Plan mit **abwechslungsreichen Übungen**, den der
Trainer für die Gruppe durchführt.

## Anforderungen (vom Nutzer)
- Plan **pro Gruppe und pro Woche**, für den/die Trainer der Gruppe.
- **Abwechslung** über die Wochen (nicht jede Woche dasselbe).
- **Themen/Schwerpunkte je nach Gruppentyp**. Beispiele:
  - **Junghunde**: Leinenführigkeit, Ablage, Hinterhandarbeit, Gesamt-Koordination,
    Alltagstraining, Umwelt-/Ablenkungstraining (z.B. Parkplatztraining mit spielenden
    Kindern), …
  - **Welpen**: andere Schwerpunkte (z.B. Sozialisierung, spielerische Grundlagen,
    Umweltgewöhnung, erste Signale) — Themengewichtung unterscheidet sich vom Junghund.
- Der Trainer soll pro Woche einen fertigen, abwechslungsreichen Übungs-Mix bekommen,
  der zu den Schwerpunkten der Gruppe passt.

## Erste Design-Hooks (grob, für später)
- **Bezug zu bestehenden Entities**: Es gibt bereits `Group`/`GroupMember` (Community).
  Ein `GroupTrainingPlan` je Gruppe (Wochen-basiert), analog zur Struktur des
  Einzel-Trainingsplans (Woche → Übungen), aber **gruppen-** statt hundebezogen.
- **Gruppentyp/Themen-Templates**: `GroupType` (z.B. Welpen/Junghunde) mit einer
  gewichteten **Themen-/Fokusliste** (Leinenführigkeit, Ablage, Hinterhand,
  Koordination, Alltag, Umweltreize, Sozialisierung …). Übungen/Themen ggf. als
  eigener Katalog oder über Kategorien der bestehenden `Exercise`.
- **Abwechslungs-Logik**: pro Woche einen Mix über die Fokusbereiche wählen und über die
  Wochen rotieren — die **Variety-/Slot-Budget-Idee** aus dem Smart-Generator lässt sich
  wiederverwenden, hier aber **gruppenbezogen** (keine per-Hund-Mastery, sondern
  Themen-Abdeckung/Rotation).
- **Trainer-Ansicht**: Wochenplan pro Gruppe (evtl. druck-/exportierbar wie die
  Hunde-Druckansicht), inkl. Hinweisen zu Ablenkungssettings (Parkplatz/Kinder etc.).
- Offen: eigener Übungskatalog für Gruppen vs. Wiederverwendung des bestehenden;
  wie stark parametrierbar (Trainer passt Themen/Umfang an); 1× vs. 2× pro Woche.

## Reihenfolge
Zuerst der adaptive Einzel-Trainingsplan (P1–P5), **danach** dieses Feature.

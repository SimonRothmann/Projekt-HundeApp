"use client";

import { useEffect, useState } from "react";
import type { Goal, NextStage, Sport, TrainingPlanItem } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { Plus, Target } from "lucide-react";
import { GoalCreateForm } from "@/components/dogs/goal-create-form";
import { GoalPlanCard } from "@/components/dogs/goal-plan-card";

import { useT } from "@/lib/i18n";
// "goals"/"onChanged" kommen von der Eltern-Seite statt aus einem eigenen
// Fetch hier (siehe dogs/[id]/page.tsx) - die Seite braucht dieselben Daten
// ohnehin für das Eintragen-Fenster, und nur ein gemeinsamer State stellt
// sicher, dass der Fortschritt hier sofort sichtbar wird, sobald dort ein
// verknüpftes Training gespeichert wird.
//
// Diese Komponente orchestriert nur noch: Anlege-Formular ein-/ausblenden
// und die Ziel-Karten auflisten. Die Anlege-Logik lebt in GoalCreateForm,
// die gesamte Plan-/Übungslogik pro Ziel in GoalPlanCard.
//
// Erreichte Ziele zeigt diese Liste nicht mehr: Sie stehen mit Prüfungstag und
// Punkten in der Karte "Leistungen" (leistungen-card.tsx).
export function GoalsSection({
  dogId,
  dogName,
  sports,
  goals,
  onEintragen,
  onChanged,
}: {
  dogId: string;
  dogName: string;
  sports: Sport[];
  goals: Goal[] | null;
  /** Eine Planübung wurde angetippt: das Eintragen-Fenster mit ihr öffnen. */
  onEintragen: (item: TrainingPlanItem) => void;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const [showForm, setShowForm] = useState(false);
  // Folgestufe, mit der das Formular geöffnet wurde ("Ergebnis eintragen" ->
  // "IGP 2 als Ziel anlegen"). Bewusst über Props durchgereicht und nicht über
  // den Merkzettel der Prüfungsordnungs-Seite (lib/start-po.ts): Der gilt für
  // Neuanmeldungen, hier ist die Person längst drin.
  const [vorauswahl, setVorauswahl] = useState<NextStage | null>(null);

  async function handleCreated() {
    setShowForm(false);
    setVorauswahl(null);
    await onChanged();
  }

  function handleFolgeziel(stufe: NextStage) {
    setVorauswahl(stufe);
    setShowForm(true);
  }

  // Zum Formular springen, wenn es über ein Folgeziel aufgeht - sonst bliebe
  // es unter dem gerade verschwundenen Dialog außer Sicht.
  useEffect(() => {
    if (vorauswahl) document.getElementById("ziel-formular")?.scrollIntoView({ behavior: "auto", block: "start" });
  }, [vorauswahl]);

  const sichtbar = goals?.filter((g) => g.status !== 1) ?? [];

  return (
    // scroll-mt: der Kopfbereich der App klebt oben - ohne den Abstand
    // verschwände die Überschrift beim Sprung auf #trainingsplan darunter.
    <div id="trainingsplan" className="flex scroll-mt-20 flex-col gap-3">
      <SectionHeading
        icon={Target}
        title={t("Ziele & Trainingsplan")}
        action={
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              setVorauswahl(null);
              setShowForm((v) => !v);
            }}
          >
            <Plus className="size-4" />
            {t("Ziel setzen")}
          </Button>
        }
      />

      {showForm && (
        <div id="ziel-formular" className="scroll-mt-20">
          <GoalCreateForm
            key={vorauswahl?.regulationId ?? "neu"}
            dogId={dogId}
            sports={sports}
            vorauswahl={vorauswahl}
            onCreated={handleCreated}
          />
        </div>
      )}

      {goals === null ? (
        <p className="text-muted-foreground">{t("Lädt…")}</p>
      ) : sichtbar.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-10 text-center text-muted-foreground">
            <Target className="size-8" />
            <p>{goals.length === 0 ? t("Noch kein Ziel gesetzt.") : t("Kein aktives Ziel.")}</p>
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {sichtbar.map((goal) => (
            <GoalPlanCard
              key={goal.id}
              goal={goal}
              dogName={dogName}
              onEintragen={onEintragen}
              onChanged={onChanged}
              onFolgeziel={handleFolgeziel}
            />
          ))}
        </div>
      )}
    </div>
  );
}

"use client";

import { useState } from "react";
import type { Goal, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Plus, Target } from "lucide-react";
import { GoalCreateForm } from "@/components/dogs/goal-create-form";
import { GoalPlanCard } from "@/components/dogs/goal-plan-card";

// "goals"/"onChanged" kommen von der Eltern-Seite statt aus einem eigenen
// Fetch hier (siehe dogs/[id]/page.tsx) - die Seite braucht dieselben Daten
// ohnehin für die "Plan-Ziel"-Auswahl im Trainingstagebuch-Formular, und nur
// ein gemeinsamer State stellt sicher, dass der Fortschritt hier sofort
// sichtbar wird, sobald dort ein verknüpftes Training gespeichert wird.
//
// Diese Komponente orchestriert nur noch: Anlege-Formular ein-/ausblenden
// und die Ziel-Karten auflisten. Die Anlege-Logik lebt in GoalCreateForm,
// die gesamte Plan-/Übungs-/Schnelleintrag-Logik pro Ziel in GoalPlanCard.
export function GoalsSection({
  dogId,
  sports,
  goals,
  onChanged,
}: {
  dogId: string;
  sports: Sport[];
  goals: Goal[] | null;
  onChanged: () => Promise<void>;
}) {
  const [showForm, setShowForm] = useState(false);

  async function handleCreated() {
    setShowForm(false);
    await onChanged();
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Ziele & Trainingsplan</h2>
        <Button size="sm" variant="outline" onClick={() => setShowForm((v) => !v)}>
          <Plus className="size-4" />
          Ziel setzen
        </Button>
      </div>

      {showForm && <GoalCreateForm dogId={dogId} sports={sports} onCreated={handleCreated} />}

      {goals === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : goals.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-10 text-center text-muted-foreground">
            <Target className="size-8" />
            <p>Noch kein Ziel gesetzt.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {goals.map((goal) => (
            <GoalPlanCard key={goal.id} goal={goal} dogId={dogId} onChanged={onChanged} />
          ))}
        </div>
      )}
    </div>
  );
}

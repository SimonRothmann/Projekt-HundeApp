"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Group } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const MIN_HOECHSTALTER_WOCHEN = 8;
const MAX_HOECHSTALTER_WOCHEN = 104;

/**
 * Name, Beschreibung und Höchstalter der Gruppe. Das Formular steht direkt da; "Speichern"
 * ist erst aktiv, wenn sich etwas geändert hat - so sieht man auch, dass
 * nichts gespeichert werden muss.
 *
 * Für jede:n Trainer:in des Vereins möglich (Backend prüft die Rechte).
 */
export function GroupEditForm({ group, onGespeichert }: { group: Group; onGespeichert: () => Promise<void> }) {
  const t = useT();
  const [name, setName] = useState(group.name);
  const [beschreibung, setBeschreibung] = useState(group.description ?? "");
  const [hoechstalter, setHoechstalter] = useState(group.maxAgeWeeks?.toString() ?? "");
  const [speichert, setSpeichert] = useState(false);

  const wochen = hoechstalter.trim() === "" ? null : Number(hoechstalter);
  const wochenGueltig = wochen === null || (Number.isInteger(wochen) && wochen >= MIN_HOECHSTALTER_WOCHEN && wochen <= MAX_HOECHSTALTER_WOCHEN);
  const geaendert =
    name.trim() !== group.name || beschreibung.trim() !== (group.description ?? "") || wochen !== group.maxAgeWeeks;

  async function speichern(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) {
      toast.error(t("Name ist erforderlich."));
      return;
    }
    if (!wochenGueltig) {
      toast.error(t("Das Höchstalter muss zwischen {min} und {max} Wochen liegen.", { min: MIN_HOECHSTALTER_WOCHEN, max: MAX_HOECHSTALTER_WOCHEN }));
      return;
    }
    setSpeichert(true);
    try {
      await api.put(`/api/groups/${group.id}`, { name: name.trim(), description: beschreibung.trim() || null, maxAgeWeeks: wochen });
      toast.success(t("Gruppe aktualisiert."));
      await onGespeichert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Gruppe konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={speichern} className="flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="edit-group-name">{t("Name")}</Label>
        <Input id="edit-group-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={200} />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="edit-group-desc">{t("Beschreibung (optional)")}</Label>
        <Input id="edit-group-desc" value={beschreibung} onChange={(e) => setBeschreibung(e.target.value)} maxLength={500} />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="edit-group-maxalter">{t("Hunde bis … Wochen (optional)")}</Label>
        <Input
          id="edit-group-maxalter"
          type="number"
          inputMode="numeric"
          min={MIN_HOECHSTALTER_WOCHEN}
          max={MAX_HOECHSTALTER_WOCHEN}
          value={hoechstalter}
          onChange={(e) => setHoechstalter(e.target.value)}
          aria-describedby="edit-group-maxalter-hinweis"
        />
        <p id="edit-group-maxalter-hinweis" className="text-sm text-muted-foreground">
          {t("Für Welpengruppen: In der Anmeldeliste ist markiert, wer herauswächst, und lässt sich in eine andere Gruppe übernehmen.")}
        </p>
      </div>
      <Button type="submit" className="h-11 self-start" disabled={speichert || !geaendert}>
        {speichert ? t("Speichert…") : t("Speichern")}
      </Button>
    </form>
  );
}

"use client";

import { useState } from "react";
import { Check, Pencil, Plus, StickyNote, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { BlockLabel } from "@/components/ui/block-label";
import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { zielText } from "@/lib/merkzettel";
import type { MerkzettelEintrag } from "@/lib/types";
import { cn } from "@/lib/utils";

/**
 * Der Merkzettel eines Hundes, oben im Reiter "Plan" - nur mit offenen Einträgen. Jeder Eintrag zeigt sein Ziel
 * ("Di 18:00"), lässt sich abhaken (mit "Rückgängig"), ändern und löschen. Nur Besitzer:innen sehen ihn.
 */
export function MerkzettelListe({
  dogId,
  eintraege,
  onMerken,
  onBearbeiten,
  onChanged,
}: {
  dogId: string;
  eintraege: MerkzettelEintrag[];
  onMerken: () => void;
  onBearbeiten: (eintrag: MerkzettelEintrag) => void;
  onChanged: () => void;
}) {
  const t = useT();
  const sprache = useSprache();
  // Die Einträge, die gerade zum Server unterwegs sind: kein zweiter Tipp, bis die Antwort da ist.
  const [beschaeftigt, setBeschaeftigt] = useState<ReadonlySet<string>>(new Set());

  async function mitSperre(id: string, aktion: () => Promise<void>) {
    setBeschaeftigt((vorher) => new Set(vorher).add(id));
    try {
      await aktion();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das ging gerade nicht. Bitte noch einmal versuchen."));
    } finally {
      setBeschaeftigt((vorher) => new Set([...vorher].filter((andere) => andere !== id)));
    }
  }

  function abhaken(eintrag: MerkzettelEintrag) {
    return mitSperre(eintrag.id, async () => {
      await api.put(`/api/dogs/${dogId}/merkzettel/${eintrag.id}/erledigt`, { done: true });
      onChanged();
      toast.success(t("Abgehakt."), {
        action: {
          label: t("Rückgängig"),
          onClick: () => {
            void api.put(`/api/dogs/${dogId}/merkzettel/${eintrag.id}/erledigt`, { done: false }).then(onChanged, () => {
              toast.error(t("Das ging gerade nicht. Bitte noch einmal versuchen."));
            });
          },
        },
      });
    });
  }

  function loeschen(eintrag: MerkzettelEintrag) {
    if (!window.confirm(t("Diesen Eintrag wirklich löschen?"))) return Promise.resolve();
    return mitSperre(eintrag.id, async () => {
      await api.delete(`/api/dogs/${dogId}/merkzettel/${eintrag.id}`);
      toast.success(t("Eintrag gelöscht."));
      onChanged();
    });
  }

  return (
    <section className="flex min-w-0 flex-col gap-2 rounded-xl border border-surface-border bg-surface p-3" aria-label={t("Merkzettel")}>
      <div className="flex items-center justify-between gap-2">
        <BlockLabel icon={StickyNote}>{t("Fürs nächste Training")}</BlockLabel>
        <Button type="button" size="sm" variant="ghost" className="min-h-11 shrink-0" onClick={onMerken}>
          <Plus className="size-4" aria-hidden />
          {t("Merken")}
        </Button>
      </div>
      <ul className="flex min-w-0 flex-col divide-y divide-surface-border">
        {eintraege.map((eintrag) => {
          const wartet = beschaeftigt.has(eintrag.id);
          // Die Übung steht nur da, wenn sie etwas anderes sagt als der Text (der Text beginnt oft mit ihrem Namen).
          const uebung = eintrag.exerciseName && eintrag.exerciseName !== eintrag.text
            ? katalogName({ name: eintrag.exerciseName, nameEn: eintrag.exerciseNameEn }, sprache)
            : null;
          return (
            <li key={eintrag.id} className="flex min-w-0 items-start gap-1 py-1">
              <button
                type="button"
                role="checkbox"
                aria-checked={false}
                aria-label={t("Abhaken: {text}", { text: eintrag.text })}
                disabled={wartet}
                onClick={() => void abhaken(eintrag)}
                className="flex size-11 shrink-0 items-center justify-center rounded-full disabled:opacity-50"
              >
                <span className="flex size-6 items-center justify-center rounded-md border border-input">
                  <Check className="size-4 opacity-0" aria-hidden />
                </span>
              </button>
              <div className="min-w-0 flex-1 py-2.5">
                <p className="text-sm [overflow-wrap:anywhere]">{eintrag.text}</p>
                <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
                  {[zielText(t, sprache, eintrag), uebung].filter(Boolean).join(" · ")}
                </p>
              </div>
              <Button
                type="button"
                size="icon"
                variant="ghost"
                className="size-11 shrink-0"
                aria-label={t("Eintrag ändern")}
                disabled={wartet}
                onClick={() => onBearbeiten(eintrag)}
              >
                <Pencil className="size-4" />
              </Button>
              <Button
                type="button"
                size="icon"
                variant="ghost"
                className={cn("size-11 shrink-0 text-destructive hover:text-destructive")}
                aria-label={t("Eintrag löschen")}
                disabled={wartet}
                onClick={() => void loeschen(eintrag)}
              >
                <Trash2 className="size-4" />
              </Button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

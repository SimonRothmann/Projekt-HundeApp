"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Archive, ArchiveRestore, ChevronDown, Pencil, Printer, Trash2, Users } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import type { Dog, DogOwner } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { CoOwnersSection } from "@/components/dogs/co-owners-section";
import { useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

const ZEILE =
  "flex min-h-12 w-full items-center gap-3 rounded-lg border border-surface-border bg-surface px-3 text-left text-sm font-medium transition-colors hover:bg-muted";

/**
 * Das Menü "Verwalten" der Hundeseite als Sheet: alles, was den Hund selbst
 * betrifft und nicht sein Training - Bearbeiten, Drucken/Exportieren,
 * Mitbesitzer, Archivieren, Endgültig löschen. Früher standen die letzten drei
 * als Karten am Seitenende, hinter dem ganzen Tagebuch.
 *
 * Gezeigt wird, was die Person darf: Besitzer:innen (auch Mitbesitzer:innen)
 * alles, eine betreuende Trainer:in nur das Drucken. Die Rückfragen vor dem
 * Archivieren und Löschen sind unverändert.
 */
export function DogActionsSheet({
  dog,
  isOwner,
  owners,
  currentUserId,
  open,
  onOpenChange,
  onEdit,
  onChanged,
}: {
  dog: Dog;
  isOwner: boolean;
  owners: DogOwner[];
  currentUserId: string | undefined;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Öffnet das Bearbeiten des Hundes (das Formular steht auf der Seite). */
  onEdit: () => void;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const router = useRouter();
  const [mitbesitzerOffen, setMitbesitzerOffen] = useState(false);

  async function deleteDog() {
    // Doppelte Bestätigung: Hunde-Löschen entfernt Trainings, Fährten, Ziele
    // und Trainerzuweisungen mit - deutlich schwerwiegender als das Löschen
    // einer einzelnen Session, deshalb zusätzlich Name-Bestätigung.
    if (!confirm(t("Hund „{name}“ wirklich löschen? Alle Trainings, Fährten, Ziele und Trainerzuweisungen werden entfernt.", { name: dog.name }))) return;
    const confirmName = prompt(t("Zum Bestätigen bitte den Namen des Hundes eingeben: „{name}“", { name: dog.name }));
    if (confirmName?.trim() !== dog.name) {
      if (confirmName !== null) toast.error(t("Name stimmt nicht - Löschen abgebrochen."));
      return;
    }
    try {
      await api.delete(`/api/dogs/${dog.id}`);
      toast.success(t("Hund „{name}“ gelöscht.", { name: dog.name }));
      router.push("/dogs");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Löschen fehlgeschlagen."));
    }
  }

  async function setArchived(archived: boolean) {
    // Archivieren blendet den Hund nur aus (reversibel, Daten bleiben) - daher
    // nur beim Archivieren eine leichte Rückfrage, das Aufheben ist harmlos.
    if (archived && !confirm(t("Hund „{name}“ archivieren? Er wird aus deiner aktiven Liste ausgeblendet, alle Daten bleiben erhalten.", { name: dog.name }))) return;
    try {
      await api.put(`/api/dogs/${dog.id}/archive`, { archived });
      toast.success(archived ? t("„{name}“ archiviert.", { name: dog.name }) : t("„{name}“ wieder aktiviert.", { name: dog.name }));
      onOpenChange(false);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Aktion fehlgeschlagen."));
    }
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="bottom" className="max-h-[85vh] overflow-y-auto pb-[max(1rem,env(safe-area-inset-bottom))]">
        <SheetHeader>
          <SheetTitle>{t("Verwalten")}</SheetTitle>
          <SheetDescription className="[overflow-wrap:anywhere]">{dog.name}</SheetDescription>
        </SheetHeader>
        <div className="flex flex-col gap-2 px-4">
          {isOwner && (
            <button
              type="button"
              className={ZEILE}
              onClick={() => {
                onOpenChange(false);
                onEdit();
              }}
            >
              <Pencil className="size-4 shrink-0" />
              {t("Bearbeiten")}
            </button>
          )}
          <Link href={`/dogs/${dog.id}/print`} className={ZEILE}>
            <Printer className="size-4 shrink-0" />
            {t("Drucken / Exportieren")}
          </Link>

          {isOwner && (
            <>
              <button
                type="button"
                className={ZEILE}
                aria-expanded={mitbesitzerOffen}
                onClick={() => setMitbesitzerOffen((v) => !v)}
              >
                <Users className="size-4 shrink-0" />
                <span className="min-w-0 flex-1">{t("Mitbesitzer")}</span>
                <ChevronDown className={cn("size-4 shrink-0 text-muted-foreground transition-transform", mitbesitzerOffen && "rotate-180")} />
              </button>
              {mitbesitzerOffen && (
                <CoOwnersSection
                  dogId={dog.id}
                  dogName={dog.name}
                  owners={owners}
                  currentUserId={currentUserId}
                  onChanged={onChanged}
                />
              )}

              {/* Archivieren: reversibel, blendet nur aus. */}
              <div className="mt-2 flex flex-col gap-2 rounded-lg border border-surface-border p-3">
                <div className="min-w-0">
                  <p className="font-medium">{dog.archivedAt ? t("Hund ist archiviert") : t("Hund archivieren")}</p>
                  <p className="text-sm text-muted-foreground">
                    {dog.archivedAt
                      ? t("Ausgeblendet aus deiner aktiven Liste – alle Daten bleiben erhalten. Du kannst die Archivierung jederzeit aufheben.")
                      : t("Blendet den Hund aus deiner Liste aus – Trainings, Fährten und Ziele bleiben vollständig erhalten. Ideal, wenn dein Hund verstorben ist oder pausiert.")}
                  </p>
                </div>
                <Button variant="outline" className="w-full" onClick={() => setArchived(!dog.archivedAt)}>
                  {dog.archivedAt ? <ArchiveRestore className="size-4" /> : <Archive className="size-4" />}
                  {dog.archivedAt ? t("Archivierung aufheben") : t("Archivieren")}
                </Button>
              </div>

              {/* Endgültig löschen: unwiderruflich, deshalb klar abgesetzt. */}
              <div className="flex flex-col gap-2 rounded-lg border border-destructive/30 p-3">
                <div className="min-w-0">
                  <p className="font-medium">{t("Hund endgültig löschen")}</p>
                  <p className="text-sm text-muted-foreground">
                    {t("Entfernt den Hund samt aller Trainings, Fährten, Ziele und Trainerzuweisungen unwiderruflich.")}
                  </p>
                </div>
                <Button variant="outline" className="w-full text-destructive hover:text-destructive" onClick={deleteDog}>
                  <Trash2 className="size-4" />
                  {t("Endgültig löschen")}
                </Button>
              </div>
            </>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}

"use client";

import { useRouter } from "next/navigation";
import { Trash2 } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

/**
 * "Gruppe auflösen" - bewusst abgesetzt ganz unten und nur über einen
 * Knopf mit Rückfrage: Die Mitgliedschaften verschwinden, Trainings und Hunde
 * bleiben. Danach gibt es die Gruppe nicht mehr, also zurück zur Übersicht.
 */
export function GroupDissolveSection({ groupId, groupName }: { groupId: string; groupName: string }) {
  const t = useT();
  const router = useRouter();

  async function aufloesen() {
    if (!window.confirm(t("Gruppe „{name}“ wirklich auflösen? Mitgliedschaften werden entfernt. Trainings und Hunde bleiben erhalten.", { name: groupName }))) return;
    try {
      await api.delete(`/api/groups/${groupId}`);
      toast.success(t("Gruppe aufgelöst."));
      router.replace("/trainer");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Gruppe konnte nicht gelöscht werden."));
    }
  }

  return (
    <Card className="border border-destructive/40">
      <CardContent className="flex flex-col gap-3">
        <div>
          <h2 className="text-base font-semibold text-destructive">{t("Gruppe auflösen")}</h2>
          <p className="text-sm text-muted-foreground">
            {t("Mitgliedschaften werden entfernt. Trainings und Hunde bleiben erhalten.")}
          </p>
        </div>
        <Button type="button" variant="outline" className="h-11 self-start text-destructive hover:text-destructive" onClick={aufloesen}>
          <Trash2 className="size-4" />
          {t("Gruppe auflösen")}
        </Button>
      </CardContent>
    </Card>
  );
}

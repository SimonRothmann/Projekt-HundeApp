"use client";

import { useParams } from "next/navigation";
import { Pencil, UserCog } from "lucide-react";
import { useT } from "@/lib/i18n";
import { useGroupDetail } from "@/lib/use-group-detail";
import { Card, CardContent } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { GroupDissolveSection } from "@/components/trainer/group-dissolve-section";
import { GroupEditForm } from "@/components/trainer/group-edit-form";
import { GroupTrainersSection } from "@/components/trainer/group-trainers-section";

/**
 * Verwaltung einer Gruppe: Name und Beschreibung, Trainer:innen, Auflösen.
 * Das stand früher als erster Block auf der Gruppenseite selbst - dabei braucht
 * man es selten, und das Alltägliche rutschte Bildschirme nach unten.
 * Für jede:n Trainer:in des Vereins erreichbar; das Backend prüft die Rechte.
 */
export default function TrainerGruppeEinstellungenPage() {
  const t = useT();
  const { groupId } = useParams<{ groupId: string }>();
  const { detail, laden } = useGroupDetail(groupId);

  if (detail === null) {
    return <p className="text-muted-foreground">{t("Lädt…")}</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("Einstellungen")}</h1>
        <p className="text-muted-foreground [overflow-wrap:anywhere]">{detail.group.name}</p>
      </div>

      <section className="flex flex-col gap-3">
        <SectionHeading icon={Pencil} title={t("Name & Beschreibung")} />
        <Card>
          <CardContent>
            {/* key: Nach dem Speichern oder Neuladen beginnt das Formular mit dem Stand des Servers. */}
            <GroupEditForm key={`${detail.group.name}|${detail.group.description ?? ""}`} group={detail.group} onGespeichert={laden} />
          </CardContent>
        </Card>
      </section>

      <section className="flex flex-col gap-3">
        <SectionHeading icon={UserCog} title={t("Trainer:innen")} />
        <Card>
          <CardContent>
            <GroupTrainersSection groupId={groupId} detail={detail} onGeaendert={laden} />
          </CardContent>
        </Card>
      </section>

      <div className="mt-4">
        <GroupDissolveSection groupId={groupId} groupName={detail.group.name} />
      </div>
    </div>
  );
}

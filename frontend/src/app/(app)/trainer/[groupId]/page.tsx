"use client";

import Link from "next/link";
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import { Settings } from "lucide-react";
import { useT } from "@/lib/i18n";
import { ANSICHT_PARAMETER, waehleAnsicht, type GruppenAnsicht } from "@/lib/gruppen-ansicht";
import { useGroupDetail } from "@/lib/use-group-detail";
import { useGroupRegistrations } from "@/lib/use-group-registrations";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { GroupMembersView } from "@/components/trainer/group-members-view";
import { GroupRegistrationsSection } from "@/components/trainer/group-registrations-section";
import { ansichtPanelId, ansichtTabId, GroupViewSwitch } from "@/components/trainer/group-view-switch";

/**
 * Eine Gruppe im Alltag: oben der Kopf, darunter entweder die Mitglieder oder
 * die Anmeldungen (Welpengruppe & Co.). Verwaltet wird auf einer eigenen Seite
 * (/trainer/{id}/einstellungen) - früher war der ganze erste Bildschirm am
 * Telefon Verwaltung, und das Häufigste (heute abhaken, Mitglieder ansehen)
 * lag Bildschirme weiter unten.
 *
 * Welche Ansicht offen ist, steht in der Adresse (?ansicht=): Zurück, Neu laden
 * und Links aus Benachrichtigungen führen dorthin, wo man war. Die Anmeldungen
 * gibt es nur für die, die die Gruppe verwalten - der Server entscheidet
 * (404 für alle anderen), dann entfällt der Umschalter.
 */
export default function TrainerGroupPage() {
  const t = useT();
  const { groupId } = useParams<{ groupId: string }>();
  const router = useRouter();
  const pathname = usePathname();
  const suchparameter = useSearchParams();
  const { detail, laden } = useGroupDetail(groupId);
  const anmeldungen = useGroupRegistrations(groupId);

  if (detail === null) {
    return <p className="text-muted-foreground">{t("Lädt…")}</p>;
  }

  const mitglieder = detail.members.length;
  const liste = anmeldungen.liste;
  const parameter = suchparameter.get(ANSICHT_PARAMETER);
  const ansicht = waehleAnsicht(parameter, { anmeldungen: liste?.length ?? null, mitglieder });
  // Auf die Anmeldungen warten nur, wenn sie die Ansicht sein könnten: ausdrücklich
  // verlangt, oder ohne Wahl bei einer Gruppe ohne Mitglieder. Sonst stehen die
  // Mitglieder sofort da - für Mitglieder ohne Trainerrechte endet die zweite
  // Anfrage ohnehin mit 404.
  const koennteAnmeldungenSein = parameter === "anmeldungen" || (parameter !== "mitglieder" && mitglieder === 0);

  function wechseln(neu: GruppenAnsicht) {
    const parameter = new URLSearchParams(suchparameter.toString());
    parameter.set(ANSICHT_PARAMETER, neu);
    router.replace(`${pathname}?${parameter.toString()}`, { scroll: false });
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight [overflow-wrap:anywhere]">{detail.group.name}</h1>
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
            {mitglieder === 1 ? t("1 Mitglied") : t("{n} Mitglieder", { n: mitglieder })}
            {detail.group.trainerName ? ` · ${t("Trainer:in: {name}", { name: detail.group.trainerName })}` : ""}
          </p>
          {detail.group.description && (
            <p className="mt-1 text-sm text-muted-foreground [overflow-wrap:anywhere]">{detail.group.description}</p>
          )}
        </div>
        {/* Für jede:n Trainer:in des Vereins sichtbar wie bisher die Bearbeiten-Knöpfe - das Backend prüft die Rechte. */}
        <Link
          href={`/trainer/${groupId}/einstellungen`}
          aria-label={t("Einstellungen")}
          title={t("Einstellungen")}
          className={cn(buttonVariants({ variant: "outline", size: "icon" }), "size-10 shrink-0")}
        >
          <Settings className="size-5" />
        </Link>
      </div>

      {liste !== null && (
        <GroupViewSwitch ansicht={ansicht} mitglieder={mitglieder} anmeldungen={liste.length} onChange={wechseln} />
      )}

      {anmeldungen.fehler && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm">
          <span className="min-w-0 text-muted-foreground">{t("Anmeldungen konnten nicht geladen werden.")}</span>
          <Button type="button" size="sm" variant="outline" onClick={() => void anmeldungen.laden()}>
            {t("Erneut versuchen")}
          </Button>
        </div>
      )}

      {anmeldungen.wirdGeladen && koennteAnmeldungenSein ? (
        <p className="text-muted-foreground">{t("Lädt…")}</p>
      ) : (
        <div
          role={liste !== null ? "tabpanel" : undefined}
          id={ansichtPanelId(ansicht)}
          aria-labelledby={liste !== null ? ansichtTabId(ansicht) : undefined}
          className="flex min-w-0 flex-col gap-4"
        >
          {ansicht === "anmeldungen" && liste !== null ? (
            <GroupRegistrationsSection
              groupId={groupId}
              groupName={detail.group.name}
              liste={liste}
              laden={anmeldungen.laden}
              ersetzen={anmeldungen.ersetzen}
              anzahlAendern={anmeldungen.anzahlAendern}
            />
          ) : (
            <GroupMembersView groupId={groupId} detail={detail} onGeaendert={laden} />
          )}
        </div>
      )}
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Club } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Card, CardContent } from "@/components/ui/card";
import { CatalogSection } from "@/components/sports/catalog-section";
import { ClubInviteSection } from "@/components/trainer/club-invite-section";
import { ClubMembersSection } from "@/components/trainer/club-members-section";

/**
 * Alles, was zum Verein gehört: Einladungslink mit QR-Code, Mitglieder und der
 * vereinseigene Katalog. Das lag früher als Block ganz unten auf der
 * Trainer-Übersicht, mit je einer Karte pro Verein untereinander.
 *
 * Bei mehreren Vereinen wählt man oben EINEN aus - die Seite zeigt dann nur
 * dessen Bereiche statt aller Vereine hintereinander.
 */
export default function TrainerVereinPage() {
  const t = useT();
  const [clubs, setClubs] = useState<Club[] | null>(null);
  const [gewaehlt, setGewaehlt] = useState("");

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    api
      .get<Club[]>("/api/groups/my-clubs")
      .then((liste) => {
        setClubs(liste);
        setGewaehlt(liste[0]?.id ?? "");
      })
      .catch((err) => {
        setClubs([]);
        toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden."));
      });
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (clubs === null) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  if (clubs.length === 0) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold tracking-tight">{t("Verein")}</h1>
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">
            {t("Du bist bei keinem Verein als Trainer:in eingetragen. Einladungslink, Mitglieder und Katalog findest du hier, sobald ein Verein dich als Trainer:in aufgenommen hat.")}
          </CardContent>
        </Card>
      </div>
    );
  }

  const club = clubs.find((c) => c.id === gewaehlt) ?? clubs[0];

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("Verein")}</h1>
        {clubs.length === 1 && <p className="text-muted-foreground [overflow-wrap:anywhere]">{club.name}</p>}
      </div>

      {clubs.length > 1 && (
        <div role="radiogroup" aria-label={t("Verein")} className="flex flex-wrap gap-2">
          {clubs.map((c) => (
            <button
              key={c.id}
              type="button"
              role="radio"
              aria-checked={c.id === club.id}
              onClick={() => setGewaehlt(c.id)}
              className={cn(
                "inline-flex min-h-9 max-w-full min-w-0 items-center rounded-full border px-3 py-1.5 text-sm font-medium transition-colors coarse:min-h-11",
                c.id === club.id
                  ? "border-primary bg-primary text-primary-foreground"
                  : "border-border bg-background hover:bg-muted",
              )}
            >
              <span className="truncate">{c.name}</span>
            </button>
          ))}
        </div>
      )}

      {/* key: Jeder Verein bekommt frische Abschnitte - die Mitgliederliste
          merkt sich sonst den zuerst gewählten Verein. */}
      <ClubInviteSection key={`einladung-${club.id}`} clubs={[club]} />
      <ClubMembersSection key={`mitglieder-${club.id}`} clubs={[club]} />
      <CatalogSection
        key={`katalog-${club.id}`}
        scope={{ kind: "club", clubId: club.id, clubName: club.name }}
        title={t("Vereinseigener Katalog · {name}", { name: club.name })}
        description={t("Eigene Sportarten und Übungen dieses Vereins - nur für Mitglieder und Trainer sichtbar.")}
      />
    </div>
  );
}

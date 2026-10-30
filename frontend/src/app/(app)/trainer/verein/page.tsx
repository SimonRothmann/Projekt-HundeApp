"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Club } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Card, CardContent } from "@/components/ui/card";
import { CatalogSection } from "@/components/sports/catalog-section";
import { ClubInviteSection } from "@/components/trainer/club-invite-section";
import { ClubMembersSection } from "@/components/trainer/club-members-section";
import { ClubRolesSection } from "@/components/trainer/club-roles-section";
import { ClubStammdatenSection } from "@/components/trainer/club-stammdaten-section";

/**
 * Alles, was zum Verein gehört - je nach Rolle: Stammdaten (Verwaltung),
 * Rollen im Verein (Ausbildungswart:in, Verwaltung), Einladungslink und
 * Mitglieder (Mitgliederverwaltung, Verwaltung) und der vereinseigene Katalog
 * (Trainer:innen). Das lag früher als Block ganz unten auf der
 * Trainer-Übersicht, mit je einer Karte pro Verein untereinander.
 *
 * Bei mehreren Vereinen wählt man oben EINEN aus - die Seite zeigt dann nur
 * dessen Bereiche statt aller Vereine hintereinander. Der Server entscheidet
 * über die Rechte und liefert sie mit den Vereinen mit; die Seite blendet nur
 * aus. Sie ist auch für Personen ohne Trainer-Rolle da (Mitgliederverwaltung):
 * Sie erreichen sie über die Vereinsseite, nicht über die Trainer-Navigation.
 */
export default function TrainerVereinPage() {
  const t = useT();
  const [clubs, setClubs] = useState<Club[] | null>(null);
  const [gewaehlt, setGewaehlt] = useState("");
  // Zählen hoch, wenn die jeweils andere Liste veraltet ist (Rolle vergeben -> Mitgliederliste; Mitglied befördert -> Rollenliste).
  const [mitgliederStand, setMitgliederStand] = useState(0);
  const [rollenStand, setRollenStand] = useState(0);

  const laden = useCallback(async () => {
    try {
      setClubs(await api.get<Club[]>("/api/clubs/mine"));
    } catch (err) {
      setClubs((alt) => alt ?? []);
      toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden."));
    }
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
    // Der Link von der Vereinsseite nennt den Verein (?verein=...); ein unbekannter fällt auf den ersten zurück.
    setGewaehlt(new URLSearchParams(window.location.search).get("verein") ?? "");
  }, [laden]);

  if (clubs === null) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  if (clubs.length === 0) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold tracking-tight">{t("Verein")}</h1>
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">
            {t("Du hast in keinem Verein eine Rolle. Einladungslink, Mitglieder und Katalog findest du hier, sobald ein Verein dir eine Rolle gegeben hat.")}
          </CardContent>
        </Card>
      </div>
    );
  }

  const club = clubs.find((c) => c.id === gewaehlt) ?? clubs[0];
  // Eine Rolle ohne eigene Funktion (Vorstand): die Seite sagt es, statt leer zu bleiben.
  const hatAbschnitte = club.canManage || club.canAppointTrainers || club.canManageMembers || club.isTrainer;

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
                  : "border-input bg-background hover:bg-muted",
              )}
            >
              <span className="truncate">{c.name}</span>
            </button>
          ))}
        </div>
      )}

      {!hatAbschnitte && (
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">
            {t("Mit deiner Rolle gibt es hier noch nichts zu tun.")}
          </CardContent>
        </Card>
      )}

      {/* key: Jeder Verein bekommt frische Abschnitte - die Mitgliederliste
          merkt sich sonst den zuerst gewählten Verein. */}
      {club.canManage && <ClubStammdatenSection key={`stammdaten-${club.id}`} club={club} onGespeichert={laden} />}
      {club.canAppointTrainers && (
        <ClubRolesSection
          key={`rollen-${club.id}-${rollenStand}`}
          clubId={club.id}
          canManage={club.canManage}
          canAppointTrainers={club.canAppointTrainers}
          mitgliederLesbar={club.canManageMembers}
          onGeaendert={async () => {
            setMitgliederStand((n) => n + 1);
            await laden();
          }}
        />
      )}
      {club.canManageMembers && <ClubInviteSection key={`einladung-${club.id}`} clubs={[club]} />}
      {club.canManageMembers && <ClubMembersSection
          key={`mitglieder-${club.id}-${mitgliederStand}`}
          clubs={[club]}
          onBefoerdert={() => setRollenStand((n) => n + 1)}
        />}
      {club.isTrainer && (
        <CatalogSection
          key={`katalog-${club.id}`}
          scope={{ kind: "club", clubId: club.id, clubName: club.name }}
          title={t("Vereinseigener Katalog · {name}", { name: club.name })}
          description={t("Eigene Sportarten und Übungen dieses Vereins - nur für Mitglieder und Trainer sichtbar.")}
        />
      )}
    </div>
  );
}

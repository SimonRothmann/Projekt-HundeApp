"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Club, Group } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";
import { ClubJoinRequestsSection } from "@/components/trainer/club-join-requests-section";
import { GroupJoinRequestsSection } from "@/components/trainer/group-join-requests-section";

/**
 * Offene Beitrittsanfragen an Gruppen und Vereine der Trainer:in. Die
 * Übersicht zeigt dazu nur noch den Zähler; die Benachrichtigung bei einer
 * neuen Vereinsanfrage führt hierher.
 *
 * Beide Abschnitte verstecken sich ohne Anfragen. Ob die Seite leer ist,
 * erfährt sie über deren Zahl (onAnzahl) - sie ändert sich, sobald die letzte
 * Anfrage entschieden ist.
 */
export default function TrainerAnfragenPage() {
  const t = useT();
  const [groups, setGroups] = useState<Group[] | null>(null);
  const [clubs, setClubs] = useState<Club[] | null>(null);
  // null = noch nicht bekannt; ohne Gruppen bzw. Vereine gibt es nichts zu laden.
  const [gruppenAnzahl, setGruppenAnzahl] = useState<number | null>(null);
  const [vereinsAnzahl, setVereinsAnzahl] = useState<number | null>(null);
  // Ein gescheiterter Abruf heißt nicht "keine Anfragen" - dann kein Leerzustand.
  const [ladefehler, setLadefehler] = useState(false);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    api
      .get<Group[]>("/api/groups")
      .then((liste) => {
        setGroups(liste);
        if (liste.length === 0) setGruppenAnzahl(0);
      })
      .catch((err) => {
        setGroups([]);
        setGruppenAnzahl(0);
        setLadefehler(true);
        toast.error(err instanceof ApiError ? err.message : t("Gruppen konnten nicht geladen werden."));
      });
    api
      .get<Club[]>("/api/groups/my-clubs")
      .then((liste) => {
        setClubs(liste);
        if (liste.length === 0) setVereinsAnzahl(0);
      })
      .catch((err) => {
        setClubs([]);
        setVereinsAnzahl(0);
        setLadefehler(true);
        toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden."));
      });
    // t bewusst nicht in der Liste: nur für Fehlermeldungen, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const geladen = groups !== null && clubs !== null && gruppenAnzahl !== null && vereinsAnzahl !== null;
  const leer = geladen && gruppenAnzahl + vereinsAnzahl === 0;

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Anfragen")}</h1>

      {groups !== null && groups.length > 0 && <GroupJoinRequestsSection groups={groups} onAnzahl={setGruppenAnzahl} onFehler={() => setLadefehler(true)} />}
      {clubs !== null && clubs.length > 0 && <ClubJoinRequestsSection clubs={clubs} onAnzahl={setVereinsAnzahl} onFehler={() => setLadefehler(true)} />}

      {!geladen && <p className="text-muted-foreground">{t("Lädt…")}</p>}
      {leer && ladefehler && (
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">{t("Anfragen konnten nicht geladen werden.")}</CardContent>
        </Card>
      )}
      {leer && !ladefehler && (
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">{t("Keine offenen Anfragen.")}</CardContent>
        </Card>
      )}
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { filtereKandidaten } from "@/lib/arbeitseinsaetze";
import { useT } from "@/lib/i18n";
import type { Arbeitseinsatz, ArbeitseinsatzKandidat } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Input } from "@/components/ui/input";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Helfer:innen abhaken (Verwaltung): Mitglieder und Rolleninhaber:innen des Vereins mit Suche und
 * Mehrfachauswahl. Wer schon abgehakt ist, steht nicht mehr zur Wahl. Die Personen bekommen keine
 * Nachricht - sie sehen den Einsatz auf ihrer Vereinsseite.
 */
export function HelferSheet({
  clubId,
  einsatz,
  open,
  onOpenChange,
  onAbgehakt,
}: {
  clubId: string;
  einsatz: Arbeitseinsatz;
  open: boolean;
  onOpenChange: (offen: boolean) => void;
  onAbgehakt: () => Promise<void>;
}) {
  const t = useT();
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{t("Helfer:innen hinzufügen")}</SheetTitle>
          <SheetDescription>{t("Wer war dabei? Die Personen bekommen keine Nachricht, sie sehen den Einsatz auf ihrer Vereinsseite.")}</SheetDescription>
        </SheetHeader>
        {open && <Formular clubId={clubId} einsatz={einsatz} onAbgehakt={onAbgehakt} />}
      </BottomSheetContent>
    </Sheet>
  );
}

function Formular({ clubId, einsatz, onAbgehakt }: { clubId: string; einsatz: Arbeitseinsatz; onAbgehakt: () => Promise<void> }) {
  const t = useT();
  const [kandidaten, setKandidaten] = useState<ArbeitseinsatzKandidat[] | null>(null);
  const [fehler, setFehler] = useState(false);
  const [suche, setSuche] = useState("");
  const [gewaehlt, setGewaehlt] = useState<string[]>([]);
  const [speichert, setSpeichert] = useState(false);

  useEffect(() => {
    let aktuell = true;
    api
      .get<ArbeitseinsatzKandidat[]>(`/api/clubs/${clubId}/work-events/candidates`)
      .then((liste) => {
        if (aktuell) setKandidaten(liste);
      })
      .catch(() => {
        if (aktuell) setFehler(true);
      });
    return () => {
      aktuell = false;
    };
  }, [clubId]);

  const schonDa = new Set(einsatz.participants.map((p) => p.userId));
  const frei = (kandidaten ?? []).filter((k) => !schonDa.has(k.userId));
  const treffer = filtereKandidaten(frei, suche);

  async function abhaken() {
    if (gewaehlt.length === 0) return;
    setSpeichert(true);
    try {
      await api.post(`/api/clubs/${clubId}/work-events/${einsatz.id}/participants`, { userIds: gewaehlt });
      toast.success(gewaehlt.length === 1 ? t("1 Person abgehakt.") : t("{n} Personen abgehakt.", { n: gewaehlt.length }));
      await onAbgehakt();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Personen konnten nicht abgehakt werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-3 p-4 pt-0">
      {fehler ? (
        <p className="text-sm text-destructive">{t("Die Mitglieder konnten nicht geladen werden.")}</p>
      ) : kandidaten === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : frei.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("Alle Mitglieder des Vereins sind schon abgehakt.")}</p>
      ) : (
        <>
          <Input
            type="search"
            value={suche}
            onChange={(e) => setSuche(e.target.value)}
            placeholder={t("Name suchen")}
            aria-label={t("Name suchen")}
          />
          {treffer.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("Keine Treffer.")}</p>
          ) : (
            <ul className="flex max-h-64 flex-col gap-1.5 overflow-y-auto">
              {treffer.map((k) => (
                <li key={k.userId}>
                  <Chip
                    gewaehlt={gewaehlt.includes(k.userId)}
                    className="w-full rounded-lg"
                    onClick={() => setGewaehlt((alt) => (alt.includes(k.userId) ? alt.filter((x) => x !== k.userId) : [...alt, k.userId]))}
                  >
                    {k.firstName} {k.lastName}
                  </Chip>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
      <Button disabled={gewaehlt.length === 0 || speichert} onClick={abhaken} className="coarse:min-h-11">
        {speichert ? t("Hakt ab…") : gewaehlt.length > 0 ? t("{n} abhaken", { n: gewaehlt.length }) : t("Abhaken")}
      </Button>
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { GroupRegistration } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { useRueckfrage } from "@/components/ui/rueckfrage";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

type Ziel = { id: string; name: string };

/**
 * Eine Anmeldung in eine andere Gruppe desselben Vereins übernehmen (Welpen ->
 * Junghunde). Der Server verschiebt sie: "bezahlt" und Anwesenheit der alten
 * Gruppe entfallen, die Notiz bleibt. Vor dem Verschieben fragt die Seite
 * nach, weil Anwesenheit und "bezahlt" nicht zurückkommen.
 */
export function RegistrationTransferSheet({
  groupId,
  registrierung,
  onOffenChange,
  onUebernommen,
}: {
  groupId: string;
  /** null = Sheet geschlossen. */
  registrierung: GroupRegistration | null;
  onOffenChange: (offen: boolean) => void;
  /** Die Anmeldung steht jetzt in der anderen Gruppe - die Liste hier muss sie entfernen. */
  onUebernommen: (id: string) => void;
}) {
  const t = useT();
  return (
    <Sheet open={registrierung !== null} onOpenChange={onOffenChange}>
      <BottomSheetContent>
        {registrierung && (
          <Inhalt
            key={registrierung.id}
            groupId={groupId}
            registrierung={registrierung}
            onFertig={() => {
              onOffenChange(false);
              onUebernommen(registrierung.id);
              toast.success(t("{name} wurde übernommen.", { name: registrierung.dogName }));
            }}
          />
        )}
      </BottomSheetContent>
    </Sheet>
  );
}

function Inhalt({
  groupId,
  registrierung,
  onFertig,
}: {
  groupId: string;
  registrierung: GroupRegistration;
  onFertig: () => void;
}) {
  const t = useT();
  const rueckfrage = useRueckfrage();
  const [ziele, setZiele] = useState<Ziel[] | null>(null);
  const [fehler, setFehler] = useState<string | null>(null);
  const [beschaeftigt, setBeschaeftigt] = useState(false);

  useEffect(() => {
    let abgebrochen = false;
    api
      .get<Ziel[]>(`/api/groups/${groupId}/registrations/transfer-targets`)
      .then((liste) => {
        if (!abgebrochen) setZiele(liste);
      })
      .catch((err) => {
        if (!abgebrochen) setFehler(err instanceof ApiError ? err.message : t("Die Gruppen konnten nicht geladen werden."));
      });
    return () => {
      abgebrochen = true;
    };
    // t nur für die Fehlermeldung.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  async function uebernehmen(ziel: Ziel) {
    const ja = await rueckfrage({
      titel: t("{name} in {gruppe} übernehmen?", { name: registrierung.dogName, gruppe: ziel.name }),
      text: t("„Bezahlt“ und die Anwesenheit in dieser Gruppe entfallen, die Notiz bleibt. Die Trainer:innen der anderen Gruppe sehen die Angaben, diese Gruppe nicht mehr."),
      bestaetigen: t("Jetzt übernehmen"),
    });
    if (!ja) return;
    setBeschaeftigt(true);
    setFehler(null);
    try {
      await api.post(`/api/groups/${groupId}/registrations/${registrierung.id}/transfer`, { targetGroupId: ziel.id });
      onFertig();
    } catch (err) {
      setFehler(err instanceof ApiError ? err.message : t("Die Anmeldung konnte nicht übernommen werden."));
      setBeschaeftigt(false);
    }
  }

  return (
    <>
      <SheetHeader>
        <SheetTitle>{t("In andere Gruppe übernehmen")}</SheetTitle>
        <SheetDescription className="[overflow-wrap:anywhere]">
          {t("Wohin soll {name} wechseln? Zur Wahl stehen die anderen Gruppen deines Vereins.", { name: registrierung.dogName })}
        </SheetDescription>
      </SheetHeader>
      <div className="flex min-w-0 flex-col gap-2 p-4 pt-0">
        {ziele === null && !fehler && <p className="py-2 text-sm text-muted-foreground">{t("Lädt…")}</p>}
        {ziele?.length === 0 && (
          <p className="py-2 text-sm text-muted-foreground">{t("Dein Verein hat keine andere Gruppe, in die sich die Anmeldung übernehmen ließe.")}</p>
        )}
        {ziele?.map((z) => (
          <Button
            key={z.id}
            type="button"
            variant="outline"
            className="h-auto min-h-11 w-full justify-start whitespace-normal py-2 text-left [overflow-wrap:anywhere]"
            disabled={beschaeftigt}
            onClick={() => void uebernehmen(z)}
          >
            {z.name}
          </Button>
        ))}
        {fehler && (
          <p role="alert" className="text-sm text-destructive [overflow-wrap:anywhere]">
            {fehler}
          </p>
        )}
      </div>
    </>
  );
}

"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { formatDogAge } from "@/lib/dog-age";
import { useT } from "@/lib/i18n";
import { filtereKandidaten, pruefeGast } from "@/lib/pruefungen";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { PruefungKandidat, PruefungOrdnung, VereinsPruefung } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SegmentUmschalter } from "@/components/ui/segment-umschalter";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { OrdnungsWahl, useOrdnungen } from "@/components/pruefungen/ordnungen";

type Modus = "mitglied" | "gast";

/**
 * Ein Team auf die Meldeliste setzen. Die Prüfungsleitung trägt ALLE Teams selbst ein - die
 * Mitglieder melden nichts in der App:
 *
 * - Mitglied: ein Vereinsmitglied und einer seiner Hunde. Die Familie bekommt dafür eine Nachricht.
 * - Gast: ohne Dogity-Bezug, mit Name und Hund (Verein und Rasse optional). Die Angaben stehen nur
 *   in dieser Prüfung und gehen mit ihr.
 */
export function TeamEintragenSheet({
  clubId,
  pruefung,
  open,
  onOpenChange,
  onEingetragen,
}: {
  clubId: string;
  pruefung: VereinsPruefung;
  open: boolean;
  onOpenChange: (offen: boolean) => void;
  onEingetragen: () => Promise<void>;
}) {
  const t = useT();
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{t("Team eintragen")}</SheetTitle>
          <SheetDescription>{t("Ein Mitglied mit Hund oder ein Gast.")}</SheetDescription>
        </SheetHeader>
        {open && <Formular clubId={clubId} pruefung={pruefung} onEingetragen={onEingetragen} />}
      </BottomSheetContent>
    </Sheet>
  );
}

function Formular({ clubId, pruefung, onEingetragen }: { clubId: string; pruefung: VereinsPruefung; onEingetragen: () => Promise<void> }) {
  const t = useT();
  const [modus, setModus] = useState<Modus>("mitglied");
  const [kandidaten, setKandidaten] = useState<PruefungKandidat[] | null>(null);
  const [kandidatenFehler, setKandidatenFehler] = useState(false);
  const [suche, setSuche] = useState("");
  const [mitglied, setMitglied] = useState<PruefungKandidat | null>(null);
  const [hundId, setHundId] = useState("");
  const [ordnungWahl, setOrdnungWahl] = useState("");
  const [fuehrer, setFuehrer] = useState("");
  const [gastHund, setGastHund] = useState("");
  const [info, setInfo] = useState("");
  const [speichert, setSpeichert] = useState(false);

  // Angeboten sind die Ordnungen des Tages; ohne Vorgabe alle, aus denen auch ein Ziel gewählt wird.
  const vorgabe = pruefung.offeredRegulations;
  const { ordnungen: alleOrdnungen, fehler: ordnungenFehler } = useOrdnungen(vorgabe.length === 0);
  const auswahl: PruefungOrdnung[] | null = vorgabe.length > 0 ? vorgabe : alleOrdnungen;

  useEffect(() => {
    let aktuell = true;
    api
      .get<PruefungKandidat[]>(`/api/clubs/${clubId}/exams/candidates`)
      .then((liste) => {
        if (aktuell) setKandidaten(liste);
      })
      .catch(() => {
        if (aktuell) setKandidatenFehler(true);
      });
    return () => {
      aktuell = false;
    };
  }, [clubId]);

  // Gibt es nur eine Ordnung zur Wahl, gibt es nichts zu wählen.
  const ordnungId = ordnungWahl || (auswahl?.length === 1 ? auswahl[0].id : "");

  const gastFehler = pruefeGast(t, { fuehrer, hund: gastHund });
  const bereit = ordnungId !== "" && (modus === "mitglied" ? mitglied !== null && hundId !== "" : gastFehler === null);
  const treffer = filtereKandidaten(kandidaten ?? [], suche);

  async function eintragen() {
    if (!bereit) return;
    setSpeichert(true);
    try {
      await api.post(`/api/clubs/${clubId}/exams/${pruefung.id}/entries`, {
        regulationId: ordnungId,
        memberUserId: modus === "mitglied" ? mitglied!.userId : null,
        dogId: modus === "mitglied" ? hundId : null,
        guestHandlerName: modus === "gast" ? fuehrer.trim() : null,
        guestDogName: modus === "gast" ? gastHund.trim() : null,
        guestInfo: modus === "gast" ? info.trim() || null : null,
      });
      toast.success(t("Team eingetragen."));
      await onEingetragen();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Team konnte nicht eingetragen werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      <SegmentUmschalter
        ariaLabel={t("Art des Teams")}
        wert={modus}
        segmente={[
          ["mitglied", t("Mitglied")],
          ["gast", t("Gast")],
        ]}
        tabId={(wert) => `team-art-${wert}`}
        onChange={setModus}
      />

      {modus === "mitglied" ? (
        <div role="tabpanel" aria-labelledby="team-art-mitglied" className="flex min-w-0 flex-col gap-3">
          {kandidatenFehler ? (
            <p className="text-sm text-destructive">{t("Die Mitglieder konnten nicht geladen werden.")}</p>
          ) : kandidaten === null ? (
            <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
          ) : kandidaten.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("Kein Mitglied des Vereins hat einen Hund in Dogity.")}</p>
          ) : (
            <>
              <Input
                type="search"
                value={suche}
                onChange={(e) => setSuche(e.target.value)}
                placeholder={t("Name oder Hund suchen")}
                aria-label={t("Name oder Hund suchen")}
              />
              {treffer.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t("Keine Treffer.")}</p>
              ) : (
                <ul className="flex max-h-56 flex-col gap-1.5 overflow-y-auto">
                  {treffer.map((k) => (
                    <li key={k.userId}>
                      <Chip
                        gewaehlt={mitglied?.userId === k.userId}
                        className="w-full rounded-lg"
                        onClick={() => {
                          setMitglied(k);
                          // Ein einziger Hund ist schon die Wahl.
                          setHundId(k.dogs.length === 1 ? k.dogs[0].id : "");
                        }}
                      >
                        <span className="min-w-0">
                          {k.firstName} {k.lastName}
                          <span className="block text-xs font-normal text-muted-foreground">{k.dogs.map((d) => d.name).join(", ")}</span>
                        </span>
                      </Chip>
                    </li>
                  ))}
                </ul>
              )}
              {mitglied && mitglied.dogs.length > 1 && (
                <div className="flex min-w-0 flex-col gap-2">
                  <Label>{t("Hund")}</Label>
                  <div className="flex flex-wrap gap-2">
                    {mitglied.dogs.map((d) => (
                      <Chip key={d.id} gewaehlt={hundId === d.id} onClick={() => setHundId(d.id)}>
                        <span className="min-w-0">
                          {d.name}
                          {(d.breed || d.birthday) && (
                            <span className="block text-xs font-normal text-muted-foreground">
                              {[d.breed, formatDogAge(d.birthday, new Date(), t)].filter(Boolean).join(" · ")}
                            </span>
                          )}
                        </span>
                      </Chip>
                    ))}
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      ) : (
        <div role="tabpanel" aria-labelledby="team-art-gast" className="flex min-w-0 flex-col gap-3">
          <div className="flex min-w-0 flex-col gap-2">
            <Label htmlFor="gast-fuehrer">{t("Hundeführer:in")}</Label>
            <Input id="gast-fuehrer" value={fuehrer} maxLength={TEXTLAENGE.gastName} onChange={(e) => setFuehrer(e.target.value)} />
          </div>
          <div className="flex min-w-0 flex-col gap-2">
            <Label htmlFor="gast-hund">{t("Hund")}</Label>
            <Input id="gast-hund" value={gastHund} maxLength={TEXTLAENGE.gastName} onChange={(e) => setGastHund(e.target.value)} />
          </div>
          <div className="flex min-w-0 flex-col gap-2">
            <Label htmlFor="gast-info">{t("Verein und Rasse")}</Label>
            <Input
              id="gast-info"
              value={info}
              maxLength={TEXTLAENGE.gastInfo}
              placeholder={t("(optional)")}
              onChange={(e) => setInfo(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              {t("Gäste haben kein Dogity-Konto. Ihre Angaben stehen nur in dieser Prüfung und werden mit ihr gelöscht.")}
            </p>
          </div>
        </div>
      )}

      <div className="flex min-w-0 flex-col gap-2">
        <Label>{t("Ordnung")}</Label>
        {ordnungenFehler && vorgabe.length === 0 ? (
          <p className="text-sm text-destructive">{t("Die Prüfungsordnungen konnten nicht geladen werden.")}</p>
        ) : auswahl === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <OrdnungsWahl ordnungen={auswahl} gewaehlt={ordnungId ? [ordnungId] : []} onToggle={setOrdnungWahl} gruppiert={vorgabe.length === 0} />
        )}
      </div>

      {modus === "mitglied" && (
        <p className="text-xs text-muted-foreground">
          {t("Die Familie bekommt eine Nachricht, dass ihr Hund gemeldet ist. Die Prüfungsleitung sieht dafür die Prüfungsreife.")}
        </p>
      )}
      <Button disabled={!bereit || speichert} onClick={eintragen} className="coarse:min-h-11">
        {speichert ? t("Trägt ein…") : t("Eintragen")}
      </Button>
    </div>
  );
}

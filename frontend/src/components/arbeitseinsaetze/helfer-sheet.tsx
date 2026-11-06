"use client";

import { useEffect, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { filtereKandidaten, gleicheGastPerson, helferName } from "@/lib/arbeitseinsaetze";
import { useT } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { Arbeitseinsatz, ArbeitseinsatzGast, ArbeitseinsatzKandidat } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Helfer:innen abhaken (Verwaltung): Mitglieder und Rolleninhaber:innen des Vereins mit Suche und
 * Mehrfachauswahl, darunter die Personen ohne Dogity-Konto - und "Person ohne Konto hinzufügen", das
 * eine neue anlegt und gleich auswählt. Heißt sie wie eine vorhandene, fragt das Fenster erst nach.
 * Wer schon abgehakt ist, steht nicht mehr zur Wahl. Die Personen bekommen keine Nachricht - Mitglieder
 * sehen den Einsatz auf ihrer Vereinsseite.
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
          <SheetDescription>{t("Wer war dabei? Niemand bekommt eine Nachricht; Mitglieder sehen den Einsatz auf ihrer Vereinsseite.")}</SheetDescription>
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
  const [gaeste, setGaeste] = useState<ArbeitseinsatzGast[]>([]);
  const [gewaehlt, setGewaehlt] = useState<string[]>([]);
  const [gewaehlteGaeste, setGewaehlteGaeste] = useState<string[]>([]);
  const [speichert, setSpeichert] = useState(false);
  const [neuOffen, setNeuOffen] = useState(false);
  const [vorname, setVorname] = useState("");
  const [nachname, setNachname] = useState("");
  // Die vorhandene Person, die so heißt wie die eingegebene - solange gesetzt, fragt das Formular nach.
  const [gibtsSchon, setGibtsSchon] = useState<ArbeitseinsatzGast | null>(null);

  useEffect(() => {
    let aktuell = true;
    Promise.all([
      api.get<ArbeitseinsatzKandidat[]>(`/api/clubs/${clubId}/work-events/candidates`),
      api.get<ArbeitseinsatzGast[]>(`/api/clubs/${clubId}/work-events/guests`),
    ])
      .then(([liste, ohneKonto]) => {
        if (!aktuell) return;
        setKandidaten(liste);
        setGaeste(ohneKonto);
      })
      .catch(() => {
        if (aktuell) setFehler(true);
      });
    return () => {
      aktuell = false;
    };
  }, [clubId]);

  const schonDa = new Set(einsatz.participants.map((p) => p.userId));
  const gastSchonDa = new Set(einsatz.participants.map((p) => p.guestId));
  const frei = (kandidaten ?? []).filter((k) => !schonDa.has(k.userId));
  const treffer = filtereKandidaten(frei, suche);
  const freieGaeste = gaeste.filter((g) => !gastSchonDa.has(g.id));
  const gaesteTreffer = filtereKandidaten(freieGaeste, suche);
  const anzahl = gewaehlt.length + gewaehlteGaeste.length;

  function umschalten(liste: string[], setze: (neu: string[]) => void, id: string) {
    setze(liste.includes(id) ? liste.filter((x) => x !== id) : [...liste, id]);
  }

  function neuSchliessen() {
    setNeuOffen(false);
    setVorname("");
    setNachname("");
    setGibtsSchon(null);
  }

  function vorhandeneWaehlen(gast: ArbeitseinsatzGast) {
    if (gastSchonDa.has(gast.id)) toast.info(t("{name} ist schon abgehakt.", { name: helferName(t, gast) }));
    else setGewaehlteGaeste((alt) => (alt.includes(gast.id) ? alt : [...alt, gast.id]));
    // Suche leeren, sonst bliebe die gewählte Person unsichtbar, zählte aber im Knopf mit.
    setSuche("");
    neuSchliessen();
  }

  async function anlegen(e: FormEvent, trotzdem = false) {
    e.preventDefault();
    if (!vorname.trim()) return;
    const doppelt = gleicheGastPerson(gaeste, vorname, nachname);
    if (doppelt && !trotzdem) {
      setGibtsSchon(doppelt);
      return;
    }
    setSpeichert(true);
    try {
      const neu = await api.post<ArbeitseinsatzGast>(`/api/clubs/${clubId}/work-events/guests`, {
        firstName: vorname.trim(),
        lastName: nachname.trim() || null,
      });
      setGaeste((alt) => [...alt, neu]);
      setGewaehlteGaeste((alt) => [...alt, neu.id]);
      setSuche("");
      neuSchliessen();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Person konnte nicht angelegt werden."));
    } finally {
      setSpeichert(false);
    }
  }

  async function abhaken() {
    if (anzahl === 0) return;
    setSpeichert(true);
    try {
      await api.post(`/api/clubs/${clubId}/work-events/${einsatz.id}/participants`, { userIds: gewaehlt, guestIds: gewaehlteGaeste });
      toast.success(anzahl === 1 ? t("1 Person abgehakt.") : t("{n} Personen abgehakt.", { n: anzahl }));
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
      ) : (
        <>
          {(frei.length > 0 || freieGaeste.length > 0) && (
            <Input
              type="search"
              value={suche}
              onChange={(e) => setSuche(e.target.value)}
              placeholder={t("Name suchen")}
              aria-label={t("Name suchen")}
            />
          )}
          {frei.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("Alle Mitglieder des Vereins sind schon abgehakt.")}</p>
          ) : treffer.length === 0 && gaesteTreffer.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("Keine Treffer.")}</p>
          ) : (
            treffer.length > 0 && (
              <ul className="flex max-h-64 flex-col gap-1.5 overflow-y-auto">
                {treffer.map((k) => (
                  <li key={k.userId}>
                    <Chip gewaehlt={gewaehlt.includes(k.userId)} className="w-full rounded-lg" onClick={() => umschalten(gewaehlt, setGewaehlt, k.userId)}>
                      {k.firstName} {k.lastName}
                    </Chip>
                  </li>
                ))}
              </ul>
            )
          )}

          <h3 className="text-sm font-medium">{t("Ohne Dogity-Konto")}</h3>
          {gaesteTreffer.length > 0 ? (
            <ul className="flex max-h-48 flex-col gap-1.5 overflow-y-auto">
              {gaesteTreffer.map((g) => (
                <li key={g.id}>
                  <Chip gewaehlt={gewaehlteGaeste.includes(g.id)} className="w-full rounded-lg" onClick={() => umschalten(gewaehlteGaeste, setGewaehlteGaeste, g.id)}>
                    <span className="min-w-0 break-words">{helferName(t, g)}</span>
                    <span className="ml-2 shrink-0 text-xs text-muted-foreground">{t("ohne Konto")}</span>
                  </Chip>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-muted-foreground">
              {freieGaeste.length === 0
                ? t("Noch niemand ohne Konto angelegt.")
                : // Hat schon der Mitgliederblock "Keine Treffer." gezeigt, nicht noch einmal.
                  treffer.length === 0 && frei.length > 0
                  ? null
                  : t("Keine Treffer.")}
            </p>
          )}

          {neuOffen ? (
            <form onSubmit={(e) => void anlegen(e)} className="flex min-w-0 flex-col gap-3 rounded-lg border p-3">
              <div className="flex min-w-0 flex-col gap-2">
                <Label htmlFor="gast-vorname">{t("Vorname")}</Label>
                <Input
                  id="gast-vorname"
                  value={vorname}
                  maxLength={TEXTLAENGE.arbeitseinsatzGastName}
                  onChange={(e) => {
                    setVorname(e.target.value);
                    setGibtsSchon(null);
                  }}
                />
              </div>
              <div className="flex min-w-0 flex-col gap-2">
                <Label htmlFor="gast-nachname">{t("Nachname (optional)")}</Label>
                <Input
                  id="gast-nachname"
                  value={nachname}
                  maxLength={TEXTLAENGE.arbeitseinsatzGastName}
                  onChange={(e) => {
                    setNachname(e.target.value);
                    setGibtsSchon(null);
                  }}
                />
              </div>
              {gibtsSchon ? (
                <div className="flex min-w-0 flex-col gap-2">
                  <p className="text-sm [overflow-wrap:anywhere]">{t("Gibt es schon: {name}.", { name: helferName(t, gibtsSchon) })}</p>
                  <Button type="button" onClick={() => vorhandeneWaehlen(gibtsSchon)} className="coarse:min-h-11">
                    {t("Vorhandene auswählen")}
                  </Button>
                  <Button type="button" variant="outline" disabled={speichert} onClick={(e) => void anlegen(e, true)} className="coarse:min-h-11">
                    {t("Trotzdem neu anlegen")}
                  </Button>
                </div>
              ) : (
                <div className="flex flex-wrap gap-2">
                  <Button type="submit" disabled={speichert || !vorname.trim()} className="coarse:min-h-11">
                    {speichert ? t("Speichert…") : t("Anlegen und auswählen")}
                  </Button>
                  <Button type="button" variant="ghost" onClick={neuSchliessen} className="coarse:min-h-11">
                    {t("Abbrechen")}
                  </Button>
                </div>
              )}
            </form>
          ) : (
            <Button type="button" variant="outline" onClick={() => setNeuOffen(true)} className="coarse:min-h-11">
              {t("Person ohne Konto hinzufügen")}
            </Button>
          )}
        </>
      )}
      <Button disabled={anzahl === 0 || speichert} onClick={abhaken} className="coarse:min-h-11">
        {speichert ? t("Hakt ab…") : anzahl > 0 ? t("{n} abhaken", { n: anzahl }) : t("Abhaken")}
      </Button>
    </div>
  );
}

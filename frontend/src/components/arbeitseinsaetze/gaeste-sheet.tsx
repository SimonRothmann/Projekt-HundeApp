"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Pencil, Trash2, UserCheck } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { filtereKandidaten, helferName } from "@/lib/arbeitseinsaetze";
import { useT } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { ArbeitseinsatzGast, ArbeitseinsatzKandidat } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

type Aktion = { art: "umbenennen" | "zuordnen"; gast: ArbeitseinsatzGast };

/**
 * Personen ohne Dogity-Konto verwalten (Verwaltung): umbenennen, löschen (samt allen ihren Einsätzen, nach
 * Rückfrage) und - wenn die Person später Dogity nutzt - einem Mitglied zuordnen. Dann wandern ihre
 * Einsätze auf das Mitglied, und die Person ohne Konto verschwindet.
 */
export function GaesteSheet({
  clubId,
  open,
  onOpenChange,
  onGeaendert,
}: {
  clubId: string;
  open: boolean;
  onOpenChange: (offen: boolean) => void;
  /** Etwas hat sich geändert (Name, Zuordnung, Löschung): Die Seite lädt den Einsatz neu. */
  onGeaendert: () => Promise<void>;
}) {
  const t = useT();
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{t("Personen ohne Konto")}</SheetTitle>
          <SheetDescription>
            {t("Wer kein Dogity nutzt, aber hilft. Nutzt die Person später Dogity, ordne sie einem Mitglied zu – ihre Einsätze gehen dann auf das Mitglied über.")}
          </SheetDescription>
        </SheetHeader>
        {open && <Inhalt clubId={clubId} onGeaendert={onGeaendert} />}
      </BottomSheetContent>
    </Sheet>
  );
}

function Inhalt({ clubId, onGeaendert }: { clubId: string; onGeaendert: () => Promise<void> }) {
  const t = useT();
  const basis = `/api/clubs/${clubId}/work-events/guests`;
  const [gaeste, setGaeste] = useState<ArbeitseinsatzGast[] | null>(null);
  const [fehler, setFehler] = useState(false);
  const [aktion, setAktion] = useState<Aktion | null>(null);
  const [laeuft, setLaeuft] = useState(false);

  useEffect(() => {
    let aktuell = true;
    api
      .get<ArbeitseinsatzGast[]>(basis)
      .then((liste) => {
        if (aktuell) setGaeste(liste);
      })
      .catch(() => {
        if (aktuell) setFehler(true);
      });
    return () => {
      aktuell = false;
    };
  }, [basis]);

  async function neuLaden() {
    setGaeste(await api.get<ArbeitseinsatzGast[]>(basis));
    await onGeaendert();
  }

  async function loeschen(gast: ArbeitseinsatzGast) {
    if (!window.confirm(t("{name} samt allen Einsätzen endgültig löschen?", { name: helferName(t, gast) }))) return;
    setLaeuft(true);
    try {
      await api.delete(`${basis}/${gast.id}`);
      toast.success(t("Person gelöscht."));
      await neuLaden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Person konnte nicht gelöscht werden."));
    } finally {
      setLaeuft(false);
    }
  }

  if (fehler) return <p className="p-4 pt-0 text-sm text-destructive">{t("Die Personen konnten nicht geladen werden.")}</p>;
  if (gaeste === null) return <p className="p-4 pt-0 text-sm text-muted-foreground">{t("Lädt…")}</p>;

  if (aktion?.art === "umbenennen") {
    return (
      <Umbenennen
        key={aktion.gast.id}
        url={`${basis}/${aktion.gast.id}`}
        gast={aktion.gast}
        onFertig={async (geaendert) => {
          setAktion(null);
          if (geaendert) await neuLaden();
        }}
      />
    );
  }
  if (aktion?.art === "zuordnen") {
    return (
      <Zuordnen
        key={aktion.gast.id}
        clubId={clubId}
        url={`${basis}/${aktion.gast.id}/assign`}
        gast={aktion.gast}
        onFertig={async (geaendert) => {
          setAktion(null);
          if (geaendert) await neuLaden();
        }}
      />
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-3 p-4 pt-0">
      {gaeste.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("Noch niemand ohne Konto angelegt.")}</p>
      ) : (
        <ul className="flex max-h-80 flex-col divide-y overflow-y-auto">
          {gaeste.map((g) => (
            <li key={g.id} className="flex min-w-0 items-center gap-2 py-2">
              <span className="min-w-0 flex-1 font-medium [overflow-wrap:anywhere]">{helferName(t, g)}</span>
              <div className="flex shrink-0 gap-1">
                <Button variant="ghost" size="icon" aria-label={t("Ist jetzt in Dogity")} disabled={laeuft} onClick={() => setAktion({ art: "zuordnen", gast: g })} className="coarse:size-11">
                  <UserCheck className="size-4" />
                </Button>
                <Button variant="ghost" size="icon" aria-label={t("Umbenennen")} disabled={laeuft} onClick={() => setAktion({ art: "umbenennen", gast: g })} className="coarse:size-11">
                  <Pencil className="size-4" />
                </Button>
                <Button variant="ghost" size="icon" aria-label={t("Löschen")} disabled={laeuft} onClick={() => void loeschen(g)} className="coarse:size-11">
                  <Trash2 className="size-4" />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Umbenennen({ url, gast, onFertig }: { url: string; gast: ArbeitseinsatzGast; onFertig: (geaendert: boolean) => Promise<void> }) {
  const t = useT();
  const [vorname, setVorname] = useState(gast.firstName);
  const [nachname, setNachname] = useState(gast.lastName);
  const [speichert, setSpeichert] = useState(false);

  async function speichern(e: FormEvent) {
    e.preventDefault();
    if (!vorname.trim()) return;
    setSpeichert(true);
    try {
      await api.put(url, { firstName: vorname.trim(), lastName: nachname.trim() || null });
      toast.success(t("Gespeichert."));
      await onFertig(true);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Person konnte nicht gespeichert werden."));
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={speichern} className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="gast-neu-vorname">{t("Vorname")}</Label>
        <Input id="gast-neu-vorname" value={vorname} maxLength={TEXTLAENGE.arbeitseinsatzGastName} onChange={(e) => setVorname(e.target.value)} />
      </div>
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="gast-neu-nachname">{t("Nachname (optional)")}</Label>
        <Input id="gast-neu-nachname" value={nachname} maxLength={TEXTLAENGE.arbeitseinsatzGastName} onChange={(e) => setNachname(e.target.value)} />
      </div>
      <div className="flex flex-wrap gap-2">
        <Button type="submit" disabled={speichert || !vorname.trim()} className="coarse:min-h-11">
          {speichert ? t("Speichert…") : t("Speichern")}
        </Button>
        <Button type="button" variant="ghost" disabled={speichert} onClick={() => void onFertig(false)} className="coarse:min-h-11">
          {t("Abbrechen")}
        </Button>
      </div>
    </form>
  );
}

function Zuordnen({ clubId, url, gast, onFertig }: { clubId: string; url: string; gast: ArbeitseinsatzGast; onFertig: (geaendert: boolean) => Promise<void> }) {
  const t = useT();
  const [kandidaten, setKandidaten] = useState<ArbeitseinsatzKandidat[] | null>(null);
  const [fehler, setFehler] = useState(false);
  const [suche, setSuche] = useState("");
  const [gewaehlt, setGewaehlt] = useState<ArbeitseinsatzKandidat | null>(null);
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

  async function zuordnen() {
    if (!gewaehlt) return;
    const frage = t("Alle Einsätze von {gast} gehen auf {mitglied} über, die Person ohne Konto wird danach gelöscht. Fortfahren?", {
      gast: helferName(t, gast),
      mitglied: helferName(t, gewaehlt),
    });
    if (!window.confirm(frage)) return;
    setSpeichert(true);
    try {
      await api.post(url, { userId: gewaehlt.userId });
      toast.success(t("Zugeordnet."));
      await onFertig(true);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Person konnte nicht zugeordnet werden."));
      setSpeichert(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-3 p-4 pt-0">
      <p className="text-sm [overflow-wrap:anywhere]">{t("Welches Mitglied ist {name}?", { name: helferName(t, gast) })}</p>
      {fehler ? (
        <p className="text-sm text-destructive">{t("Die Mitglieder konnten nicht geladen werden.")}</p>
      ) : kandidaten === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : (
        <>
          <Input type="search" value={suche} onChange={(e) => setSuche(e.target.value)} placeholder={t("Name suchen")} aria-label={t("Name suchen")} />
          <ul className="flex max-h-64 flex-col gap-1.5 overflow-y-auto">
            {filtereKandidaten(kandidaten, suche).map((k) => (
              <li key={k.userId}>
                <Chip gewaehlt={gewaehlt?.userId === k.userId} className="w-full rounded-lg" onClick={() => setGewaehlt(k)}>
                  {k.firstName} {k.lastName}
                </Chip>
              </li>
            ))}
          </ul>
        </>
      )}
      <div className="flex flex-wrap gap-2">
        <Button disabled={!gewaehlt || speichert} onClick={() => void zuordnen()} className="coarse:min-h-11">
          {speichert ? t("Speichert…") : t("Zuordnen")}
        </Button>
        <Button variant="ghost" disabled={speichert} onClick={() => void onFertig(false)} className="coarse:min-h-11">
          {t("Abbrechen")}
        </Button>
      </div>
    </div>
  );
}

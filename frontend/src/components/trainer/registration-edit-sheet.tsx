"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { jahreZurueck, MAX_HUNDEALTER_JAHRE, pruefeAnmeldung, tagDerAnmeldung } from "@/lib/anmeldung";
import { anmeldeFehlerSatz } from "@/lib/anmeldung-texte";
import { useT } from "@/lib/i18n";
import { heuteIso } from "@/lib/pruefung";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { GroupRegistration } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

type Eingabe = {
  vorname: string;
  nachname: string;
  rufname: string;
  rasse: string;
  wurftag: string;
  telefon: string;
  notiz: string;
};

function ausAnmeldung(r: GroupRegistration | null): Eingabe {
  return {
    vorname: r?.firstName ?? "",
    nachname: r?.lastName ?? "",
    rufname: r?.dogName ?? "",
    rasse: r?.dogBreed ?? "",
    wurftag: r?.dogBirthDate.slice(0, 10) ?? "",
    telefon: r?.phone ?? "",
    notiz: r?.notes ?? "",
  };
}

/**
 * Eine Anmeldung von Hand eintragen (r = null) oder bearbeiten - mit denselben
 * Feldern und Prüfungen wie das Formular, dazu die Notiz, die nur
 * Trainer:innen sehen.
 */
export function RegistrationEditSheet({
  groupId,
  registrierung,
  offen,
  onOffenChange,
  onGespeichert,
}: {
  groupId: string;
  /** null = neu anlegen. */
  registrierung: GroupRegistration | null;
  offen: boolean;
  onOffenChange: (offen: boolean) => void;
  onGespeichert: () => Promise<void> | void;
}) {
  const t = useT();
  return (
    <Sheet open={offen} onOpenChange={onOffenChange}>
      <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto">
        {/* Der Inhalt hängt am Schlüssel: Jedes Öffnen beginnt mit frischem Zustand. */}
        <Formular
          key={registrierung?.id ?? "neu"}
          groupId={groupId}
          registrierung={registrierung}
          onFertig={async () => {
            onOffenChange(false);
            await onGespeichert();
          }}
          titel={registrierung ? t("Anmeldung bearbeiten") : t("Anmeldung hinzufügen")}
        />
      </SheetContent>
    </Sheet>
  );
}

function Formular({
  groupId,
  registrierung,
  onFertig,
  titel,
}: {
  groupId: string;
  registrierung: GroupRegistration | null;
  onFertig: () => Promise<void>;
  titel: string;
}) {
  const t = useT();
  const [eingabe, setEingabe] = useState<Eingabe>(() => ausAnmeldung(registrierung));
  const [speichert, setSpeichert] = useState(false);
  const [fehler, setFehler] = useState<string | null>(null);

  const heute = heuteIso();
  // Beim Bearbeiten gegen den Tag der Anmeldung gemessen - wie auf dem Server.
  const referenz = registrierung ? tagDerAnmeldung(registrierung.registeredAt) : heute;
  const patch = (teil: Partial<Eingabe>) => setEingabe((e) => ({ ...e, ...teil }));

  async function speichern() {
    const problem = pruefeAnmeldung(eingabe, heute, referenz);
    setFehler(problem ? anmeldeFehlerSatz(t, problem) : null);
    if (problem) return;

    setSpeichert(true);
    try {
      const body = {
        firstName: eingabe.vorname.trim(),
        lastName: eingabe.nachname.trim(),
        dogName: eingabe.rufname.trim(),
        dogBreed: eingabe.rasse.trim(),
        dogBirthDate: eingabe.wurftag,
        phone: eingabe.telefon.trim(),
        notes: eingabe.notiz.trim() || null,
      };
      if (registrierung) await api.put(`/api/groups/${groupId}/registrations/${registrierung.id}`, body);
      else await api.post(`/api/groups/${groupId}/registrations`, body);
      toast.success(t("Anmeldung gespeichert."));
      await onFertig();
    } catch (err) {
      setFehler(err instanceof ApiError ? err.message : t("Anmeldung konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <>
      <SheetHeader>
        <SheetTitle>{titel}</SheetTitle>
        <SheetDescription>{t("Angaben der Kursteilnehmer:in. Sie braucht dafür kein Dogity-Konto.")}</SheetDescription>
      </SheetHeader>
      <form
        className="flex min-w-0 flex-col gap-4 p-4 pt-0"
        onSubmit={(e) => {
          e.preventDefault();
          void speichern();
        }}
        noValidate
      >
        <div className="grid min-w-0 grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="flex min-w-0 flex-col gap-1.5">
            <Label htmlFor="reg-vorname">{t("Vorname")}</Label>
            <Input id="reg-vorname" value={eingabe.vorname} maxLength={TEXTLAENGE.anmeldeName} onChange={(e) => patch({ vorname: e.target.value })} />
          </div>
          <div className="flex min-w-0 flex-col gap-1.5">
            <Label htmlFor="reg-nachname">{t("Nachname")}</Label>
            <Input id="reg-nachname" value={eingabe.nachname} maxLength={TEXTLAENGE.anmeldeName} onChange={(e) => patch({ nachname: e.target.value })} />
          </div>
        </div>
        <div className="grid min-w-0 grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="flex min-w-0 flex-col gap-1.5">
            <Label htmlFor="reg-rufname">{t("Rufname des Hundes")}</Label>
            <Input id="reg-rufname" value={eingabe.rufname} maxLength={TEXTLAENGE.anmeldeName} onChange={(e) => patch({ rufname: e.target.value })} />
          </div>
          <div className="flex min-w-0 flex-col gap-1.5">
            <Label htmlFor="reg-rasse">{t("Hunderasse")}</Label>
            <Input id="reg-rasse" value={eingabe.rasse} maxLength={TEXTLAENGE.anmeldeRasse} onChange={(e) => patch({ rasse: e.target.value })} />
          </div>
        </div>
        <div className="grid min-w-0 grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="flex min-w-0 flex-col gap-1.5">
            <Label htmlFor="reg-wurftag">{t("Wurftag")}</Label>
            <Input
              id="reg-wurftag"
              type="date"
              value={eingabe.wurftag}
              min={jahreZurueck(referenz, MAX_HUNDEALTER_JAHRE)}
              max={heute}
              onChange={(e) => patch({ wurftag: e.target.value })}
            />
          </div>
          <div className="flex min-w-0 flex-col gap-1.5">
            <Label htmlFor="reg-telefon">{t("Telefonnummer")}</Label>
            <Input
              id="reg-telefon"
              type="tel"
              inputMode="tel"
              value={eingabe.telefon}
              maxLength={TEXTLAENGE.anmeldeTelefon}
              onChange={(e) => patch({ telefon: e.target.value })}
            />
          </div>
        </div>
        <div className="flex min-w-0 flex-col gap-1.5">
          <Label htmlFor="reg-notiz">{t("Notiz (nur für Trainer:innen)")}</Label>
          <Input
            id="reg-notiz"
            value={eingabe.notiz}
            maxLength={TEXTLAENGE.anmeldeNotiz}
            placeholder={t("(optional)")}
            onChange={(e) => patch({ notiz: e.target.value })}
          />
        </div>

        {fehler && (
          <p role="alert" className="text-sm text-destructive [overflow-wrap:anywhere]">
            {fehler}
          </p>
        )}

        <Button type="submit" disabled={speichert} className="h-11">
          {speichert ? t("Speichert…") : t("Speichern")}
        </Button>
      </form>
    </>
  );
}

"use client";

import { useCallback, useRef, useState, type FormEvent } from "react";
import { X } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { GeschlechtWahl } from "@/components/dogs/geschlecht-wahl";
import { pruefeErstenHund } from "@/lib/erststart";
import { formatDogAge } from "@/lib/dog-age";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { useT } from "@/lib/i18n";
import { useTastatur } from "@/lib/use-tastatur";
import type { Dog, DogGender } from "@/lib/types";
import { cn } from "@/lib/utils";

/**
 * Das Fenster "Neuer Hund": Name, Rasse, Geschlecht, Geburtsdatum. Es geht auf der Hundeseite
 * (über das "+" bei den Hunden und "Weiteren Hund anlegen" im Verwalten-Fenster) und im Leerzustand
 * der Hundeliste auf - ohne Seitenwechsel. Nach dem Speichern übernimmt die aufrufende Seite
 * (onAngelegt): Sie wechselt zur Seite des neuen Hundes.
 */
export function NeuerHundSheet({
  open,
  onOpenChange,
  onAngelegt,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onAngelegt: (hund: Dog) => void | Promise<void>;
}) {
  const t = useT();
  // Ob schon etwas im Fenster steht, das beim Schließen verloren ginge (siehe MerkenSheet).
  const entwurfRef = useRef(false);
  const meldeEntwurf = useCallback((vorhanden: boolean) => {
    entwurfRef.current = vorhanden;
  }, []);
  const tastatur = useTastatur(open);

  return (
    <Sheet
      open={open}
      onOpenChange={(offen, details) => {
        if (!offen && entwurfRef.current && (details.reason === "outside-press" || details.reason === "escape-key")) return;
        onOpenChange(offen);
      }}
    >
      <SheetContent
        side="bottom"
        showCloseButton={false}
        className={cn("max-h-[92dvh] gap-0 overflow-hidden rounded-t-xl p-0", tastatur.hoehe !== null && "transition-none")}
        // Bei offener Tastatur (iPhone): über die Tastatur rücken und auf den sichtbaren Bereich schrumpfen.
        style={tastatur.hoehe !== null ? { bottom: tastatur.unten, maxHeight: tastatur.hoehe - 8 } : undefined}
      >
        <NeuerHundInhalt onEntwurf={meldeEntwurf} onClose={() => onOpenChange(false)} onAngelegt={onAngelegt} />
        <SheetDescription className="sr-only">{t("Lege einen neuen Hund an.")}</SheetDescription>
      </SheetContent>
    </Sheet>
  );
}

function NeuerHundInhalt({
  onEntwurf,
  onClose,
  onAngelegt,
}: {
  onEntwurf: (vorhanden: boolean) => void;
  onClose: () => void;
  onAngelegt: (hund: Dog) => void | Promise<void>;
}) {
  const t = useT();
  const [name, setName] = useState("");
  const [breed, setBreed] = useState("");
  // Ohne Vorbelegung: Das Backend nimmt bei fehlender Angabe "Rüde" - eine
  // Hündin würde sonst unbemerkt falsch angelegt. Gespeichert wird erst nach
  // einer Auswahl.
  const [gender, setGender] = useState<DogGender | null>(null);
  const [birthday, setBirthday] = useState("");
  const [versucht, setVersucht] = useState(false);
  const [speichert, setSpeichert] = useState(false);

  const geschlechtFehlt = versucht && gender === null;
  const nameFehlt = versucht && name.trim() === "";

  function aendere(setter: (wert: string) => void, wert: string) {
    setter(wert);
    onEntwurf(true);
  }

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setVersucht(true);
    const eingabe = pruefeErstenHund(name, gender);
    if (!eingabe.ok) return;

    setSpeichert(true);
    try {
      const hund = await api.post<Dog>("/api/dogs", {
        name: eingabe.name,
        breed: breed || null,
        birthday: birthday || null,
        gender: eingabe.geschlecht,
        imageUrl: null,
        notes: null,
      });
      onEntwurf(false);
      onClose();
      await onAngelegt(hund);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Hund konnte nicht angelegt werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={handleCreate} noValidate className="flex min-h-0 flex-1 flex-col">
      <SheetHeader className="shrink-0 flex-row items-center justify-between gap-2 p-4 pb-2">
        <SheetTitle className="text-lg">{t("Neuer Hund")}</SheetTitle>
        <Button type="button" variant="ghost" size="icon" className="size-11 shrink-0" aria-label={t("Schließen")} onClick={onClose}>
          <X className="size-5" />
        </Button>
      </SheetHeader>
      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto overscroll-contain px-4 pb-4">
        <div className="flex flex-col gap-2">
          <Label htmlFor="neuer-hund-name">{t("Name")}</Label>
          <Input
            id="neuer-hund-name"
            required
            value={name}
            maxLength={TEXTLAENGE.hundename}
            aria-invalid={nameFehlt || undefined}
            onChange={(e) => aendere(setName, e.target.value)}
          />
          {nameFehlt && (
            <p role="alert" className="text-sm text-destructive">
              {t("Wie heißt dein Hund?")}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="neuer-hund-rasse">{t("Rasse")}</Label>
          <Input id="neuer-hund-rasse" value={breed} maxLength={TEXTLAENGE.hundeRasse} onChange={(e) => aendere(setBreed, e.target.value)} />
        </div>
        <div className="flex flex-col gap-2">
          <Label id="neuer-hund-geschlecht">{t("Geschlecht")}</Label>
          <GeschlechtWahl wert={gender} onChange={setGender} labelId="neuer-hund-geschlecht" ungueltig={geschlechtFehlt} />
          {geschlechtFehlt && (
            <p role="alert" className="text-sm text-destructive">
              {t("Bitte wähle Rüde oder Hündin.")}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="neuer-hund-geburtstag">{t("Geburtsdatum")}</Label>
          <Input
            id="neuer-hund-geburtstag"
            type="date"
            value={birthday}
            className="h-11 min-w-0"
            onChange={(e) => aendere(setBirthday, e.target.value)}
          />
          <p className="text-xs text-muted-foreground">
            {formatDogAge(birthday, new Date(), t)
              ? t("Alter: {alter}", { alter: formatDogAge(birthday, new Date(), t) ?? "" })
              : t("Optional - daraus wird das Alter berechnet.")}
          </p>
        </div>
      </div>
      <div className="shrink-0 border-t border-surface-border bg-popover px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
        <Button type="submit" className="h-12 w-full text-base font-semibold" disabled={speichert}>
          {speichert ? t("Wird gespeichert…") : t("Speichern")}
        </Button>
      </div>
    </form>
  );
}

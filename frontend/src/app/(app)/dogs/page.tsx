"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import type { Dog, DogGender } from "@/lib/types";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { CircleCheck, Dog as DogIcon, Plus } from "lucide-react";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { HundEinladungen } from "@/components/dogs/hund-einladungen";
import { EintragenSheet } from "@/components/dogs/eintragen-sheet";
import { GeschlechtWahl } from "@/components/dogs/geschlecht-wahl";
import { pruefeErstenHund } from "@/lib/erststart";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { formatDogAge } from "@/lib/dog-age";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

import { useT } from "@/lib/i18n";
export default function DogsPage() {
  const [dogs, setDogs] = useState<Dog[] | null>(null);
  // null = automatisch: offen, solange die geladene Liste leer ist (wer noch
  // keinen Hund hat, ist genau dafür hier). Erst ein Tippen auf den Knopf oder
  // das Speichern legt es fest.
  const [formWunsch, setFormWunsch] = useState<boolean | null>(null);
  const [name, setName] = useState("");
  const [breed, setBreed] = useState("");
  // Ohne Vorbelegung: Das Backend nimmt bei fehlender Angabe "Rüde" - eine
  // Hündin würde sonst unbemerkt falsch angelegt. Gespeichert wird erst nach
  // einer Auswahl.
  const [gender, setGender] = useState<DogGender | null>(null);
  const [versucht, setVersucht] = useState(false);
  // Der gerade angelegte Hund: Statt still in der Liste zu bleiben, bietet die
  // Seite den nächsten Schritt an (erstes Training, Hundeseite).
  const [angelegt, setAngelegt] = useState<Dog | null>(null);
  // Das Eintragen-Fenster hält seinen Hund selbst fest: Nach dem Speichern
  // verschwindet der Hinweis, das Fenster soll aber in Ruhe zugehen können.
  const [eintragenOffen, setEintragenOffen] = useState(false);
  const [eintragenHund, setEintragenHund] = useState<Dog | null>(null);
  const [birthday, setBirthday] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const t = useT();

  async function loadDogs() {
    const cached = await getCachedData<Dog[]>("dogs-list");
    if (cached) setDogs(cached);

    try {
      const data = await api.get<Dog[]>("/api/dogs");
      setDogs(data);
      await setCachedData("dogs-list", data);
    } catch (err) {
      if (!cached) toast.error(err instanceof ApiError ? err.message : t("Hunde konnten nicht geladen werden."));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf beim Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDogs();
  }, []);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setVersucht(true);
    const eingabe = pruefeErstenHund(name, gender);
    if (!eingabe.ok) return;

    setIsSubmitting(true);
    try {
      const hund = await api.post<Dog>("/api/dogs", {
        name: eingabe.name,
        breed: breed || null,
        birthday: birthday || null,
        gender: eingabe.geschlecht,
        imageUrl: null,
        notes: null,
      });
      setName("");
      setBreed("");
      setGender(null);
      setVersucht(false);
      setBirthday("");
      setFormWunsch(false);
      setAngelegt(hund);
      await loadDogs();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Hund konnte nicht angelegt werden."));
    } finally {
      setIsSubmitting(false);
    }
  }

  // Archivierte Hunde (z.B. verstorben) getrennt vom aktiven Bestand anzeigen -
  // sie bleiben erreichbar, verstellen aber nicht die tägliche Liste.
  const activeDogs = dogs?.filter((d) => !d.archivedAt) ?? [];
  const archivedDogs = dogs?.filter((d) => d.archivedAt) ?? [];
  const showForm = formWunsch ?? dogs?.length === 0;
  const geschlechtFehlt = versucht && gender === null;
  const nameFehlt = versucht && name.trim() === "";

  function dogCard(dog: Dog, archived = false) {
    return (
      <Link key={dog.id} href={`/dogs/${dog.id}`}>
        <Card className={`transition-colors hover:bg-accent/10 ${archived ? "opacity-70" : ""}`}>
          <CardHeader className="flex flex-row items-center gap-3 space-y-0">
            <DogAvatar dogId={dog.id} hasImage={dog.hasImage} name={dog.name} />
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <CardTitle className="text-base [overflow-wrap:anywhere]">{dog.name}</CardTitle>
                {archived && <Badge variant="secondary">{t("Archiviert")}</Badge>}
              </div>
              <p className="text-sm text-muted-foreground">
                {[dog.breed ?? t("Unbekannte Rasse"), formatDogAge(dog.birthday, new Date(), t)].filter(Boolean).join(" · ")}
              </p>
            </div>
          </CardHeader>
        </Card>
      </Link>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold tracking-tight">{t("Meine Hunde")}</h1>
        <Button
          onClick={() => {
            setFormWunsch(!showForm);
            setAngelegt(null);
          }}
          size="sm"
        >
          <Plus className="size-4" />
          {t("Hund hinzufügen")}
        </Button>
      </div>

      <HundEinladungen onAngenommen={loadDogs} />

      {angelegt && (
        <Card className="border-primary/40 bg-primary/5">
          <CardContent className="flex flex-col gap-3">
            <p role="status" className="flex min-w-0 items-start gap-2 font-medium">
              <CircleCheck className="mt-0.5 size-5 shrink-0 text-primary-text" aria-hidden />
              <span className="min-w-0 [overflow-wrap:anywhere]">{t("{name} ist angelegt.", { name: angelegt.name })}</span>
            </p>
            <div className="flex flex-col gap-2 sm:flex-row">
              <Button
                className="h-11 min-w-0 flex-1 text-base"
                aria-haspopup="dialog"
                onClick={() => {
                  setEintragenHund(angelegt);
                  setEintragenOffen(true);
                }}
              >
                {t("Erstes Training eintragen")}
              </Button>
              <Link
                href={`/dogs/${angelegt.id}`}
                className={cn(buttonVariants({ variant: "outline" }), "h-11 min-w-0 flex-1 text-base")}
              >
                {t("Zum Hund")}
              </Link>
            </div>
          </CardContent>
        </Card>
      )}

      {eintragenHund && (
        <EintragenSheet
          open={eintragenOffen}
          onOpenChange={setEintragenOffen}
          dogId={eintragenHund.id}
          hunde={[eintragenHund]}
          onSaved={async () => {
            // Das Training steht - der Hinweis hat seine Aufgabe erfüllt.
            setAngelegt(null);
          }}
        />
      )}

      {showForm && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t("Neuer Hund")}</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleCreate} noValidate className="flex flex-col gap-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="name">{t("Name")}</Label>
                  <Input
                    id="name"
                    required
                    value={name}
                    maxLength={TEXTLAENGE.hundename}
                    aria-invalid={nameFehlt || undefined}
                    onChange={(e) => setName(e.target.value)}
                  />
                  {nameFehlt && (
                    <p role="alert" className="text-sm text-destructive">
                      {t("Wie heißt dein Hund?")}
                    </p>
                  )}
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="breed">{t("Rasse")}</Label>
                  <Input id="breed" value={breed} maxLength={TEXTLAENGE.hundeRasse} onChange={(e) => setBreed(e.target.value)} />
                </div>
                <div className="flex flex-col gap-2">
                  <Label id="gender">{t("Geschlecht")}</Label>
                  <GeschlechtWahl wert={gender} onChange={setGender} labelId="gender" ungueltig={geschlechtFehlt} />
                  {geschlechtFehlt && (
                    <p role="alert" className="text-sm text-destructive">
                      {t("Bitte wähle Rüde oder Hündin.")}
                    </p>
                  )}
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="birthday">{t("Geburtsdatum")}</Label>
                  <Input id="birthday" type="date" value={birthday} onChange={(e) => setBirthday(e.target.value)} />
                  <p className="text-xs text-muted-foreground">
                    {formatDogAge(birthday, new Date(), t)
                      ? t("Alter: {alter}", { alter: formatDogAge(birthday, new Date(), t) ?? "" })
                      : t("Optional - daraus wird das Alter berechnet.")}
                  </p>
                </div>
              </div>
              <Button type="submit" className="self-start" disabled={isSubmitting}>
                {isSubmitting ? t("Wird gespeichert…") : t("Speichern")}
              </Button>
            </form>
          </CardContent>
        </Card>
      )}

      {dogs === null ? (
        <p className="text-muted-foreground">{t("Lädt…")}</p>
      ) : dogs.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-12 text-center text-muted-foreground">
            <DogIcon className="size-10" />
            <p>{t("Leg deinen ersten Hund an.")}</p>
          </CardContent>
        </Card>
      ) : (
        <>
          {activeDogs.length > 0 ? (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {activeDogs.map((dog) => dogCard(dog))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t("Alle Hunde sind archiviert.")}</p>
          )}

          {archivedDogs.length > 0 && (
            <div className="flex flex-col gap-3">
              <h2 className="text-sm font-medium text-muted-foreground">{t("Archivierte Hunde")}</h2>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {archivedDogs.map((dog) => dogCard(dog, true))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

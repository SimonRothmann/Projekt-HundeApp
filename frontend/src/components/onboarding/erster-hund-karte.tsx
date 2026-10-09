"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { ChevronRight, Dog as DogIcon } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { pruefeErstenHund } from "@/lib/erststart";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { Dog, DogGender } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { GeschlechtWahl } from "@/components/dogs/geschlecht-wahl";
import { AusblendenKnopf } from "@/components/onboarding/ausblenden-knopf";
import { useT } from "@/lib/i18n";

/**
 * Der erste Handgriff der App: "Wie heißt dein Hund?".
 *
 * Steht auf der Startseite, solange es keinen Hund gibt, und ersetzt dort den
 * früheren Weg über die Hundeliste (Knopf, Seitenwechsel, Formular mit vier
 * Feldern, danach wieder zurück). Gefragt wird nur, was der Rest der App
 * braucht: Name und Geschlecht. Rasse und Geburtstag lassen sich später unter
 * "Bearbeiten" auf der Hundeseite ergänzen.
 *
 * Kein autoFocus: iOS risse sonst beim Laden die Tastatur hoch, bevor man auch
 * nur die Karte gesehen hat.
 *
 * Wer stattdessen einen Einladungslink seines Vereins hat, braucht diese Karte
 * nicht - die Zeile darunter führt dorthin.
 */
export function ErsterHundKarte({
  onAngelegt,
  onDismissed,
}: {
  /** Der Hund ist angelegt; die Startseite lädt neu und öffnet das Eintragen-Fenster. */
  onAngelegt: (hund: Dog) => Promise<void>;
  onDismissed: () => void;
}) {
  const t = useT();
  const [name, setName] = useState("");
  const [geschlecht, setGeschlecht] = useState<DogGender | null>(null);
  // Erst nach dem ersten Versuch zu speichern zeigen wir, was fehlt - vorher
  // wäre jedes leere Feld schon ein Vorwurf.
  const [versucht, setVersucht] = useState(false);
  const [laeuft, setLaeuft] = useState(false);

  const eingabe = pruefeErstenHund(name, geschlecht);
  const fehler = versucht && !eingabe.ok ? eingabe.fehler : null;

  async function weiter(e: FormEvent) {
    e.preventDefault();
    setVersucht(true);
    if (!eingabe.ok) return;

    setLaeuft(true);
    try {
      const hund = await api.post<Dog>("/api/dogs", {
        name: eingabe.name,
        breed: null,
        birthday: null,
        gender: eingabe.geschlecht,
        imageUrl: null,
        notes: null,
      });
      await onAngelegt(hund);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Hund konnte nicht angelegt werden."));
    } finally {
      setLaeuft(false);
    }
  }

  return (
    <Card className="border-primary/40 bg-primary/5">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <DogIcon className="size-5 shrink-0" aria-hidden />
          {t("Wie heißt dein Hund?")}
        </CardTitle>
      </CardHeader>

      <CardContent className="flex flex-col gap-4">
        <form onSubmit={weiter} noValidate className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="erster-hund-name">{t("Name")}</Label>
            <Input
              id="erster-hund-name"
              className="h-11"
              value={name}
              maxLength={TEXTLAENGE.hundename}
              autoComplete="off"
              autoCapitalize="words"
              enterKeyHint="go"
              aria-invalid={fehler === "name" || undefined}
              aria-describedby={fehler === "name" ? "erster-hund-fehler" : undefined}
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label id="erster-hund-geschlecht">{t("Geschlecht")}</Label>
            <GeschlechtWahl
              wert={geschlecht}
              onChange={setGeschlecht}
              labelId="erster-hund-geschlecht"
              ungueltig={fehler === "geschlecht"}
            />
          </div>

          {fehler && (
            <p id="erster-hund-fehler" role="alert" className="text-sm text-destructive">
              {fehler === "name" ? t("Wie heißt dein Hund?") : t("Bitte wähle Rüde oder Hündin.")}
            </p>
          )}

          <Button type="submit" className="h-11 w-full text-base" disabled={laeuft}>
            {laeuft ? t("Wird gespeichert…") : t("Weiter")}
            {!laeuft && <ChevronRight className="size-4" aria-hidden />}
          </Button>
        </form>

        <Link
          href="/clubs"
          className="flex min-h-11 min-w-0 items-center gap-2 rounded-lg border border-border/60 bg-card px-3 py-2 text-sm transition-colors hover:border-primary/40"
        >
          <span className="min-w-0 flex-1">
            <span className="block font-medium [overflow-wrap:anywhere]">
              {t("Du hast einen Einladungslink von deinem Verein?")}
            </span>
            <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">
              {t("Such deinen Verein hier und stell eine Beitrittsanfrage – dein Trainer sieht dann eure Trainings.")}
            </span>
          </span>
          <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        </Link>

        <AusblendenKnopf onDismissed={onDismissed} />
      </CardContent>
    </Card>
  );
}

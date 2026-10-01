"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { CheckCircle2, PawPrint } from "lucide-react";
import { ApiError, api } from "@/lib/api";
import { istGueltigerAnmeldecode, jahreZurueck, MAX_HUNDEALTER_JAHRE, pruefeAnmeldung } from "@/lib/anmeldung";
import { anmeldeFehlerSatz } from "@/lib/anmeldung-texte";
import { useT } from "@/lib/i18n";
import { heuteIso } from "@/lib/pruefung";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { GroupRegistrationForm } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { RechtlicheLinks } from "@/components/rechtliche-links";

type Stand =
  | { art: "laedt" }
  | { art: "gueltig"; vereinsname: string | null; gruppenname: string }
  | { art: "ungueltig" }
  // Netz weg oder Server gerade nicht erreichbar - nicht dasselbe wie ein
  // geschlossenes Formular, und der Link soll dabei nicht als ungültig erscheinen.
  | { art: "nichtErreichbar" }
  | { art: "angemeldet"; vorname: string; gruppenname: string };

/**
 * Öffentliches Anmeldeformular einer Gruppe, erreichbar ohne Konto.
 *
 * Wer sich hier anmeldet, ist Kursteilnehmer:in und kein App-Nutzer:in: Es
 * gibt kein Konto, keine E-Mail-Adresse und keine Bestätigung per Link - der
 * Verein meldet sich telefonisch. Der Server gibt ohne Anmeldung nur Vereins-
 * und Gruppenname heraus.
 *
 * Das Feld "website" ist ein Köder gegen Roboter: Menschen sehen es nicht und
 * lassen es leer (der Server speichert nichts, wenn es gefüllt ist).
 */
export function Anmeldeformular({ code }: { code: string }) {
  const t = useT();
  const formatOk = istGueltigerAnmeldecode(code);
  const [stand, setStand] = useState<Stand>({ art: "laedt" });
  const [versuch, setVersuch] = useState(0);

  const [vorname, setVorname] = useState("");
  const [nachname, setNachname] = useState("");
  const [rufname, setRufname] = useState("");
  const [rasse, setRasse] = useState("");
  const [wurftag, setWurftag] = useState("");
  const [telefon, setTelefon] = useState("");
  const [koeder, setKoeder] = useState("");
  const [einwilligung, setEinwilligung] = useState(false);
  const [sendet, setSendet] = useState(false);
  const [fehler, setFehler] = useState<string | null>(null);

  useEffect(() => {
    // Ein Code, der nie einer sein konnte, braucht keine Anfrage.
    if (!formatOk) return;
    let abgebrochen = false;
    api
      .get<GroupRegistrationForm>(`/api/groups/registration/${code}`)
      .then((vorschau) => {
        if (!abgebrochen) setStand({ art: "gueltig", vereinsname: vorschau.clubName, gruppenname: vorschau.groupName });
      })
      .catch((err) => {
        if (abgebrochen) return;
        // Nur 404 heißt "geschlossen". Ein 429 (zu viele Anfragen) oder ein
        // Serverfehler sagt nichts über den Link.
        setStand(err instanceof ApiError && err.status === 404 ? { art: "ungueltig" } : { art: "nichtErreichbar" });
      });
    return () => {
      abgebrochen = true;
    };
  }, [code, formatOk, versuch]);

  const aktuell: Stand = formatOk ? stand : { art: "ungueltig" };
  const heute = heuteIso();

  /** Die erste Unstimmigkeit in der Eingabe - dieselben Regeln wie der Server, damit man sie vor dem Senden sieht. */
  function pruefe(): string | null {
    const fehler = pruefeAnmeldung({ vorname, nachname, rufname, rasse, wurftag, telefon }, heute);
    if (fehler) return anmeldeFehlerSatz(t, fehler);
    if (!einwilligung) return t("Bitte stimme der Speicherung deiner Angaben zu.");
    return null;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (aktuell.art !== "gueltig") return;
    const problem = pruefe();
    setFehler(problem);
    if (problem) return;

    setSendet(true);
    try {
      await api.post(`/api/groups/registration/${code}`, {
        firstName: vorname.trim(),
        lastName: nachname.trim(),
        dogName: rufname.trim(),
        dogBreed: rasse.trim(),
        dogBirthDate: wurftag,
        phone: telefon.trim(),
        consent: einwilligung,
        website: koeder,
      });
      setStand({ art: "angemeldet", vorname: vorname.trim(), gruppenname: aktuell.gruppenname });
    } catch (err) {
      // Nur eine Prüfmeldung des Servers (400) ist für Menschen geschrieben.
      // Drosselung (429, gerade im Vereins-WLAN, wo viele dieselbe Adresse
      // teilen), Serverfehler und Netzprobleme bekommen einen klaren Satz statt
      // eines technischen Codes.
      if (err instanceof ApiError && err.status === 404) setStand({ art: "ungueltig" });
      else if (err instanceof ApiError && err.status === 400 && err.errors.length > 0) setFehler(err.message);
      else setFehler(t("Die Anmeldung konnte nicht gesendet werden. Bitte versuche es gleich noch einmal."));
    } finally {
      setSendet(false);
    }
  }

  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <div className="flex w-full max-w-md min-w-0 flex-col gap-3">
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary-text" />
            {aktuell.art === "gueltig" ? (
              <>
                {aktuell.vereinsname && (
                  <p className="min-w-0 text-sm text-muted-foreground [overflow-wrap:anywhere]">{aktuell.vereinsname}</p>
                )}
                <CardTitle className="min-w-0 text-xl [overflow-wrap:anywhere]">
                  <h1>{t("Anmeldung zur {gruppe}", { gruppe: aktuell.gruppenname })}</h1>
                </CardTitle>
              </>
            ) : aktuell.art === "angemeldet" ? (
              <CardTitle className="text-xl">
                <h1>{t("Angemeldet")}</h1>
              </CardTitle>
            ) : (
              <CardTitle className="text-xl">
                <h1>Dogity</h1>
              </CardTitle>
            )}
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {aktuell.art === "laedt" && <p className="text-center text-muted-foreground">{t("Lädt…")}</p>}

            {aktuell.art === "ungueltig" && (
              <p className="text-center">{t("Diese Anmeldung ist geschlossen. Frag im Verein nach.")}</p>
            )}

            {aktuell.art === "nichtErreichbar" && (
              <>
                <p className="text-center">
                  {t("Das Anmeldeformular konnte gerade nicht geladen werden. Bitte versuche es gleich noch einmal.")}
                </p>
                <Button
                  variant="outline"
                  className="h-11"
                  onClick={() => {
                    setStand({ art: "laedt" });
                    setVersuch((v) => v + 1);
                  }}
                >
                  {t("Erneut versuchen")}
                </Button>
              </>
            )}

            {aktuell.art === "angemeldet" && (
              <div className="flex flex-col items-center gap-3 text-center">
                <CheckCircle2 className="size-10 text-emerald-600 dark:text-emerald-500" />
                <p className="min-w-0 font-medium [overflow-wrap:anywhere]">
                  {t("Danke, {vorname}! Du bist für die {gruppe} angemeldet. Der Verein meldet sich bei dir.", {
                    vorname: aktuell.vorname,
                    gruppe: aktuell.gruppenname,
                  })}
                </p>
              </div>
            )}

            {aktuell.art === "gueltig" && (
              <form onSubmit={handleSubmit} noValidate className="flex min-w-0 flex-col gap-4">
                <div className="grid min-w-0 grid-cols-1 gap-4 sm:grid-cols-2">
                  <div className="flex min-w-0 flex-col gap-1.5">
                    <Label htmlFor="anm-vorname">{t("Vorname")}</Label>
                    <Input
                      id="anm-vorname"
                      value={vorname}
                      onChange={(e) => setVorname(e.target.value)}
                      maxLength={TEXTLAENGE.anmeldeName}
                      autoComplete="given-name"
                      required
                    />
                  </div>
                  <div className="flex min-w-0 flex-col gap-1.5">
                    <Label htmlFor="anm-nachname">{t("Nachname")}</Label>
                    <Input
                      id="anm-nachname"
                      value={nachname}
                      onChange={(e) => setNachname(e.target.value)}
                      maxLength={TEXTLAENGE.anmeldeName}
                      autoComplete="family-name"
                      required
                    />
                  </div>
                </div>

                <div className="flex min-w-0 flex-col gap-1.5">
                  <Label htmlFor="anm-rufname">{t("Rufname des Hundes")}</Label>
                  <Input
                    id="anm-rufname"
                    value={rufname}
                    onChange={(e) => setRufname(e.target.value)}
                    maxLength={TEXTLAENGE.anmeldeName}
                    autoComplete="off"
                    required
                  />
                </div>

                <div className="flex min-w-0 flex-col gap-1.5">
                  <Label htmlFor="anm-rasse">{t("Hunderasse")}</Label>
                  <Input
                    id="anm-rasse"
                    value={rasse}
                    onChange={(e) => setRasse(e.target.value)}
                    maxLength={TEXTLAENGE.anmeldeRasse}
                    autoComplete="off"
                    required
                  />
                </div>

                <div className="flex min-w-0 flex-col gap-1.5">
                  <Label htmlFor="anm-wurftag">{t("Wurftag")}</Label>
                  <Input
                    id="anm-wurftag"
                    type="date"
                    value={wurftag}
                    onChange={(e) => setWurftag(e.target.value)}
                    min={jahreZurueck(heute, MAX_HUNDEALTER_JAHRE)}
                    max={heute}
                    required
                  />
                </div>

                <div className="flex min-w-0 flex-col gap-1.5">
                  <Label htmlFor="anm-telefon">{t("Telefonnummer")}</Label>
                  <Input
                    id="anm-telefon"
                    type="tel"
                    inputMode="tel"
                    value={telefon}
                    onChange={(e) => setTelefon(e.target.value)}
                    maxLength={TEXTLAENGE.anmeldeTelefon}
                    autoComplete="tel"
                    required
                  />
                </div>

                {/* Köder gegen Roboter: außerhalb des Bildes, nicht per Tab erreichbar,
                    für Screenreader verborgen. Menschen füllen es nie. */}
                <div aria-hidden="true" className="absolute -left-[9999px] h-0 w-0 overflow-hidden">
                  <label htmlFor="anm-website">Website</label>
                  <input
                    id="anm-website"
                    name="website"
                    type="text"
                    tabIndex={-1}
                    autoComplete="off"
                    value={koeder}
                    onChange={(e) => setKoeder(e.target.value)}
                  />
                </div>

                <label className="flex min-w-0 cursor-pointer items-start gap-3 text-sm">
                  <input
                    type="checkbox"
                    className="mt-0.5 size-5 shrink-0 accent-primary"
                    checked={einwilligung}
                    onChange={(e) => setEinwilligung(e.target.checked)}
                  />
                  <span className="min-w-0 [overflow-wrap:anywhere]">
                    {t("Ich bin einverstanden, dass der Verein meine Angaben zur Organisation der Gruppe speichert.")}{" "}
                    {/* In neuem Tab: Sonst ginge beim Nachlesen die halb ausgefüllte Anmeldung verloren. */}
                    <Link
                      href="/datenschutz"
                      target="_blank"
                      rel="noopener"
                      className="text-primary-text underline underline-offset-4"
                    >
                      {t("Datenschutzerklärung")}
                    </Link>
                  </span>
                </label>

                {fehler && (
                  <p role="alert" className="text-sm text-destructive [overflow-wrap:anywhere]">
                    {fehler}
                  </p>
                )}

                <Button type="submit" className={cn("h-11 text-base")} disabled={sendet}>
                  {sendet ? t("Wird gesendet…") : t("Anmelden")}
                </Button>
              </form>
            )}

            {(aktuell.art === "ungueltig" || aktuell.art === "angemeldet") && (
              <Link href="/" className={cn(buttonVariants({ variant: "outline" }), "h-11")}>
                {t("Zur Startseite")}
              </Link>
            )}
          </CardContent>
        </Card>
        <RechtlicheLinks />
      </div>
    </main>
  );
}

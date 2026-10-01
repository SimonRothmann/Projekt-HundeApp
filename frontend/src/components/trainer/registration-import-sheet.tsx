"use client";

import { useState, type ChangeEvent } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import {
  bildeVorschau,
  inStuecke,
  leseAnmeldungen,
  MAX_DATEIGROESSE,
  type CsvLesen,
  type ImportVorschau,
} from "@/lib/anmeldung-csv";
import { anmeldeFehlerKurz, spaltenName } from "@/lib/anmeldung-texte";
import { useT } from "@/lib/i18n";
import type { GroupRegistration, ImportRegistrationsResult } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/** So viele fehlerhafte Zeilen werden einzeln aufgelistet - der Rest als Zahl. */
const MAX_FEHLERZEILEN = 10;

/**
 * Übernimmt die Antworten eines früheren Google-Formulars als CSV.
 *
 * Die Datei wird im Browser gelesen und geprüft; erst "Importieren" schickt die
 * gültigen Zeilen an den Server, der jede noch einmal wie das Formular prüft.
 * Die Vorschau zeigt vorher, was passieren wird - neue, schon vorhandene und
 * fehlerhafte Zeilen -, damit ein falsches Blatt nicht still etwas anlegt.
 */
export function RegistrationImportSheet({
  groupId,
  bestehende,
  offen,
  onOffenChange,
  onImportiert,
}: {
  groupId: string;
  bestehende: GroupRegistration[];
  offen: boolean;
  onOffenChange: (offen: boolean) => void;
  onImportiert: () => Promise<void> | void;
}) {
  const t = useT();
  return (
    <Sheet open={offen} onOpenChange={onOffenChange}>
      <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{t("Aus Google-Formular importieren")}</SheetTitle>
          <SheetDescription>{t("Lade die bisherigen Antworten als CSV-Datei und übernimm sie in diese Gruppe.")}</SheetDescription>
        </SheetHeader>
        <Inhalt
          groupId={groupId}
          bestehende={bestehende}
          onFertig={async () => {
            onOffenChange(false);
            await onImportiert();
          }}
        />
      </SheetContent>
    </Sheet>
  );
}

type Datei =
  | { art: "keine" }
  | { art: "zuGross" }
  | { art: "nichtLesbar" }
  | { art: "gelesen"; name: string; lesen: CsvLesen };

function Inhalt({
  groupId,
  bestehende,
  onFertig,
}: {
  groupId: string;
  bestehende: GroupRegistration[];
  onFertig: () => Promise<void>;
}) {
  const t = useT();
  const [datei, setDatei] = useState<Datei>({ art: "keine" });
  const [sendet, setSendet] = useState(false);
  // Was der Server zu Zeilen gesagt hat, die er trotz Prüfung im Browser abgelehnt hat.
  const [serverFehler, setServerFehler] = useState<ImportRegistrationsResult["fehler"]>([]);

  async function dateiGewaehlt(e: ChangeEvent<HTMLInputElement>) {
    const gewaehlt = e.target.files?.[0];
    setServerFehler([]);
    if (!gewaehlt) {
      setDatei({ art: "keine" });
      return;
    }
    if (gewaehlt.size > MAX_DATEIGROESSE) {
      setDatei({ art: "zuGross" });
      return;
    }
    try {
      setDatei({ art: "gelesen", name: gewaehlt.name, lesen: leseAnmeldungen(await gewaehlt.text()) });
    } catch {
      setDatei({ art: "nichtLesbar" });
    }
  }

  const vorschau: ImportVorschau | null =
    datei.art === "gelesen" && datei.lesen.art === "ok" ? bildeVorschau(datei.lesen.zeilen, bestehende.map((r) => ({ phone: r.phone, dogName: r.dogName }))) : null;

  async function importieren() {
    if (!vorschau || vorschau.neu.length === 0) return;
    setSendet(true);
    setServerFehler([]);
    // Außerhalb des try: Bricht ein späteres Stück ab, zählt der catch, was
    // schon drin ist.
    let angelegt = 0;
    try {
      // Der Server nimmt höchstens 500 Zeilen je Aufruf: größere Dateien gehen in Stücken.
      const fehler: ImportRegistrationsResult["fehler"] = [];
      for (const stueck of inStuecke(vorschau.neu)) {
        const ergebnis = await api.post<ImportRegistrationsResult>(`/api/groups/${groupId}/registrations/import`, {
          rows: stueck.map((z) => ({
            zeile: z.zeile,
            firstName: z.vorname,
            lastName: z.nachname,
            dogName: z.rufname,
            dogBreed: z.rasse,
            dogBirthDate: z.wurftag,
            phone: z.telefon,
            registeredAt: z.zeitstempel,
          })),
        });
        angelegt += ergebnis.angelegt;
        fehler.push(...ergebnis.fehler);
      }
      toast.success(angelegt === 1 ? t("1 Anmeldung importiert.") : t("{n} Anmeldungen importiert.", { n: angelegt }));
      if (fehler.length === 0) {
        await onFertig();
      } else {
        // Der Server hat etwas abgelehnt, das der Browser durchgelassen hat
        // (etwa die Obergrenze): Das bleibt sichtbar, statt zu verschwinden.
        setServerFehler(fehler);
        setDatei({ art: "keine" });
      }
    } catch (err) {
      // Bei großen Dateien können frühere Stücke schon gespeichert sein. Das
      // sagt die Meldung, und Liste und Vorschau laden den echten Stand - ein
      // zweiter Versuch ist harmlos, der Server überspringt Doppelte.
      const grund = err instanceof ApiError ? err.message : t("Der Import hat nicht geklappt.");
      toast.error(
        angelegt > 0
          ? t("Der Import wurde unterbrochen ({grund}). {n} Anmeldungen sind schon übernommen - versuche es noch einmal, Doppelte werden übersprungen.", { grund, n: angelegt })
          : grund,
      );
      if (angelegt > 0) await onFertig();
    } finally {
      setSendet(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      <ol className="flex list-decimal flex-col gap-1 pl-5 text-sm text-muted-foreground">
        <li>{t("Öffne dein Google-Formular und wähle „Antworten“.")}</li>
        <li>{t("Tippe auf „In Google Tabellen ansehen“.")}</li>
        <li>{t("Wähle in der Tabelle „Datei“, dann „Herunterladen“ und „Kommagetrennte Werte (.csv)“.")}</li>
        <li>{t("Wähle die heruntergeladene Datei hier aus.")}</li>
      </ol>

      <div className="flex min-w-0 flex-col gap-1.5">
        <Label htmlFor="import-datei">{t("CSV-Datei")}</Label>
        <Input id="import-datei" type="file" accept=".csv,text/csv" onChange={(e) => void dateiGewaehlt(e)} className="h-auto min-w-0 py-2" />
      </div>

      {datei.art === "zuGross" && <p role="alert" className="text-sm text-destructive">{t("Die Datei ist zu groß für einen Import.")}</p>}
      {datei.art === "nichtLesbar" && <p role="alert" className="text-sm text-destructive">{t("Die Datei konnte nicht gelesen werden.")}</p>}

      {datei.art === "gelesen" && datei.lesen.art === "leer" && (
        <p role="alert" className="text-sm text-destructive">{t("In der Datei stehen keine Anmeldungen.")}</p>
      )}

      {datei.art === "gelesen" && datei.lesen.art === "spaltenFehlen" && (
        <p role="alert" className="text-sm text-destructive [overflow-wrap:anywhere]">
          {t("In der Datei fehlen Spalten: {spalten}. Ist es die Antwortliste des Anmeldeformulars?", {
            spalten: datei.lesen.fehlend.map((s) => spaltenName(t, s)).join(", "),
          })}
        </p>
      )}

      {vorschau && (
        <div className="flex min-w-0 flex-col gap-2 rounded-lg border bg-muted/40 p-3 text-sm">
          <p className="font-medium [overflow-wrap:anywhere]">
            {t("{n} neue, {m} schon vorhanden, {k} fehlerhaft", {
              n: vorschau.neu.length,
              m: vorschau.vorhanden.length,
              k: vorschau.fehlerhaft.length,
            })}
          </p>
          {vorschau.fehlerhaft.length > 0 && (
            <>
              <ul className="flex flex-col gap-0.5 text-muted-foreground">
                {vorschau.fehlerhaft.slice(0, MAX_FEHLERZEILEN).map((z) => (
                  <li key={z.zeile} className="[overflow-wrap:anywhere]">
                    {t("Zeile {zeile}: {fehler}", { zeile: z.zeile, fehler: z.fehler ? anmeldeFehlerKurz(t, z.fehler) : "" })}
                  </li>
                ))}
              </ul>
              {vorschau.fehlerhaft.length > MAX_FEHLERZEILEN && (
                <p className="text-muted-foreground">{t("… und {n} weitere", { n: vorschau.fehlerhaft.length - MAX_FEHLERZEILEN })}</p>
              )}
              <p className="text-xs text-muted-foreground">{t("Fehlerhafte Zeilen werden nicht importiert. Du kannst sie danach von Hand hinzufügen.")}</p>
            </>
          )}
        </div>
      )}

      {serverFehler.length > 0 && (
        <div role="alert" className="flex min-w-0 flex-col gap-1 text-sm text-destructive">
          <p className="font-medium">{t("Der Server hat einige Zeilen abgelehnt:")}</p>
          <ul>
            {serverFehler.slice(0, MAX_FEHLERZEILEN).map((f) => (
              <li key={f.zeile} className="[overflow-wrap:anywhere]">{t("Zeile {zeile}: {fehler}", { zeile: f.zeile, fehler: f.meldung })}</li>
            ))}
          </ul>
        </div>
      )}

      <Button className="h-11" disabled={sendet || !vorschau || vorschau.neu.length === 0} onClick={() => void importieren()}>
        {sendet ? t("Importiert…") : t("Importieren")}
      </Button>
    </div>
  );
}

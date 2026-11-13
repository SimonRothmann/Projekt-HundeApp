"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Printer } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { formatDogAge } from "@/lib/dog-age";
import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { punkteText } from "@/lib/pruefung";
import { datumLang, ergebnisWort, gruppiereTeams, hatErgebnisse, hundName, teamName, uhrzeitText, zaehleErgebnisse } from "@/lib/pruefungen";
import { PRUEFUNG_ERGEBNIS, TEAM_ART, type PruefungTeam, type VereinsPruefung } from "@/lib/types";
import { Button, buttonVariants } from "@/components/ui/button";

/**
 * Druckansicht eines Prüfungstags: die Starterliste (Ordnung, Startnummer, Hundeführer:in, Hund, Rasse
 * und Alter, bei Gästen Verein) und - sobald Ergebnisse eingetragen sind - die Ergebnisliste. Die
 * Tabellen scrollen am Telefon nicht seitlich: Die Spalten brechen um, gedruckt füllen sie die Seite.
 */
export default function PruefungDruckPage() {
  const t = useT();
  const sprache = useSprache();
  const { examId } = useParams<{ examId: string }>();
  const [pruefung, setPruefung] = useState<VereinsPruefung | null>(null);
  const [vereinId, setVereinId] = useState("");
  const [nichtGefunden, setNichtGefunden] = useState(false);

  useEffect(() => {
    const verein = new URLSearchParams(window.location.search).get("verein");
    if (!verein) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setNichtGefunden(true);
      return;
    }
    setVereinId(verein);
    api
      .get<VereinsPruefung>(`/api/clubs/${verein}/exams/${examId}`)
      .then(setPruefung)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : t("Prüfung konnte nicht geladen werden.")));
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [examId]);

  if (nichtGefunden) return <p className="p-6 text-muted-foreground">{t("Diese Prüfung gibt es nicht.")}</p>;
  if (!pruefung) return <p className="p-6 text-muted-foreground">{t("Lädt…")}</p>;

  const gruppen = gruppiereTeams(pruefung);
  const mitErgebnissen = hatErgebnisse(pruefung.entries);
  const zaehlung = zaehleErgebnisse(pruefung.entries);
  const uhr = uhrzeitText(pruefung.startTime);

  /** Rasse und Alter eines Mitglied-Teams, bei einem Gast die Angabe zu Verein und Rasse. */
  const zusatz = (team: PruefungTeam) =>
    team.kind === TEAM_ART.Gast
      ? (team.info ?? "")
      : [team.breed, formatDogAge(team.birthday, new Date(), t)].filter(Boolean).join(" · ");

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-8 p-6 print:p-0">
      <div className="flex flex-wrap items-center justify-between gap-3 print:hidden">
        <h1 className="min-w-0 text-2xl font-semibold tracking-tight [overflow-wrap:anywhere]">{t("Druckansicht")}</h1>
        <div className="flex flex-wrap gap-2">
          <Link
            href={`/trainer/pruefungen/${examId}?verein=${vereinId}`}
            className={buttonVariants({ variant: "outline", className: "coarse:min-h-11" })}
          >
            {t("Zur Prüfung")}
          </Link>
          <Button onClick={() => window.print()} className="coarse:min-h-11">
            <Printer className="size-4" />
            {t("Drucken")}
          </Button>
        </div>
      </div>

      <header className="border-b pb-4">
        <h1 className="hidden text-2xl font-semibold print:block [overflow-wrap:anywhere]">{pruefung.clubName}</h1>
        <p className="text-lg font-medium [overflow-wrap:anywhere]">{datumLang(pruefung.date, sprache)}</p>
        <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{[uhr, pruefung.location].filter(Boolean).join(" · ")}</p>
        {pruefung.judgeName && <p className="text-sm [overflow-wrap:anywhere]">{t("Richter:in: {name}", { name: pruefung.judgeName })}</p>}
        {pruefung.note && <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{pruefung.note}</p>}
      </header>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold">{t("Starterliste")}</h2>
        {gruppen.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Noch kein Team eingetragen.")}</p>
        ) : (
          gruppen.map(({ ordnung, teams }) => (
            <div key={ordnung.id} className="break-inside-avoid">
              <h3 className="mb-1 font-medium [overflow-wrap:anywhere]">{katalogName(ordnung, sprache)}</h3>
              <table className="w-full table-fixed text-sm hyphens-auto [overflow-wrap:anywhere]">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="w-10 py-1 pr-2">{t("Nr.")}</th>
                    <th className="py-1 pr-2">{t("Hundeführer:in")}</th>
                    <th className="py-1 pr-2">{t("Hund")}</th>
                    <th className="py-1">{t("Rasse / Alter / Verein")}</th>
                  </tr>
                </thead>
                <tbody>
                  {teams.map((team) => (
                    <tr key={team.id} className="border-b align-top last:border-b-0">
                      <td className="py-1.5 pr-2 tabular-nums">{team.startNumber}</td>
                      <td className="py-1.5 pr-2 [overflow-wrap:anywhere]">{teamName(t, team)}</td>
                      <td className="py-1.5 pr-2 [overflow-wrap:anywhere]">{hundName(team)}</td>
                      <td className="py-1.5 [overflow-wrap:anywhere]">{zusatz(team)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))
        )}
      </section>

      {mitErgebnissen && (
        <section className="flex flex-col gap-4 print:break-before-page">
          <h2 className="text-lg font-semibold">{t("Ergebnisse")}</h2>
          <p className="text-sm text-muted-foreground">
            {t("{bestanden} bestanden, {nicht} nicht bestanden, {fern} nicht angetreten, {offen} offen", {
              bestanden: zaehlung.bestanden,
              nicht: zaehlung.nichtBestanden,
              fern: zaehlung.nichtAngetreten,
              offen: zaehlung.offen,
            })}
          </p>
          {gruppen.map(({ ordnung, teams }) => (
            <div key={ordnung.id} className="break-inside-avoid">
              <h3 className="mb-1 font-medium [overflow-wrap:anywhere]">{katalogName(ordnung, sprache)}</h3>
              <table className="w-full table-fixed text-sm hyphens-auto [overflow-wrap:anywhere]">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="w-10 py-1 pr-2">{t("Nr.")}</th>
                    <th className="py-1 pr-2">{t("Hundeführer:in")}</th>
                    <th className="py-1 pr-2">{t("Hund")}</th>
                    <th className="py-1">{t("Ergebnis")}</th>
                  </tr>
                </thead>
                <tbody>
                  {teams.map((team) => (
                    <tr key={team.id} className="border-b align-top last:border-b-0">
                      <td className="py-1.5 pr-2 tabular-nums">{team.startNumber}</td>
                      <td className="py-1.5 pr-2 [overflow-wrap:anywhere]">{teamName(t, team)}</td>
                      <td className="py-1.5 pr-2 [overflow-wrap:anywhere]">{hundName(team)}</td>
                      <td className="py-1.5 [overflow-wrap:anywhere]">
                        {[
                          ergebnisWort(t, team.result),
                          team.score != null ? punkteText(t, team.score, ordnung.maxPoints) : null,
                          team.result === PRUEFUNG_ERGEBNIS.Offen ? null : team.resultNote,
                        ]
                          .filter(Boolean)
                          .join(" · ")}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
        </section>
      )}
    </div>
  );
}

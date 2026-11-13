"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { Ban, Pencil, Printer, Trash2, UserPlus } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { datumKurz } from "@/lib/pruefung";
import { datumLang, gruppiereTeams, hatErgebnisse, uhrzeitText } from "@/lib/pruefungen";
import { katalogName } from "@/lib/katalog-name";
import { PRUEFUNG_ERGEBNIS, PRUEFUNG_STATUS, type PruefungTeam, type VereinsPruefung } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ErgebnisSheet } from "@/components/pruefungen/ergebnis-sheet";
import { PruefungSheet } from "@/components/pruefungen/pruefung-sheet";
import { ReifeFenster } from "@/components/pruefungen/reife-fenster";
import { TeamEintragenSheet } from "@/components/pruefungen/team-eintragen-sheet";
import { TeamZeile } from "@/components/pruefungen/team-zeile";

/**
 * Ein Prüfungstag: Kopf mit Datum, Ort und Richter:in, die Meldeliste je Ordnung mit Prüfungsreife und
 * Ergebnis je Team, Reihenfolge ändern, Teams eintragen und austragen, Druckansicht, Absagen und
 * Löschen. Der Verein steht in der Adresse (?verein=). Alles Weitere entscheidet der Server.
 */
export default function PruefungDetailPage() {
  const t = useT();
  const sprache = useSprache();
  const router = useRouter();
  const { examId } = useParams<{ examId: string }>();
  const [vereinId, setVereinId] = useState<string | null>(null);
  const [pruefung, setPruefung] = useState<VereinsPruefung | null>(null);
  const [nichtGefunden, setNichtGefunden] = useState(false);
  const [bearbeiten, setBearbeiten] = useState(false);
  const [eintragen, setEintragen] = useState(false);
  const [reifeTeam, setReifeTeam] = useState<PruefungTeam | null>(null);
  const [ergebnisTeam, setErgebnisTeam] = useState<PruefungTeam | null>(null);
  const [laeuft, setLaeuft] = useState(false);

  useEffect(() => {
    // Initial aus der Adresse gelesen (kein Next-Hook, damit die Seite ohne Suspense auskommt).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setVereinId(new URLSearchParams(window.location.search).get("verein") ?? "");
  }, []);

  const laden = useCallback(async () => {
    if (!vereinId) return;
    try {
      setPruefung(await api.get<VereinsPruefung>(`/api/clubs/${vereinId}/exams/${examId}`));
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) setNichtGefunden(true);
      else toast.error(err instanceof ApiError ? err.message : t("Prüfung konnte nicht geladen werden."));
    }
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vereinId, examId]);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden]);

  /** Eine Aktion des Servers, danach die Seite neu: ein Tipp, eine Antwort, kein Zwischenstand im Browser. */
  async function ausfuehren(aktion: () => Promise<unknown>, erfolg: string | null, fehler: string) {
    setLaeuft(true);
    try {
      await aktion();
      if (erfolg) toast.success(erfolg);
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : fehler);
    } finally {
      setLaeuft(false);
    }
  }

  if (nichtGefunden || vereinId === "") {
    return (
      <Card>
        <CardContent className="py-6 text-center text-muted-foreground">{t("Diese Prüfung gibt es nicht.")}</CardContent>
      </Card>
    );
  }
  if (!pruefung || !vereinId) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  const basis = `/api/clubs/${vereinId}/exams/${pruefung.id}`;
  const gruppen = gruppiereTeams(pruefung);
  const mitErgebnissen = hatErgebnisse(pruefung.entries);
  const abgesagt = pruefung.status === PRUEFUNG_STATUS.Abgesagt;
  const uhr = uhrzeitText(pruefung.startTime);
  const hoechstens = (regulationId: string) => pruefung.regulations.find((o) => o.id === regulationId)?.maxPoints;

  async function absagen() {
    if (!window.confirm(t("Die Prüfung absagen? Die Familien der gemeldeten Mitglieder-Teams bekommen eine Nachricht."))) return;
    await ausfuehren(() => api.post(`${basis}/cancel`), t("Prüfung abgesagt."), t("Prüfung konnte nicht abgesagt werden."));
  }

  async function loeschen() {
    // Ohne Teams gibt es nichts zu verlieren; mit Teams fragt die Seite nach.
    if (pruefung!.entries.length > 0 && !window.confirm(t("Die Prüfung samt Meldeliste löschen? Die Familien der gemeldeten Mitglieder-Teams bekommen eine Nachricht."))) return;
    setLaeuft(true);
    try {
      await api.delete(basis);
      toast.success(t("Prüfung gelöscht."));
      router.replace(`/trainer/pruefungen?verein=${vereinId}`);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Prüfung konnte nicht gelöscht werden."));
      setLaeuft(false);
    }
  }

  async function austragen(team: PruefungTeam) {
    const warnung = team.result === PRUEFUNG_ERGEBNIS.Bestanden ? t("Das Team austragen? Die Leistung beim Hund wird dann wieder entfernt.") : t("Das Team austragen?");
    if (!window.confirm(warnung)) return;
    await ausfuehren(() => api.delete(`${basis}/entries/${team.id}`), t("Team ausgetragen."), t("Team konnte nicht ausgetragen werden."));
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex min-w-0 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <h1 className="text-2xl font-semibold tracking-tight [overflow-wrap:anywhere]">{datumLang(pruefung.date, sprache)}</h1>
          {abgesagt && <Badge variant="destructive">{t("Abgesagt")}</Badge>}
          {pruefung.status === PRUEFUNG_STATUS.Fertig && <Badge variant="secondary">{t("Durchgeführt")}</Badge>}
        </div>
        <p className="text-muted-foreground [overflow-wrap:anywhere]">{[uhr, pruefung.location].filter(Boolean).join(" · ")}</p>
        {pruefung.judgeName && <p className="text-sm [overflow-wrap:anywhere]">{t("Richter:in: {name}", { name: pruefung.judgeName })}</p>}
        {pruefung.registrationDeadline && (
          <p className="text-sm text-muted-foreground">{t("Meldeschluss: {datum}", { datum: datumKurz(pruefung.registrationDeadline, sprache) })}</p>
        )}
        {pruefung.note && <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{pruefung.note}</p>}
      </div>

      <div className="flex flex-wrap gap-2">
        {!abgesagt && (
          <Button onClick={() => setEintragen(true)} disabled={laeuft} className="coarse:min-h-11">
            <UserPlus className="size-4" />
            {t("Team eintragen")}
          </Button>
        )}
        <Button variant="outline" onClick={() => setBearbeiten(true)} disabled={laeuft} className="coarse:min-h-11">
          <Pencil className="size-4" />
          {t("Bearbeiten")}
        </Button>
        <Link
          href={`/trainer/pruefungen/${pruefung.id}/druck?verein=${vereinId}`}
          className={buttonVariants({ variant: "outline", className: "coarse:min-h-11" })}
        >
          <Printer className="size-4" />
          {t("Druckansicht")}
        </Link>
      </div>

      {gruppen.length === 0 ? (
        <Card>
          <CardContent className="py-8 text-center text-muted-foreground">{t("Noch kein Team eingetragen.")}</CardContent>
        </Card>
      ) : (
        gruppen.map(({ ordnung, teams }) => (
          <section key={ordnung.id} className="flex min-w-0 flex-col gap-3">
            <h2 className="font-heading text-lg font-semibold tracking-tight [overflow-wrap:anywhere]">
              {katalogName(ordnung, sprache)}{" "}
              <span className="text-sm font-normal text-muted-foreground">({teams.length === 1 ? t("1 Team") : t("{n} Teams", { n: teams.length })})</span>
            </h2>
            <Card>
              <CardContent>
                <ul className="flex flex-col divide-y">
                  {teams.map((team, index) => (
                    <TeamZeile
                      key={team.id}
                      team={team}
                      erster={index === 0}
                      letzter={index === teams.length - 1}
                      hoechstens={ordnung.maxPoints}
                      gesperrt={laeuft}
                      onReife={() => setReifeTeam(team)}
                      onErgebnis={() => setErgebnisTeam(team)}
                      onVerschieben={(richtung) =>
                        ausfuehren(() => api.post(`${basis}/entries/${team.id}/move`, { direction: richtung }), null, t("Die Reihenfolge konnte nicht geändert werden."))
                      }
                      onAustragen={() => austragen(team)}
                    />
                  ))}
                </ul>
              </CardContent>
            </Card>
          </section>
        ))
      )}

      {!abgesagt && !mitErgebnissen && (
        <Button variant="outline" disabled={laeuft} onClick={absagen} className="self-start coarse:min-h-11">
          <Ban className="size-4" />
          {t("Prüfung absagen")}
        </Button>
      )}
      {!mitErgebnissen && (
        <Button
          variant="ghost"
          disabled={laeuft}
          onClick={loeschen}
          className="self-start text-destructive hover:text-destructive coarse:min-h-11"
        >
          <Trash2 className="size-4" />
          {t("Prüfung löschen")}
        </Button>
      )}

      <PruefungSheet
        clubId={vereinId}
        pruefung={pruefung}
        open={bearbeiten}
        onOpenChange={setBearbeiten}
        onGespeichert={(gespeichert) => {
          setBearbeiten(false);
          setPruefung(gespeichert);
        }}
      />
      <TeamEintragenSheet
        clubId={vereinId}
        pruefung={pruefung}
        open={eintragen}
        onOpenChange={setEintragen}
        onEingetragen={async () => {
          setEintragen(false);
          await laden();
        }}
      />
      <ReifeFenster team={reifeTeam} onOpenChange={(offen) => !offen && setReifeTeam(null)} />
      <ErgebnisSheet
        clubId={vereinId}
        pruefung={pruefung}
        team={ergebnisTeam}
        hoechstens={ergebnisTeam ? hoechstens(ergebnisTeam.regulationId) : null}
        onOpenChange={(offen) => !offen && setErgebnisTeam(null)}
        onGespeichert={async () => {
          setErgebnisTeam(null);
          await laden();
        }}
      />
    </div>
  );
}

"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight, HandHelping, Plus } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { dauerText, stundenText } from "@/lib/arbeitseinsaetze";
import { useSprache, useT } from "@/lib/i18n";
import { datumLang } from "@/lib/pruefungen";
import type { ArbeitseinsatzListe, Club } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { SegmentUmschalter } from "@/components/ui/segment-umschalter";
import { Auswertung } from "@/components/arbeitseinsaetze/auswertung";
import { EinsatzSheet } from "@/components/arbeitseinsaetze/einsatz-sheet";

type Ansicht = "einsaetze" | "auswertung";

/**
 * Die Arbeitseinsätze des Vereins: "Einsätze" (Liste, neu anlegen - nur Verwaltung) und "Auswertung"
 * (Jahr, Tabelle, CSV - Verwaltung und Vorstand). Bei mehreren Vereinen wählt man oben einen (?verein=).
 * Wer keine Einsätze lesen darf, sieht hier nichts - der Server antwortet dann mit 404.
 */
export default function ArbeitseinsaetzePage() {
  const t = useT();
  const sprache = useSprache();
  const router = useRouter();
  const [vereine, setVereine] = useState<Club[] | null>(null);
  const [gewaehlt, setGewaehlt] = useState("");
  const [ansicht, setAnsicht] = useState<Ansicht>("einsaetze");
  const [liste, setListe] = useState<ArbeitseinsatzListe[] | null>(null);
  const [neu, setNeu] = useState(false);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    api
      .get<Club[]>("/api/clubs/mine")
      .then((alle) => setVereine(alle.filter((c) => c.canViewWork)))
      .catch((err) => {
        setVereine([]);
        toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden."));
      });
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setGewaehlt(new URLSearchParams(window.location.search).get("verein") ?? "");
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const verein = vereine?.find((c) => c.id === gewaehlt) ?? vereine?.[0] ?? null;
  const vereinId = verein?.id ?? null;

  const laden = useCallback(async () => {
    if (!vereinId) return;
    try {
      setListe(await api.get<ArbeitseinsatzListe[]>(`/api/clubs/${vereinId}/work-events`));
    } catch (err) {
      setListe((alt) => alt ?? []);
      toast.error(err instanceof ApiError ? err.message : t("Arbeitseinsätze konnten nicht geladen werden."));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vereinId]);

  useEffect(() => {
    // Beim Wechsel des Vereins soll nicht kurz die Liste des vorigen stehen.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setListe(null);
    void laden();
  }, [laden]);

  if (vereine === null) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  if (!verein) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold tracking-tight">{t("Arbeitseinsätze")}</h1>
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">
            {t("Arbeitseinsätze tragen die Verwaltung eines Vereins ein; Verwaltung und Vorstand sehen die Auswertung.")}
          </CardContent>
        </Card>
      </div>
    );
  }

  // ?from=: Zurück führt in die Liste dieses Vereins, nicht in die des ersten.
  const detailLink = (id: string) =>
    `/trainer/arbeitseinsaetze/${id}?verein=${verein.id}&from=${encodeURIComponent(`/trainer/arbeitseinsaetze?verein=${verein.id}`)}`;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight">{t("Arbeitseinsätze")}</h1>
          {vereine.length === 1 && <p className="text-muted-foreground [overflow-wrap:anywhere]">{verein.name}</p>}
        </div>
        {verein.canManageWork && (
          <Button onClick={() => setNeu(true)} className="coarse:min-h-11">
            <Plus className="size-4" />
            {t("Neuer Einsatz")}
          </Button>
        )}
      </div>

      {vereine.length > 1 && (
        <div role="group" aria-label={t("Verein")} className="flex flex-wrap gap-2">
          {vereine.map((c) => (
            <Chip key={c.id} gewaehlt={c.id === verein.id} onClick={() => setGewaehlt(c.id)}>
              {c.name}
            </Chip>
          ))}
        </div>
      )}

      <SegmentUmschalter
        ariaLabel={t("Ansicht")}
        wert={ansicht}
        segmente={[
          ["einsaetze", t("Einsätze")],
          ["auswertung", t("Auswertung")],
        ]}
        tabId={(wert) => `arbeit-ansicht-${wert}`}
        onChange={setAnsicht}
      />

      {ansicht === "auswertung" ? (
        <div role="tabpanel" aria-labelledby="arbeit-ansicht-auswertung">
          <Auswertung key={verein.id} clubId={verein.id} />
        </div>
      ) : (
        <div role="tabpanel" aria-labelledby="arbeit-ansicht-einsaetze">
          {liste === null ? (
            <p className="text-muted-foreground">{t("Lädt…")}</p>
          ) : liste.length === 0 ? (
            <Card>
              <CardContent className="flex flex-col items-center gap-2 py-8 text-center text-muted-foreground">
                <HandHelping className="size-8" />
                <p>{t("Noch kein Arbeitseinsatz eingetragen.")}</p>
              </CardContent>
            </Card>
          ) : (
            <ul className="flex flex-col gap-3">
              {liste.map((e) => (
                <li key={e.id}>
                  <Link
                    href={detailLink(e.id)}
                    className="flex min-w-0 items-center gap-3 rounded-xl border bg-card px-4 py-3 transition-colors hover:bg-accent/30 coarse:min-h-14"
                  >
                    <div className="flex min-w-0 flex-1 flex-col gap-1">
                      <span className="font-medium [overflow-wrap:anywhere]">{e.title}</span>
                      <span className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{datumLang(e.date, sprache)}</span>
                      <span className="text-xs text-muted-foreground">
                        {e.helperCount === 1 ? t("1 Helfer:in") : t("{n} Helfer:innen", { n: e.helperCount })}
                        {" · "}
                        {t("Dauer {dauer}", { dauer: dauerText(e.durationMinutes) })}
                        {e.helperCount > 0 && ` · ${t("{stunden} Helferstunden", { stunden: stundenText(e.totalMinutes, sprache) })}`}
                      </span>
                    </div>
                    <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <EinsatzSheet
        clubId={verein.id}
        open={neu}
        onOpenChange={setNeu}
        onGespeichert={(gespeichert) => {
          setNeu(false);
          router.push(detailLink(gespeichert.id));
        }}
      />
    </div>
  );
}

"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight, ClipboardCheck, Plus } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { heuteIso } from "@/lib/kalendertag";
import { datumLang, kommendeUndVergangene, ordnungenText, uhrzeitText } from "@/lib/pruefungen";
import { PRUEFUNG_STATUS, type Club, type VereinsPruefungListe } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Chip } from "@/components/ui/chip";
import { PruefungSheet } from "@/components/pruefungen/pruefung-sheet";

/**
 * Die Prüfungstage des Vereins (Ausbildungswart:in, Verwaltung): kommende oben mit Datum, Ordnungen
 * und Zahl der Teams, vergangene darunter eingeklappt. Bei mehreren Vereinen wählt man oben einen
 * (?verein=). Wer keine Prüfungen führen darf, sieht hier nichts - der Server antwortet dann mit 404.
 */
export default function PruefungenPage() {
  const t = useT();
  const sprache = useSprache();
  const router = useRouter();
  const [vereine, setVereine] = useState<Club[] | null>(null);
  const [gewaehlt, setGewaehlt] = useState("");
  const [liste, setListe] = useState<VereinsPruefungListe[] | null>(null);
  const [neu, setNeu] = useState(false);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    api
      .get<Club[]>("/api/clubs/mine")
      .then((alle) => setVereine(alle.filter((c) => c.canManageExams)))
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
      setListe(await api.get<VereinsPruefungListe[]>(`/api/clubs/${vereinId}/exams`));
    } catch (err) {
      setListe((alt) => alt ?? []);
      toast.error(err instanceof ApiError ? err.message : t("Prüfungen konnten nicht geladen werden."));
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
        <h1 className="text-2xl font-semibold tracking-tight">{t("Prüfungen")}</h1>
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">
            {t("Prüfungstage führen Ausbildungswart:innen und die Verwaltung eines Vereins.")}
          </CardContent>
        </Card>
      </div>
    );
  }

  const { kommend, vergangen } = kommendeUndVergangene(liste ?? [], heuteIso());
  // ?from=: Zurück führt in die Liste dieses Vereins, nicht in die des ersten (bei Prüfungsleitung in mehreren Vereinen).
  const detailLink = (id: string) =>
    `/trainer/pruefungen/${id}?verein=${verein.id}&from=${encodeURIComponent(`/trainer/pruefungen?verein=${verein.id}`)}`;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight">{t("Prüfungen")}</h1>
          {vereine.length === 1 && <p className="text-muted-foreground [overflow-wrap:anywhere]">{verein.name}</p>}
        </div>
        <Button onClick={() => setNeu(true)} className="coarse:min-h-11">
          <Plus className="size-4" />
          {t("Neue Prüfung")}
        </Button>
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

      {liste === null ? (
        <p className="text-muted-foreground">{t("Lädt…")}</p>
      ) : (
        <>
          <section className="flex flex-col gap-3">
            <h2 className="font-heading text-lg font-semibold tracking-tight">{t("Kommende Prüfungen")}</h2>
            {kommend.length === 0 ? (
              <Card>
                <CardContent className="flex flex-col items-center gap-2 py-8 text-center text-muted-foreground">
                  <ClipboardCheck className="size-8" />
                  <p>{t("Keine Prüfung geplant.")}</p>
                </CardContent>
              </Card>
            ) : (
              <ul className="flex flex-col gap-3">
                {kommend.map((p) => (
                  <li key={p.id}>
                    <PruefungKarte pruefung={p} href={detailLink(p.id)} sprache={sprache} />
                  </li>
                ))}
              </ul>
            )}
          </section>

          {vergangen.length > 0 && (
            <details className="group flex flex-col gap-3">
              <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-2 font-heading text-lg font-semibold tracking-tight [&::-webkit-details-marker]:hidden">
                {t("Vergangene Prüfungen ({n})", { n: vergangen.length })}
                <ChevronRight className="size-4 shrink-0 transition-transform group-open:rotate-90" aria-hidden />
              </summary>
              <ul className="mt-3 flex flex-col gap-3">
                {vergangen.map((p) => (
                  <li key={p.id}>
                    <PruefungKarte pruefung={p} href={detailLink(p.id)} sprache={sprache} />
                  </li>
                ))}
              </ul>
            </details>
          )}
        </>
      )}

      <PruefungSheet
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

function PruefungKarte({ pruefung, href, sprache }: { pruefung: VereinsPruefungListe; href: string; sprache: ReturnType<typeof useSprache> }) {
  const t = useT();
  const uhr = uhrzeitText(pruefung.startTime);
  return (
    <Link
      href={href}
      className="flex min-w-0 items-center gap-3 rounded-xl border bg-card px-4 py-3 transition-colors hover:bg-accent/30 coarse:min-h-14"
    >
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="font-medium [overflow-wrap:anywhere]">{datumLang(pruefung.date, sprache)}</span>
          {pruefung.status === PRUEFUNG_STATUS.Abgesagt && <Badge variant="destructive">{t("Abgesagt")}</Badge>}
          {pruefung.status === PRUEFUNG_STATUS.Fertig && <Badge variant="secondary">{t("Durchgeführt")}</Badge>}
        </div>
        <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{[uhr, pruefung.location].filter(Boolean).join(" · ")}</p>
        {pruefung.regulations.length > 0 && (
          <p className="text-sm [overflow-wrap:anywhere]">{ordnungenText(pruefung.regulations, sprache)}</p>
        )}
        <p className="text-xs text-muted-foreground">
          {pruefung.teamCount === 1 ? t("1 Team") : t("{n} Teams", { n: pruefung.teamCount })}
          {pruefung.passedCount > 0 && ` · ${t("{n} bestanden", { n: pruefung.passedCount })}`}
        </p>
      </div>
      <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
    </Link>
  );
}

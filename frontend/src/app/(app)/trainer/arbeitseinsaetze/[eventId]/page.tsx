"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Pencil, Timer, Trash2, UserMinus, UserPlus, Users } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { abhakbar, dauerText, helferName } from "@/lib/arbeitseinsaetze";
import { useSprache, useT } from "@/lib/i18n";
import { vereinszeitTag } from "@/lib/kalendertag";
import { datumLang } from "@/lib/pruefungen";
import type { Arbeitseinsatz, ArbeitseinsatzHelfer, Club } from "@/lib/types";
import { AktionenMenue } from "@/components/ui/aktionen-menue";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { EinsatzSheet } from "@/components/arbeitseinsaetze/einsatz-sheet";
import { GaesteSheet } from "@/components/arbeitseinsaetze/gaeste-sheet";
import { HelferSheet } from "@/components/arbeitseinsaetze/helfer-sheet";
import { MinutenSheet } from "@/components/arbeitseinsaetze/minuten-sheet";

/**
 * Ein Arbeitseinsatz: Kopf mit Datum, Titel, Dauer und Notiz, die abgehakten Helfer:innen mit ihren Minuten
 * (abweichende Minuten änderbar; Personen ohne Konto sind gekennzeichnet) und "Helfer:innen hinzufügen". Ändern darf nur die Verwaltung; der Vorstand
 * sieht den Einsatz lesend. Der Verein steht in der Adresse (?verein=). Alles Weitere entscheidet der Server.
 */
export default function ArbeitseinsatzDetailPage() {
  const t = useT();
  const sprache = useSprache();
  const router = useRouter();
  const { eventId } = useParams<{ eventId: string }>();
  const [vereinId, setVereinId] = useState<string | null>(null);
  const [darfAendern, setDarfAendern] = useState(false);
  const [einsatz, setEinsatz] = useState<Arbeitseinsatz | null>(null);
  const [nichtGefunden, setNichtGefunden] = useState(false);
  const [bearbeiten, setBearbeiten] = useState(false);
  const [hinzufuegen, setHinzufuegen] = useState(false);
  const [gaesteOffen, setGaesteOffen] = useState(false);
  const [minutenFuer, setMinutenFuer] = useState<ArbeitseinsatzHelfer | null>(null);
  const [laeuft, setLaeuft] = useState(false);

  useEffect(() => {
    // Initial aus der Adresse gelesen (kein Next-Hook, damit die Seite ohne Suspense auskommt).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setVereinId(new URLSearchParams(window.location.search).get("verein") ?? "");
  }, []);

  useEffect(() => {
    if (!vereinId) return;
    // Ob die Person ändern darf, sagt der Server mit den Vereinen; fehlt die Antwort, bleibt die Seite lesend.
    api
      .get<Club[]>("/api/clubs/mine")
      .then((alle) => setDarfAendern(alle.some((c) => c.id === vereinId && c.canManageWork)))
      .catch(() => setDarfAendern(false));
  }, [vereinId]);

  const laden = useCallback(async () => {
    if (!vereinId) return;
    try {
      setEinsatz(await api.get<Arbeitseinsatz>(`/api/clubs/${vereinId}/work-events/${eventId}`));
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) setNichtGefunden(true);
      else toast.error(err instanceof ApiError ? err.message : t("Arbeitseinsatz konnte nicht geladen werden."));
    }
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vereinId, eventId]);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden]);

  if (nichtGefunden || vereinId === "") {
    return (
      <Card>
        <CardContent className="py-6 text-center text-muted-foreground">{t("Diesen Arbeitseinsatz gibt es nicht.")}</CardContent>
      </Card>
    );
  }
  if (!einsatz || !vereinId) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true">
        <span className="sr-only">{t("Lädt…")}</span>
        <div className="h-20 rounded-xl bg-card motion-safe:animate-pulse" />
        <div className="h-36 rounded-xl bg-card motion-safe:animate-pulse" />
      </div>
    );
  }

  const basis = `/api/clubs/${vereinId}/work-events/${einsatz.id}`;
  const kannAbhaken = abhakbar(einsatz.date, vereinszeitTag());

  async function austragen(helfer: ArbeitseinsatzHelfer) {
    if (!window.confirm(t("{name} austragen?", { name: helferName(t, helfer) }))) return;
    setLaeuft(true);
    try {
      await api.delete(`${basis}/participants/${helfer.id}`);
      toast.success(t("Ausgetragen."));
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Person konnte nicht ausgetragen werden."));
    } finally {
      setLaeuft(false);
    }
  }

  async function loeschen() {
    // Ohne Helfer:innen gibt es nichts zu verlieren; mit ihnen fragt die Seite nach.
    if (einsatz!.participants.length > 0 && !window.confirm(t("Den Einsatz samt abgehakter Helfer:innen löschen?"))) return;
    setLaeuft(true);
    try {
      await api.delete(basis);
      toast.success(t("Einsatz gelöscht."));
      router.replace(`/trainer/arbeitseinsaetze?verein=${vereinId}`);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einsatz konnte nicht gelöscht werden."));
      setLaeuft(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex min-w-0 flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight [overflow-wrap:anywhere]">{einsatz.title}</h1>
        <p className="text-muted-foreground [overflow-wrap:anywhere]">{datumLang(einsatz.date, sprache)}</p>
        <p className="text-sm">{t("Dauer {dauer}", { dauer: dauerText(einsatz.durationMinutes) })}</p>
        {einsatz.note && <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{einsatz.note}</p>}
      </div>

      {darfAendern && (
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => setHinzufuegen(true)} disabled={laeuft || !kannAbhaken} className="coarse:min-h-11">
            <UserPlus className="size-4" />
            {t("Helfer:innen hinzufügen")}
          </Button>
          <Button variant="ghost" onClick={() => setBearbeiten(true)} disabled={laeuft} className="text-primary-text coarse:min-h-11">
            <Pencil className="size-4" />
            {t("Bearbeiten")}
          </Button>
          <Button variant="ghost" onClick={() => setGaesteOffen(true)} disabled={laeuft} className="text-primary-text coarse:min-h-11">
            <Users className="size-4" />
            {t("Personen ohne Konto")}
          </Button>
          {/* Löschen ist selten: hinter dem ⋯-Menü, die Rückfrage bleibt. */}
          <AktionenMenue
            label={t("Aktionen für {name}", { name: einsatz.title })}
            disabled={laeuft}
            className="coarse:size-11"
            aktionen={[{ label: t("Einsatz löschen"), icon: Trash2, onSelect: loeschen, destruktiv: true }]}
          />
        </div>
      )}
      {darfAendern && !kannAbhaken && (
        <p className="text-sm text-muted-foreground">{t("Abhaken kannst du erst ab dem Tag des Einsatzes.")}</p>
      )}

      <section className="flex min-w-0 flex-col gap-3">
        <h2 className="font-heading text-lg font-semibold tracking-tight">
          {t("Helfer:innen")}{" "}
          <span className="text-sm font-normal text-muted-foreground">({einsatz.participants.length})</span>
        </h2>
        {einsatz.participants.length === 0 ? (
          <Card>
            <CardContent className="py-8 text-center text-muted-foreground">{t("Noch niemand abgehakt.")}</CardContent>
          </Card>
        ) : (
          <Card>
            <CardContent>
              <ul className="flex flex-col divide-y">
                {einsatz.participants.map((h) => (
                  <li key={h.id} className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 py-3">
                    <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="font-medium [overflow-wrap:anywhere]">{helferName(t, h)}</span>
                      <span className="flex flex-wrap items-center gap-x-2 text-sm text-muted-foreground">
                        {dauerText(h.minutes)}
                        {h.guest && <Badge variant="outline">{t("ohne Konto")}</Badge>}
                        {h.custom && <Badge variant="secondary">{t("abweichend")}</Badge>}
                      </span>
                    </div>
                    {darfAendern && (
                      <AktionenMenue
                        label={t("Aktionen für {name}", { name: helferName(t, h) })}
                        disabled={laeuft}
                        className="coarse:size-11"
                        aktionen={[
                          { label: t("Dauer ändern"), icon: Timer, onSelect: () => setMinutenFuer(h) },
                          { label: t("Austragen"), icon: UserMinus, onSelect: () => void austragen(h), destruktiv: true },
                        ]}
                      />
                    )}
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        )}
      </section>

      <EinsatzSheet
        clubId={vereinId}
        einsatz={einsatz}
        open={bearbeiten}
        onOpenChange={setBearbeiten}
        onGespeichert={(gespeichert) => {
          setBearbeiten(false);
          setEinsatz(gespeichert);
        }}
      />
      <HelferSheet
        clubId={vereinId}
        einsatz={einsatz}
        open={hinzufuegen}
        onOpenChange={setHinzufuegen}
        onAbgehakt={async () => {
          setHinzufuegen(false);
          await laden();
        }}
      />
      <GaesteSheet clubId={vereinId} open={gaesteOffen} onOpenChange={setGaesteOffen} onGeaendert={laden} />
      <MinutenSheet
        clubId={vereinId}
        einsatz={einsatz}
        helfer={minutenFuer}
        onOpenChange={(offen) => !offen && setMinutenFuer(null)}
        onGespeichert={async () => {
          setMinutenFuer(null);
          await laden();
        }}
      />
    </div>
  );
}

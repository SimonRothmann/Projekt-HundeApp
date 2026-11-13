"use client";

import { useEffect, useState } from "react";
import { Wrench } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { WartungshinweisAdmin } from "@/lib/types";
import { ausEingabe, bannerInhalt, entwurfPruefen, zuEingabe, type Eingabezeit } from "@/lib/wartung";
import { WartungsBannerAnzeige } from "@/components/wartungs-banner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SchalterZeile } from "@/components/ui/switch";

/** Vorschlag für einen neuen Hinweis: heute, zur nächsten vollen Stunde. */
function vorschlagBeginn(): Eingabezeit {
  const d = new Date();
  d.setHours(d.getHours() + 1, 0, 0, 0);
  return zuEingabe(d.toISOString());
}

/**
 * "Wartungshinweis": kündigt eine längere Wartung in der App und auf der Startseite an.
 * Der Hinweis ist nur die Ankündigung; während der Wartung zeigt der Proxy von selbst
 * eine Wartungsseite (siehe docs/BETRIEB.md, Abschnitt Wartung).
 */
export function WartungshinweisSection() {
  const t = useT();
  const sprache = useSprache();
  const [geladen, setGeladen] = useState(false);
  const [beginn, setBeginn] = useState<Eingabezeit>({ datum: "", zeit: "" });
  const [ende, setEnde] = useState<Eingabezeit>({ datum: "", zeit: "" });
  const [text, setText] = useState("");
  const [aktiv, setAktiv] = useState(false);
  // Ob der Server gerade einen eingeschalteten Hinweis hält: Nur dann gibt es etwas auszuschalten.
  const [eingeblendet, setEingeblendet] = useState(false);
  const [laeuft, setLaeuft] = useState(false);

  function uebernehmen(hinweis: WartungshinweisAdmin) {
    setBeginn(hinweis.beginn ? zuEingabe(hinweis.beginn) : vorschlagBeginn());
    setEnde(zuEingabe(hinweis.ende));
    setText(hinweis.text ?? "");
    setAktiv(hinweis.aktiv);
    setEingeblendet(hinweis.aktiv);
  }

  useEffect(() => {
    let aktuell = true;
    api
      .get<WartungshinweisAdmin>("/api/admin/wartungshinweis")
      .then((hinweis) => {
        if (!aktuell) return;
        uebernehmen(hinweis);
        setGeladen(true);
      })
      .catch(() => {
        if (aktuell) toast.error(t("Der Wartungshinweis konnte nicht geladen werden."));
      });
    return () => {
      aktuell = false;
    };
    // Einmal beim Öffnen; t ändert sich nur mit der Sprache und holt nichts neu.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function speichern(e: React.FormEvent) {
    e.preventDefault();
    const pruefung = entwurfPruefen({ beginn, ende, text, aktiv });
    if (pruefung.art !== "ok") {
      toast.error(
        pruefung.art === "beginn"
          ? t("Beginn: Datum und Uhrzeit angeben.")
          : pruefung.art === "ende"
            ? t("Ende: Datum und Uhrzeit angeben – oder beides leer lassen.")
            : t("Das Ende muss nach dem Beginn liegen."),
      );
      return;
    }
    setLaeuft(true);
    try {
      const gespeichert = await api.put<WartungshinweisAdmin>("/api/admin/wartungshinweis", pruefung.anfrage);
      uebernehmen(gespeichert);
      toast.success(t("Gespeichert."));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Der Wartungshinweis konnte nicht gespeichert werden."));
    } finally {
      setLaeuft(false);
    }
  }

  async function ausschalten() {
    setLaeuft(true);
    try {
      await api.delete("/api/admin/wartungshinweis");
      setAktiv(false);
      setEingeblendet(false);
      toast.success(t("Der Hinweis ist ausgeschaltet."));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Der Wartungshinweis konnte nicht ausgeschaltet werden."));
    } finally {
      setLaeuft(false);
    }
  }

  // Die Vorschau zeigt, was die Nutzer:innen mit den eingegebenen Werten lesen würden.
  const beginnIso = ausEingabe(beginn);
  const endeIso = ausEingabe(ende);
  const vorschau = beginnIso
    ? bannerInhalt(
        { text: text.trim() || null, beginn: beginnIso, ende: endeIso, laeuft: new Date(beginnIso) <= new Date() },
        false,
        sprache,
      )
    : null;
  // Ohne eigenen Text zeigt das Feld den Standardtext als Platzhalter.
  const standardtext = beginnIso ? bannerInhalt({ text: null, beginn: beginnIso, ende: endeIso, laeuft: false }, false, sprache)?.text : undefined;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Wrench className="size-5 shrink-0" />
          {t("Wartungshinweis")}
        </CardTitle>
        <CardDescription>
          {t("Kündigt eine längere Wartung in der App und auf der Startseite an. Während der Wartung zeigt der Server von selbst eine Wartungsseite.")}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={speichern} className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-3 min-[420px]:grid-cols-2">
            <div className="flex min-w-0 flex-col gap-2">
              <Label htmlFor="wartung-beginn-datum">{t("Beginn (Datum)")}</Label>
              <Input
                id="wartung-beginn-datum"
                type="date"
                required
                value={beginn.datum}
                onChange={(e) => setBeginn({ ...beginn, datum: e.target.value })}
                className="h-11 min-w-0"
                disabled={!geladen}
              />
            </div>
            <div className="flex min-w-0 flex-col gap-2">
              <Label htmlFor="wartung-beginn-zeit">{t("Beginn (Uhrzeit)")}</Label>
              <Input
                id="wartung-beginn-zeit"
                type="time"
                required
                value={beginn.zeit}
                onChange={(e) => setBeginn({ ...beginn, zeit: e.target.value })}
                className="h-11 min-w-0"
                disabled={!geladen}
              />
            </div>
            <div className="flex min-w-0 flex-col gap-2">
              <Label htmlFor="wartung-ende-datum">{t("Ende (Datum, optional)")}</Label>
              <Input
                id="wartung-ende-datum"
                type="date"
                value={ende.datum}
                min={beginn.datum || undefined}
                onChange={(e) => setEnde({ ...ende, datum: e.target.value })}
                className="h-11 min-w-0"
                disabled={!geladen}
              />
            </div>
            <div className="flex min-w-0 flex-col gap-2">
              <Label htmlFor="wartung-ende-zeit">{t("Ende (Uhrzeit, optional)")}</Label>
              <Input
                id="wartung-ende-zeit"
                type="time"
                value={ende.zeit}
                onChange={(e) => setEnde({ ...ende, zeit: e.target.value })}
                className="h-11 min-w-0"
                disabled={!geladen}
              />
            </div>
          </div>

          <div className="flex min-w-0 flex-col gap-2">
            <Label htmlFor="wartung-text">{t("Eigener Text (optional)")}</Label>
            <textarea
              id="wartung-text"
              className="min-h-24 w-full min-w-0 resize-y rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/85 dark:focus-visible:ring-ring/50 md:text-sm dark:bg-input/30"
              placeholder={standardtext}
              value={text}
              maxLength={TEXTLAENGE.wartungshinweisText}
              onChange={(e) => setText(e.target.value)}
              disabled={!geladen}
            />
            <p className="text-xs text-muted-foreground">
              {t("Ein eigener Text ersetzt den Standardtext; die Zeiten stehen trotzdem davor.")}
            </p>
          </div>

          <div className="overflow-hidden rounded-lg border">
            <SchalterZeile
              an={aktiv}
              onChange={setAktiv}
              disabled={!geladen}
              titel={t("Hinweis anzeigen")}
              beschreibung={t("Ist er aus, sieht niemand den Hinweis – Text und Zeiten bleiben gespeichert.")}
            />
          </div>

          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium">{t("Vorschau")}</p>
            {vorschau ? (
              <WartungsBannerAnzeige text={vorschau.text} />
            ) : (
              <p className="text-sm text-muted-foreground">{t("Die Vorschau erscheint, sobald der Beginn feststeht.")}</p>
            )}
          </div>

          <div className="flex flex-col gap-2 sm:flex-row">
            <Button type="submit" className="w-full sm:w-auto" disabled={!geladen || laeuft}>
              {laeuft ? t("Wird gespeichert…") : t("Speichern")}
            </Button>
            {eingeblendet && (
              <Button type="button" variant="outline" className="w-full sm:w-auto" disabled={laeuft} onClick={ausschalten}>
                {t("Hinweis ausschalten")}
              </Button>
            )}
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

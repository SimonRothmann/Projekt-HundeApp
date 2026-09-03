"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRight, Sparkles, X } from "lucide-react";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  AKTUELLE_VERSION,
  formatiereVeroeffentlichung,
  VERSIONSHINWEISE,
} from "@/lib/versionshinweise";
import { useT } from "@/lib/i18n";

const SPEICHER_SCHLUESSEL = "dogity.neuerungen.gesehen";

/**
 * Merkt sich je Gerät, welche Fassung schon zur Kenntnis genommen wurde.
 *
 * Bewusst localStorage und keine Servereinstellung: Die Frage "hast du das
 * gesehen?" ist eine des Geräts, nicht des Kontos - und eine Tabellenspalte
 * samt Migration und Endpunkt wäre für eine weggeklickte Karte ein hoher
 * Preis. Jeder Zugriff ist abgesichert, weil das Lesen in privaten Fenstern
 * und bei gesperrten Website-Daten nicht nur leer zurückkommt, sondern
 * wirft - eine ungefangene Ausnahme hier würde das ganze Dashboard mitreißen.
 */
function lies(): string | null {
  try {
    return window.localStorage.getItem(SPEICHER_SCHLUESSEL);
  } catch {
    return null;
  }
}

function merke(version: string): void {
  try {
    window.localStorage.setItem(SPEICHER_SCHLUESSEL, version);
  } catch {
    // Ohne Speicher erscheint der Hinweis beim nächsten Besuch erneut.
    // Lästig, aber harmlos - und allemal besser als ein Absturz.
  }
}

/**
 * Die ganze Regel an einer Stelle - und damit prüfbar, ohne einen Browser
 * zu starten.
 *
 * Sie steckte vorher im Effekt und war damit nur von Hand zu testen: Wer
 * hätte gemerkt, wenn der Hinweis Neulingen doch erscheint? Genau dieser
 * Fall lässt sich beim Draufschauen am schwersten herstellen.
 */
export function sollHinweisZeigen(
  gesehen: string | null,
  aktuell: string,
  erststartLaeuft: boolean,
): boolean {
  if (gesehen === aktuell) return false;
  if (erststartLaeuft) return false;
  return true;
}

/**
 * "Neu in Dogity" auf dem Dashboard - einmal je Fassung, dann wieder weg.
 *
 * Ein dauerhaft eingeblendeter Änderungsverlauf wäre auf der Seite, die man
 * täglich zum Arbeiten öffnet, am dritten Tag nur noch Rauschen und würde
 * die anstehenden Trainings nach unten drücken. Die vollständige Liste hat
 * ihren festen Platz unter /neuerungen, in der Fußzeile und im Profil; hier
 * geht es allein um die Nachricht "es hat sich etwas getan".
 */
export function NeuerungenHinweis({ erststartLaeuft }: { erststartLaeuft: boolean }) {
  // null = noch nicht entschieden. Der Wert steht erst nach dem ersten
  // Effekt fest, denn localStorage gibt es beim Vorab-Rendern auf dem Server
  // nicht. Würde hier gleich "anzeigen" stehen, unterschiede sich das
  // Server-HTML vom ersten Browser-Rendern und React meldete eine
  // Hydration-Abweichung.
  const [zeigen, setZeigen] = useState<boolean | null>(null);
  const t = useT();

  useEffect(() => {
    const gesehen = lies();

    // Wer gerade erst anfängt, für den ist alles neu. Eine Meldung "Neu in
    // Dogity" neben dem Erststart-Leitfaden erklärt nichts und lenkt von den
    // ersten Schritten ab - also still als gesehen vermerken, damit sie auch
    // beim nächsten Besuch nicht nachträglich auftaucht.
    if (erststartLaeuft && gesehen !== AKTUELLE_VERSION) merke(AKTUELLE_VERSION);

    // Zustand aus Quellen, die es beim Rendern nicht gibt: localStorage
    // steht erst im Browser zur Verfügung, und "läuft gerade der Erststart?"
    // stammt aus einem Abruf, der beim ersten Rendern noch offen ist. Genau
    // dafür ist der Effekt da; die Regel zielt auf Zustand, der sich aus den
    // Eigenschaften herleiten lässt - dieser hier lässt es nicht.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setZeigen(sollHinweisZeigen(gesehen, AKTUELLE_VERSION, erststartLaeuft));
  }, [erststartLaeuft]);

  function wegklicken() {
    merke(AKTUELLE_VERSION);
    setZeigen(false);
  }

  if (!zeigen) return null;

  const neueste = VERSIONSHINWEISE[0];

  return (
    <Card className="border-primary/40 bg-primary/5">
      {/* CardAction statt eines dritten Kindes im Kopf: CardHeader ist ein
          Raster, kein Flex-Container - ein zusätzliches Kind landet sonst in
          einer eigenen Zeile unter dem Titel statt rechts daneben. */}
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Sparkles className="size-4 shrink-0 text-primary" aria-hidden />
          {t("Neu in Dogity")}
        </CardTitle>
        <CardAction>
          <Button
            type="button"
            size="icon-sm"
            variant="ghost"
            onClick={wegklicken}
            aria-label={t("Hinweis ausblenden")}
            title={t("Hinweis ausblenden")}
          >
            <X className="size-4" />
          </Button>
        </CardAction>
        <CardDescription>
          {t("Version {v}", { v: neueste.version })} · {formatiereVeroeffentlichung(neueste.datum)}
        </CardDescription>
      </CardHeader>

      {/* Nur die Überschrift, nicht die Punkte: Das Dashboard ist die Seite,
          die man täglich zum Arbeiten öffnet. Ein vollständiger Auszug schöbe
          die anstehenden Trainings unter den Bildschirmrand - und das für eine
          Nachricht, die nach einmal Lesen erledigt ist. */}
      <CardContent className="flex flex-col items-start gap-3">
        <p className="font-medium text-balance">{t(neueste.titel)}</p>
        {/* Wer hier durchklickt, hat die Neuerungen gesehen - sonst stünde
            die Karte nach dem Zurückkommen unverändert da und verlangte ein
            zweites Mal Aufmerksamkeit für dieselbe Nachricht. */}
        <Link
          href="/neuerungen"
          onClick={() => merke(AKTUELLE_VERSION)}
          className="inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline"
        >
          {t("Alle Neuerungen")}
          <ArrowRight className="size-4" aria-hidden />
        </Link>
      </CardContent>
    </Card>
  );
}

"use client";

import { useEffect, useRef, useState } from "react";
import { Download, Share2 } from "lucide-react";
import { toast } from "sonner";
import type { GpsTrack } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import {
  ampelStufe,
  baueBildTexte,
  faehrtenGeometrie,
  legedatumFuerDatei,
  neuesterAusgewerteterAblauf,
} from "@/lib/faehrten-bild";
import { BILD_BREITE, BILD_HOEHE, zeichneFaehrtenbild } from "@/lib/faehrten-bild-zeichnen";
import { useSprache, useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * "Als Bild teilen" an einer Fährte: ein Hochformat-Bild mit der Form der
 * Fährte und den Kennzahlen, zum Weitergeben im Messenger.
 *
 * Das Bild entsteht auf dem Gerät (Canvas) und wird nirgends hochgeladen. Es
 * enthält bewusst weder Karte noch Koordinaten noch Uhrzeiten: Die Form einer
 * Fährte auf einer Karte verriete den Ort. Einen öffentlichen Link mit
 * GPS-Punkten gibt es deshalb nicht (siehe Datenschutzerklärung).
 */
export function FaehrteTeilen({ track, hundeName }: { track: GpsTrack; hundeName?: string }) {
  const t = useT();
  const [offen, setOffen] = useState(false);

  // Weniger als zwei Punkte sind keine Linie - dann gibt es nichts zu zeigen.
  if (faehrtenGeometrie(track, null) === null) return null;

  return (
    <>
      <Button type="button" size="sm" variant="ghost" onClick={() => setOffen(true)}>
        <Share2 className="size-4" />
        {t("Als Bild teilen")}
      </Button>
      <Sheet open={offen} onOpenChange={setOffen}>
        <SheetContent side="bottom" className="max-h-[92vh] overflow-y-auto">
          <SheetHeader>
            <SheetTitle>{t("Fährte als Bild teilen")}</SheetTitle>
            <SheetDescription>{t("Ohne Karte und ohne Koordinaten: Niemand sieht, wo die Fährte lag.")}</SheetDescription>
          </SheetHeader>
          {/* Erst beim Öffnen eingehängt: Das Bild wird nur gezeichnet, wenn
              jemand es sehen will, und beim Schließen räumt die Komponente auf. */}
          <BildVorschau track={track} hundeName={hundeName} />
        </SheetContent>
      </Sheet>
    </>
  );
}

type Bild = { url: string; datei: File };

/**
 * Die Schrift der App, damit das Bild wie die App aussieht. Im Canvas steht
 * eine Webschrift erst zur Verfügung, wenn sie geladen ist - deshalb werden die
 * gebrauchten Schnitte vor dem Zeichnen angefordert. Klappt das nicht, bleibt
 * die Systemschrift.
 */
async function ladeSchrift(): Promise<string> {
  const rueckfall = "system-ui, -apple-system, sans-serif";
  try {
    const app = getComputedStyle(document.body).fontFamily;
    if (!app) return rueckfall;
    await Promise.all(["500", "600", "700", "800"].map((gewicht) => document.fonts.load(`${gewicht} 40px ${app}`)));
    return `${app}, ${rueckfall}`;
  } catch {
    return rueckfall;
  }
}

function kannTeilen(datei: File): boolean {
  try {
    return (
      typeof navigator !== "undefined" &&
      typeof navigator.share === "function" &&
      typeof navigator.canShare === "function" &&
      navigator.canShare({ files: [datei] })
    );
  } catch {
    return false;
  }
}

function BildVorschau({ track, hundeName }: { track: GpsTrack; hundeName?: string }) {
  const t = useT();
  const sprache = useSprache();
  const [mitName, setMitName] = useState(true);
  const [bild, setBild] = useState<Bild | null>(null);
  const [fehler, setFehler] = useState(false);
  // Die Adresse des aktuell gezeigten Bildes - beim Wechsel und beim Schließen
  // freigeben, sonst bleibt jedes Bild bis zum Neuladen der Seite im Speicher.
  const urlRef = useRef<string | null>(null);
  const zeigeName = mitName && Boolean(hundeName);

  useEffect(() => {
    let aktiv = true;
    (async () => {
      try {
        const ablauf = neuesterAusgewerteterAblauf(track.walkRuns);
        const geometrie = faehrtenGeometrie(track, ablauf);
        if (!geometrie) throw new Error("keine Linie");
        const texte = baueBildTexte(t, { track, ablauf, hundeName: zeigeName ? (hundeName ?? null) : null, sprache });
        const schrift = await ladeSchrift();

        const canvas = document.createElement("canvas");
        canvas.width = BILD_BREITE;
        canvas.height = BILD_HOEHE;
        const ctx = canvas.getContext("2d");
        if (!ctx) throw new Error("kein Canvas");
        zeichneFaehrtenbild(ctx, {
          geometrie,
          ampel: ablauf?.avgDeviationMeters != null ? ampelStufe(ablauf.avgDeviationMeters) : null,
          texte,
          schrift,
        });
        const blob = await new Promise<Blob | null>((fertig) => canvas.toBlob(fertig, "image/png"));
        if (!aktiv) return;
        if (!blob) throw new Error("kein Bild");

        // Der Dateiname nennt keinen Hundenamen: Wer ihn im Bild ausblendet,
        // will ihn auch nicht im Dateinamen der Nachricht wiederfinden.
        const datei = new File([blob], `dogity-faehrte-${legedatumFuerDatei(track)}.png`, { type: "image/png" });
        const url = URL.createObjectURL(blob);
        const alt = urlRef.current;
        urlRef.current = url;
        setBild({ url, datei });
        setFehler(false);
        if (alt) URL.revokeObjectURL(alt);
      } catch {
        if (aktiv) setFehler(true);
      }
    })();
    return () => {
      aktiv = false;
    };
  }, [track, hundeName, zeigeName, sprache, t]);

  useEffect(
    () => () => {
      if (urlRef.current) URL.revokeObjectURL(urlRef.current);
      urlRef.current = null;
    },
    [],
  );

  async function teilen(datei: File) {
    try {
      await navigator.share({ files: [datei] });
    } catch (err) {
      // Abbrechen im Teilen-Menü ist keine Störung.
      if (err instanceof DOMException && err.name === "AbortError") return;
      toast.error(t("Teilen hat nicht geklappt."));
    }
  }

  function speichern(eintrag: Bild) {
    const verweis = document.createElement("a");
    verweis.href = eintrag.url;
    verweis.download = eintrag.datei.name;
    document.body.appendChild(verweis);
    verweis.click();
    verweis.remove();
    toast.success(t("Fährtenbild gespeichert."));
  }

  return (
    <div className="flex min-w-0 flex-col gap-3 p-4 pt-0">
      {/* Das Seitenverhältnis steht fest, damit das Sheet beim Erscheinen des
          Bildes nicht springt. Höhe begrenzt, damit Schalter und Knöpfe auf
          einem kleinen Telefon ohne Scrollen erreichbar bleiben. */}
      <div
        className="mx-auto overflow-hidden rounded-lg border bg-muted/30"
        style={{ aspectRatio: `${BILD_BREITE} / ${BILD_HOEHE}`, width: `min(100%, ${(52 * BILD_BREITE) / BILD_HOEHE}vh)` }}
      >
        {bild ? (
          // eslint-disable-next-line @next/next/no-img-element -- Blob-Adresse des eben gezeichneten Bildes, next/image bringt hier nichts.
          <img
            src={bild.url}
            alt={t("Vorschau des Fährtenbilds")}
            className="block h-full w-full object-contain"
            width={BILD_BREITE}
            height={BILD_HOEHE}
          />
        ) : (
          <p className="flex h-full items-center justify-center p-4 text-center text-sm text-muted-foreground">
            {fehler ? t("Das Bild konnte nicht erstellt werden.") : t("Bild wird erstellt…")}
          </p>
        )}
      </div>

      {hundeName && (
        <button
          type="button"
          role="switch"
          aria-checked={mitName}
          onClick={() => setMitName((vorher) => !vorher)}
          className="flex min-h-11 w-full items-center justify-between gap-3 rounded-lg border px-3 text-left text-sm"
        >
          <span className="min-w-0 break-words">{t("Hundename zeigen")}</span>
          <span
            aria-hidden="true"
            className={cn(
              "relative h-6 w-10 shrink-0 rounded-full transition-colors",
              mitName ? "bg-primary" : "bg-muted-foreground/40",
            )}
          >
            <span
              className={cn(
                "absolute top-0.5 size-5 rounded-full bg-white shadow transition-all",
                mitName ? "left-[1.125rem]" : "left-0.5",
              )}
            />
          </span>
        </button>
      )}

      {bild &&
        (kannTeilen(bild.datei) ? (
          <Button type="button" className="w-full coarse:min-h-11" onClick={() => teilen(bild.datei)}>
            <Share2 className="size-4" />
            {t("Bild teilen")}
          </Button>
        ) : (
          <Button type="button" className="w-full coarse:min-h-11" onClick={() => speichern(bild)}>
            <Download className="size-4" />
            {t("Bild speichern")}
          </Button>
        ))}
    </div>
  );
}

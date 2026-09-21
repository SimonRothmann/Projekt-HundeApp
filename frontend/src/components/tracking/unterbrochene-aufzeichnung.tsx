"use client";

import { useState, useSyncExternalStore } from "react";
import { usePathname, useRouter } from "next/navigation";
import { History } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth-context";
import {
  aufzeichnungsEnde,
  markerAnzahl,
  schluesselVon,
  sicherungenAbonnieren,
  sicherungenAuflisten,
  sicherungenStand,
  sicherungLoeschen,
  sicherungSpeichern,
  wirdAngezeigt,
  type AufzeichnungsSicherung,
  type SpeicherErgebnis,
} from "@/lib/aufzeichnung-sicherung";
import { useT } from "@/lib/i18n";

function zeitpunkt(ms: number) {
  const d = new Date(ms);
  const heute = new Date().toDateString() === d.toDateString();
  return heute
    ? d.toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" })
    : d.toLocaleString("de-DE", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
}

/**
 * Eine Aufzeichnung, die nicht ordentlich beendet wurde - mit den drei
 * Auswegen: weitermachen, speichern, was da ist, oder verwerfen.
 *
 * Erscheint am Recorder selbst (dann mit "Weiter aufzeichnen") und im
 * Hinweis der App-Hülle (siehe UnterbrocheneAufzeichnungen).
 */
export function UnterbrocheneAufzeichnungKarte({
  sicherung,
  beschaeftigt = false,
  onFortsetzen,
  onSpeichern,
  onVerwerfen,
}: {
  sicherung: AufzeichnungsSicherung;
  beschaeftigt?: boolean;
  /** Fehlt, wo nicht an Ort und Stelle weitergemacht werden kann. */
  onFortsetzen?: () => void;
  onSpeichern: () => void;
  onVerwerfen: () => void;
}) {
  const t = useT();
  const marker = markerAnzahl(sicherung);
  const faehrte = sicherung.art === "faehrte";

  return (
    <div
      role="status"
      className="flex w-full flex-col gap-2 rounded-lg border border-primary/40 bg-surface p-3"
    >
      <p className="flex items-center gap-2 font-medium">
        <History className="size-4 shrink-0 text-primary-text" />
        {faehrte ? t("Unterbrochene Fährte") : t("Unterbrochener Ablauf")}
      </p>
      <p className="text-sm text-muted-foreground">
        {t("{von} bis {bis} · {punkte} Punkte", {
          von: zeitpunkt(sicherung.begonnen),
          bis: zeitpunkt(aufzeichnungsEnde(sicherung)),
          punkte: sicherung.points.length - marker,
        })}
        {faehrte && ` · ${t("{anzahl} Marker", { anzahl: marker })}`}
      </p>
      <p className="text-sm">
        {t("Die Aufzeichnung wurde nicht beendet, ist aber auf diesem Gerät gesichert.")}
      </p>
      <div className="flex flex-wrap gap-2">
        {onFortsetzen && (
          <Button size="sm" disabled={beschaeftigt} onClick={onFortsetzen} className="coarse:min-h-11">
            {t("Weiter aufzeichnen")}
          </Button>
        )}
        <Button
          size="sm"
          variant={onFortsetzen ? "outline" : "default"}
          disabled={beschaeftigt}
          onClick={onSpeichern}
          className="coarse:min-h-11"
        >
          {beschaeftigt ? t("Speichert…") : t("So speichern")}
        </Button>
        <Button
          size="sm"
          variant="ghost"
          disabled={beschaeftigt}
          onClick={() => {
            if (confirm(t("Aufzeichnung verwerfen? Die gesicherten Punkte gehen verloren."))) onVerwerfen();
          }}
          className="text-destructive hover:text-destructive coarse:min-h-11"
        >
          {t("Verwerfen")}
        </Button>
      </div>
    </div>
  );
}

/** Meldet das Ergebnis eines Speicherns - gemeinsam für Recorder und Hinweis. */
export function speicherErgebnisMelden(
  ergebnis: SpeicherErgebnis,
  t: ReturnType<typeof useT>,
  art: AufzeichnungsSicherung["art"],
) {
  const faehrte = art === "faehrte";
  if (ergebnis.ausgang === "gespeichert") {
    toast.success(faehrte ? t("Fährte gespeichert.") : t("Ablauf-Versuch gespeichert."));
  } else if (ergebnis.ausgang === "offline") {
    toast.success(
      faehrte
        ? t("Fährte offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.")
        : t("Ablauf-Versuch offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist."),
    );
  } else {
    toast.error(ergebnis.meldung ?? t("Das hat nicht geklappt."), {
      description: t("Die Aufzeichnung bleibt auf diesem Gerät gesichert."),
    });
  }
}

/**
 * Hinweis in der App-Hülle auf unterbrochene Aufzeichnungen, die gerade kein
 * Recorder zeigt.
 *
 * Der typische Fall: iOS hat die App im Hintergrund beendet, beim nächsten
 * Öffnen startet sie auf der Startseite. Die Fährte ist gesichert - aber wer
 * weiß das, wenn er nicht auf die Hundeseite geht?
 */
export function UnterbrocheneAufzeichnungen() {
  const t = useT();
  const { user } = useAuth();
  const router = useRouter();
  const pfad = usePathname();
  const [beschaeftigt, setBeschaeftigt] = useState<string | null>(null);
  // Nur das Abonnement zählt: Es rendert neu, wenn eine Sicherung
  // verschwindet oder ein Recorder kommt oder geht. Die Liste selbst wird
  // dann frisch gelesen - wenige Einträge, das kostet nichts.
  useSyncExternalStore(sicherungenAbonnieren, sicherungenStand, () => 0);
  const offen = user
    ? sicherungenAuflisten(user.userId).filter((s) => !wirdAngezeigt(schluesselVon(s)))
    : [];

  if (offen.length === 0) return null;

  async function speichern(s: AufzeichnungsSicherung) {
    const schluessel = schluesselVon(s);
    setBeschaeftigt(schluessel);
    const ergebnis = await sicherungSpeichern(s, s.art === "faehrte" ? t("Fährte") : t("Ablauf-Versuch"));
    setBeschaeftigt(null);
    speicherErgebnisMelden(ergebnis, t, s.art);
  }

  return (
    <div className="mb-4 flex flex-col gap-3">
      {offen.map((s) => (
        <UnterbrocheneAufzeichnungKarte
          key={schluesselVon(s)}
          sicherung={s}
          beschaeftigt={beschaeftigt === schluesselVon(s)}
          // Weitermachen geht nur am Recorder. Liegt er auf einer anderen
          // Seite, führt der Knopf dorthin; auf dieser Seite ist er gerade
          // nicht zu sehen (etwa eine zugeklappte Einheit im Tagebuch) -
          // dann bleiben Speichern und Verwerfen.
          onFortsetzen={
            s.seite !== pfad
              ? () => router.push(s.art === "faehrte" ? `${s.seite}#faehrte-aufnehmen` : s.seite)
              : undefined
          }
          onSpeichern={() => speichern(s)}
          onVerwerfen={() => sicherungLoeschen(schluesselVon(s))}
        />
      ))}
    </div>
  );
}
